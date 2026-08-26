using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class MasterDataService(HrmDbContext db, IAuthzService authz, IUnitOfWork unitOfWork) : IMasterDataService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> WorkerTypes = new(StringComparer.OrdinalIgnoreCase)
        { "employee", "contingent", "intern", "volunteer" };
    private static readonly HashSet<string> WorkerStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "pre-hire", "active", "on-leave", "notice", "terminated" };

    public async Task<MasterDataBatchDto> PreviewImportAsync(WorkerImportPreviewRequest request, string actorSubjectId, CancellationToken ct)
    {
        RequireActor(actorSubjectId);
        var validation = await ValidateImportAsync(request.Rows, ct);
        var batch = NewBatch("worker-import", actorSubjectId, DateOnly.FromDateTime(DateTime.UtcNow));
        batch.FileName = SafeFileName(request.FileName);
        batch.PayloadJson = Serialize(request.Rows);
        ApplyValidation(batch, request.Rows.Count, validation);
        db.MasterDataBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return Map(batch);
    }

    public async Task<MasterDataBatchDto> PreviewBulkAsync(WorkerBulkPreviewRequest request, string actorSubjectId, CancellationToken ct)
    {
        RequireActor(actorSubjectId);
        if (!DateOnly.TryParse(request.EffectiveDate, out var effectiveDate))
            throw new DomainException("master-data-effective-date", "EffectiveDate must use yyyy-MM-dd.");
        if (effectiveDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("master-data-effective-date", "Bulk changes cannot be backdated.");
        var validation = await ValidateBulkAsync(request.Rows, effectiveDate, ct);
        var batch = NewBatch("bulk-update", actorSubjectId, effectiveDate);
        batch.PayloadJson = Serialize(request.Rows);
        ApplyValidation(batch, request.Rows.Count, validation);
        db.MasterDataBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return Map(batch);
    }

    public async Task<MasterDataBatchDto> ApplyAsync(Guid id, string actorSubjectId, CancellationToken ct)
    {
        RequireActor(actorSubjectId);
        var batch = await db.MasterDataBatches.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("master-data-batch-not-found", $"Master-data batch {id} does not exist.");
        if (batch.Status != "previewed")
            throw new DomainException("master-data-batch-state", "Only a previewed batch can be applied.");
        if (batch.ErrorCount > 0)
            throw new DomainException("master-data-batch-errors", "Resolve every validation error and preview the file again before applying it.");

        await unitOfWork.ExecuteAsync(async transactionCt =>
        {
            List<WorkerRecoverySnapshot> snapshots;
            if (batch.BatchType == "worker-import")
            {
                var rows = Deserialize<List<WorkerImportRow>>(batch.PayloadJson) ?? [];
                var validation = await ValidateImportAsync(rows, transactionCt);
                EnsureStillValid(validation);
                snapshots = await ApplyImportAsync(rows, transactionCt);
            }
            else if (batch.BatchType == "bulk-update")
            {
                var rows = Deserialize<List<WorkerBulkChangeRow>>(batch.PayloadJson) ?? [];
                var validation = await ValidateBulkAsync(rows, batch.EffectiveDate, transactionCt);
                EnsureStillValid(validation);
                snapshots = await ApplyBulkAsync(rows, batch.EffectiveDate, transactionCt);
            }
            else
            {
                throw new DomainException("master-data-batch-type", $"Batch type {batch.BatchType} cannot be applied through this endpoint.");
            }

            batch.SnapshotJson = Serialize(snapshots);
            batch.Status = "applied";
            batch.AppliedAt = DateTimeOffset.UtcNow;
            batch.AppliedBySubjectId = actorSubjectId;
            batch.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(transactionCt);
        }, ct);
        return Map(batch);
    }

    public async Task<MasterDataBatchDto> RollbackAsync(Guid id, string actorSubjectId, CancellationToken ct)
    {
        RequireActor(actorSubjectId);
        var batch = await db.MasterDataBatches.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("master-data-batch-not-found", $"Master-data batch {id} does not exist.");
        if (batch.Status != "applied" || batch.AppliedAt is null)
            throw new DomainException("master-data-batch-state", "Only an applied batch can be rolled back.");
        if (batch.AppliedAt < DateTimeOffset.UtcNow.AddDays(-30))
            throw new DomainException("master-data-rollback-expired", "The 30-day master-data rollback window has expired.");

        await unitOfWork.ExecuteAsync(async transactionCt =>
        {
            var snapshots = Deserialize<List<WorkerRecoverySnapshot>>(batch.SnapshotJson) ?? [];
            foreach (var snapshot in snapshots)
            {
                var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == snapshot.WorkerId, transactionCt);
                if (worker is null) continue;
                if (snapshot.Created)
                {
                    worker.IsArchived = true;
                    worker.Status = "archived";
                    worker.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    Restore(worker, snapshot);
                }
                if (snapshot.MovementId is Guid movementId)
                {
                    var movement = await db.Movements.FirstOrDefaultAsync(x => x.Id == movementId, transactionCt);
                    if (movement is not null) movement.Status = "cancelled";
                }
            }
            batch.Status = "rolled-back";
            batch.RolledBackAt = DateTimeOffset.UtcNow;
            batch.UpdatedBy = actorSubjectId;
            batch.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(transactionCt);
        }, ct);
        return Map(batch);
    }

    public async Task<MasterDataBatchDto> ReactivateAsync(Guid workerId, WorkerReactivateRequest request, string actorSubjectId, CancellationToken ct)
    {
        RequireActor(actorSubjectId);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("worker-reactivation-reason", "A reactivation reason is required.");
        var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == workerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        if (!worker.IsArchived)
            throw new DomainException("worker-not-archived", "Only an archived worker can be reactivated.");
        var snapshot = Snapshot(worker, created: false);
        worker.IsArchived = false;
        worker.Status = worker.StartDate is null || worker.StartDate > DateOnly.FromDateTime(DateTime.UtcNow) ? "pre-hire" : "active";
        worker.EndDate = null;
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        worker.UpdatedBy = actorSubjectId;
        var batch = NewBatch("reactivation", actorSubjectId, DateOnly.FromDateTime(DateTime.UtcNow));
        batch.Status = "applied";
        batch.RowCount = batch.ReadyCount = 1;
        batch.PayloadJson = Serialize(new { workerId, request.Reason });
        batch.SummaryJson = Serialize(new[] { new MasterDataBatchSample(worker.EmployeeNo, "reactivate", "archived", worker.Status) });
        batch.SnapshotJson = Serialize(new[] { snapshot });
        batch.AppliedAt = DateTimeOffset.UtcNow;
        batch.AppliedBySubjectId = actorSubjectId;
        db.MasterDataBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return Map(batch);
    }

    public async Task<Paged<MasterDataBatchDto>> ListAsync(string? batchType, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var query = db.MasterDataBatches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(batchType)) query = query.Where(x => x.BatchType == batchType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var rows = (await query.Take(200).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
        return new Paged<MasterDataBatchDto>(rows.Select(Map).ToList(), rows.Count, 1, 200);
    }

    private async Task<ValidationResult> ValidateImportAsync(List<WorkerImportRow> rows, CancellationToken ct)
    {
        var result = StartValidation(rows.Count);
        if (rows.Count is 0 or > 1000)
        {
            result.Errors.Add(new MasterDataBatchError(0, null, "rows", "An import must contain between 1 and 1,000 rows."));
            return result;
        }
        var workers = await db.Workers.AsNoTracking().ToListAsync(ct);
        var byNo = workers.Where(x => !string.IsNullOrWhiteSpace(x.EmployeeNo)).ToDictionary(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        var orgs = await db.OrgUnits.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var locations = await db.WorkLocations.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var seenNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNrcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var rowNo = index + 1;
            var row = rows[index];
            var beforeErrors = result.Errors.Count;
            var employeeNo = Clean(row.EmployeeNo);
            if (string.IsNullOrWhiteSpace(row.FirstName)) Add("firstName", "First name is required.");
            if (string.IsNullOrWhiteSpace(row.LastName)) Add("lastName", "Last name is required.");
            if (employeeNo is not null && !seenNos.Add(employeeNo)) Add("employeeNo", "Employee number appears more than once in this file.");
            if (!WorkerTypes.Contains(row.WorkerType)) Add("workerType", "Worker type must be employee, contingent, intern, or volunteer.");
            if (row.StartDate is not null && !TryParseImportDate(row.StartDate, out _)) Add("startDate", "Start date must use DD-MM-YYYY.");
            if (row.OrgUnitCode is not null && !orgs.ContainsKey(row.OrgUnitCode)) Add("orgUnitCode", "Organisation unit code was not found.");
            if (row.LocationCode is not null && !locations.ContainsKey(row.LocationCode)) Add("locationCode", "Location code was not found.");
            ValidateUnique("email", row.Email, seenEmails, workers, x => x.Email, employeeNo, Add);
            ValidateUnique("nrc", row.Nrc, seenNrcs, workers, x => x.Nrc, employeeNo, Add);
            if (employeeNo is not null && byNo.TryGetValue(employeeNo, out var existing) && existing.IsArchived)
                Add("employeeNo", "The matching worker is archived. Reactivate that record instead of importing over it.");
            if (result.Errors.Count != beforeErrors) continue;
            Worker? current = null;
            if (employeeNo is not null) byNo.TryGetValue(employeeNo, out current);
            var action = current is not null
                ? ImportWouldChange(current, row, orgs, locations) ? "update" : "unchanged"
                : "create";
            if (action == "unchanged") result.Unchanged++;
            else result.Ready++;
            if (result.Samples.Count < 10)
                result.Samples.Add(new MasterDataBatchSample(employeeNo ?? "automatic", action,
                    current?.FullName ?? "new worker", $"{row.FirstName} {row.LastName}".Trim()));

            void Add(string field, string message) => result.Errors.Add(new MasterDataBatchError(rowNo, employeeNo, field, message));
        }
        return result;
    }

    private async Task<ValidationResult> ValidateBulkAsync(List<WorkerBulkChangeRow> rows, DateOnly effectiveDate, CancellationToken ct)
    {
        var result = StartValidation(rows.Count);
        if (rows.Count is 0 or > 1000)
        {
            result.Errors.Add(new MasterDataBatchError(0, null, "rows", "A bulk update must contain between 1 and 1,000 rows."));
            return result;
        }
        var workers = await db.Workers.AsNoTracking().ToListAsync(ct);
        var byNo = workers.ToDictionary(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        var orgs = await db.OrgUnits.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var locations = await db.WorkLocations.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var seenNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNrcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var rowNo = index + 1;
            var row = rows[index];
            var employeeNo = row.EmployeeNo.Trim();
            var beforeErrors = result.Errors.Count;
            if (!seenNos.Add(employeeNo)) Add("employeeNo", "Employee number appears more than once in this batch.");
            if (!byNo.TryGetValue(employeeNo, out var worker)) Add("employeeNo", "Worker was not found.");
            else if (worker.IsArchived) Add("employeeNo", "Archived workers must be reactivated before a bulk update.");
            if (row.Status is not null && !WorkerStatuses.Contains(row.Status)) Add("status", "Status is not valid for an active worker record.");
            if (row.OrgUnitCode is not null && !orgs.ContainsKey(row.OrgUnitCode)) Add("orgUnitCode", "Organisation unit code was not found.");
            if (row.LocationCode is not null && !locations.ContainsKey(row.LocationCode)) Add("locationCode", "Location code was not found.");
            if (row.ManagerEmployeeNo is not null && !byNo.ContainsKey(row.ManagerEmployeeNo)) Add("managerEmployeeNo", "Manager employee number was not found.");
            if (effectiveDate > DateOnly.FromDateTime(DateTime.UtcNow) && HasImmediateFields(row))
                Add("effectiveDate", "Future-effective batches may contain organisation, location, manager, grade, and job-title changes only.");
            ValidateUnique("email", row.Email, seenEmails, workers, x => x.Email, employeeNo, Add);
            ValidateUnique("nrc", row.Nrc, seenNrcs, workers, x => x.Nrc, employeeNo, Add);
            if (result.Errors.Count != beforeErrors || worker is null) continue;
            var changed = BulkWouldChange(worker, row, orgs, locations, byNo);
            if (changed) result.Ready++; else result.Unchanged++;
            if (result.Samples.Count < 10)
                result.Samples.Add(new MasterDataBatchSample(employeeNo, changed ? "update" : "unchanged",
                    worker.Status, changed ? $"effective {effectiveDate:yyyy-MM-dd}" : "already correct"));

            void Add(string field, string message) => result.Errors.Add(new MasterDataBatchError(rowNo, employeeNo, field, message));
        }
        return result;
    }

    private async Task<List<WorkerRecoverySnapshot>> ApplyImportAsync(List<WorkerImportRow> rows, CancellationToken ct)
    {
        var workers = await db.Workers.ToListAsync(ct);
        var byNo = workers.ToDictionary(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        var usedNumbers = byNo.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orgs = await db.OrgUnits.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var locations = await db.WorkLocations.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var snapshots = new List<WorkerRecoverySnapshot>();
        foreach (var row in rows)
        {
            var employeeNo = Clean(row.EmployeeNo) ?? IssueEmployeeNo(usedNumbers);
            if (!byNo.TryGetValue(employeeNo, out var worker))
            {
                worker = new Worker { EmployeeNo = employeeNo, FirstName = row.FirstName.Trim(), LastName = row.LastName.Trim() };
                db.Workers.Add(worker);
                byNo[employeeNo] = worker;
                snapshots.Add(Snapshot(worker, created: true));
            }
            else
            {
                if (!ImportWouldChange(worker, row, orgs, locations)) continue;
                snapshots.Add(Snapshot(worker, created: false));
            }
            ApplyImport(worker, row, orgs, locations);
        }
        await db.SaveChangesAsync(ct);
        return snapshots;
    }

    private async Task<List<WorkerRecoverySnapshot>> ApplyBulkAsync(List<WorkerBulkChangeRow> rows, DateOnly effectiveDate, CancellationToken ct)
    {
        var workers = await db.Workers.ToListAsync(ct);
        var byNo = workers.ToDictionary(x => x.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        var orgs = await db.OrgUnits.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var locations = await db.WorkLocations.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        var snapshots = new List<WorkerRecoverySnapshot>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var row in rows)
        {
            var worker = byNo[row.EmployeeNo.Trim()];
            if (!BulkWouldChange(worker, row, orgs, locations, byNo)) continue;
            var snapshot = Snapshot(worker, created: false);
            var organisationChange = HasOrganisationFields(row);
            if (organisationChange)
            {
                var movement = new Movement
                {
                    WorkerId = worker.Id, MovementType = "bulk-master-data", Reason = "Controlled M32 bulk update",
                    EffectiveDate = effectiveDate, FromOrgUnitId = worker.OrgUnitId, FromJobTitle = worker.JobTitle,
                    FromGrade = worker.Grade, ToOrgUnitId = Resolve(row.OrgUnitCode, orgs, worker.OrgUnitId),
                    ToLocationId = Resolve(row.LocationCode, locations, worker.LocationId),
                    ToManagerId = row.ManagerEmployeeNo is null ? worker.ManagerId : byNo[row.ManagerEmployeeNo].Id,
                    ToGrade = row.Grade ?? worker.Grade, ToJobTitle = row.JobTitle ?? worker.JobTitle,
                    Status = effectiveDate > today ? "approved" : "executed",
                };
                db.Movements.Add(movement);
                snapshot = snapshot with { MovementId = movement.Id };
                if (effectiveDate <= today) ApplyOrganisation(worker, row, orgs, locations, byNo);
            }
            ApplyImmediate(worker, row);
            worker.UpdatedAt = DateTimeOffset.UtcNow;
            snapshots.Add(snapshot);
        }
        await db.SaveChangesAsync(ct);
        return snapshots;
    }

    private static void ApplyImport(Worker worker, WorkerImportRow row, Dictionary<string, OrgUnit> orgs, Dictionary<string, WorkLocation> locations)
    {
        worker.FirstName = row.FirstName.Trim();
        worker.LastName = row.LastName.Trim();
        worker.MiddleName = row.MiddleName ?? worker.MiddleName;
        worker.Email = row.Email ?? worker.Email;
        worker.Phone = row.Phone ?? worker.Phone;
        worker.Nrc = row.Nrc ?? worker.Nrc;
        worker.Tpin = row.Tpin ?? worker.Tpin;
        worker.NapsaNumber = row.NapsaNumber ?? worker.NapsaNumber;
        worker.NhimaNumber = row.NhimaNumber ?? worker.NhimaNumber;
        worker.WorkerType = row.WorkerType;
        worker.OrgUnitId = Resolve(row.OrgUnitCode, orgs, worker.OrgUnitId);
        worker.LocationId = Resolve(row.LocationCode, locations, worker.LocationId);
        worker.Grade = row.Grade ?? worker.Grade;
        worker.JobTitle = row.JobTitle ?? worker.JobTitle;
        if (row.StartDate is not null && TryParseImportDate(row.StartDate, out var startDate)) worker.StartDate = startDate;
        if (worker.Status is null or "pre-hire") worker.Status = worker.StartDate is null ? "pre-hire" : "active";
        worker.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ApplyOrganisation(Worker worker, WorkerBulkChangeRow row, Dictionary<string, OrgUnit> orgs,
        Dictionary<string, WorkLocation> locations, Dictionary<string, Worker> workers)
    {
        worker.OrgUnitId = Resolve(row.OrgUnitCode, orgs, worker.OrgUnitId);
        worker.LocationId = Resolve(row.LocationCode, locations, worker.LocationId);
        if (row.ManagerEmployeeNo is not null) worker.ManagerId = workers[row.ManagerEmployeeNo].Id;
        if (row.Grade is not null) worker.Grade = row.Grade;
        if (row.JobTitle is not null) worker.JobTitle = row.JobTitle;
    }

    private static void ApplyImmediate(Worker worker, WorkerBulkChangeRow row)
    {
        if (row.Email is not null) worker.Email = row.Email;
        if (row.Phone is not null) worker.Phone = row.Phone;
        if (row.Nrc is not null) worker.Nrc = row.Nrc;
        if (row.Tpin is not null) worker.Tpin = row.Tpin;
        if (row.NapsaNumber is not null) worker.NapsaNumber = row.NapsaNumber;
        if (row.NhimaNumber is not null) worker.NhimaNumber = row.NhimaNumber;
        if (row.Status is not null) worker.Status = row.Status;
    }

    private static bool ImportWouldChange(Worker worker, WorkerImportRow row, Dictionary<string, OrgUnit> orgs, Dictionary<string, WorkLocation> locations) =>
        worker.FirstName != row.FirstName.Trim() || worker.LastName != row.LastName.Trim()
        || Different(row.MiddleName, worker.MiddleName) || Different(row.Email, worker.Email)
        || Different(row.Phone, worker.Phone) || Different(row.Nrc, worker.Nrc)
        || Different(row.Tpin, worker.Tpin) || Different(row.NapsaNumber, worker.NapsaNumber)
        || Different(row.NhimaNumber, worker.NhimaNumber) || !worker.WorkerType.Equals(row.WorkerType, StringComparison.OrdinalIgnoreCase)
        || Resolve(row.OrgUnitCode, orgs, worker.OrgUnitId) != worker.OrgUnitId
        || Resolve(row.LocationCode, locations, worker.LocationId) != worker.LocationId
        || Different(row.Grade, worker.Grade) || Different(row.JobTitle, worker.JobTitle)
        || (row.StartDate is not null && TryParseImportDate(row.StartDate, out var startDate) && startDate != worker.StartDate);

    private static readonly string[] ImportDateFormats =
    [
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "dd.MM.yyyy",
        "yyyy-MM-dd",
    ];

    private static bool TryParseImportDate(string? value, out DateOnly date)
    {
        var t = value?.Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            date = default;
            return false;
        }

        return DateOnly.TryParseExact(t, ImportDateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private static bool BulkWouldChange(Worker worker, WorkerBulkChangeRow row, Dictionary<string, OrgUnit> orgs,
        Dictionary<string, WorkLocation> locations, Dictionary<string, Worker> workers) =>
        Different(row.Email, worker.Email, allowEmpty: true) || Different(row.Phone, worker.Phone, allowEmpty: true)
        || Different(row.Nrc, worker.Nrc, allowEmpty: true) || Different(row.Tpin, worker.Tpin, allowEmpty: true)
        || Different(row.NapsaNumber, worker.NapsaNumber, allowEmpty: true) || Different(row.NhimaNumber, worker.NhimaNumber, allowEmpty: true)
        || Resolve(row.OrgUnitCode, orgs, worker.OrgUnitId) != worker.OrgUnitId
        || Resolve(row.LocationCode, locations, worker.LocationId) != worker.LocationId
        || (row.ManagerEmployeeNo is not null && workers[row.ManagerEmployeeNo].Id != worker.ManagerId)
        || Different(row.Grade, worker.Grade, allowEmpty: true) || Different(row.JobTitle, worker.JobTitle, allowEmpty: true)
        || Different(row.Status, worker.Status, allowEmpty: true);

    private static bool Different(string? requested, string? current, bool allowEmpty = false) =>
        requested is not null && (allowEmpty || !string.IsNullOrWhiteSpace(requested))
        && !string.Equals(requested.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool HasOrganisationFields(WorkerBulkChangeRow row) => row.OrgUnitCode is not null || row.LocationCode is not null
        || row.ManagerEmployeeNo is not null || row.Grade is not null || row.JobTitle is not null;
    private static bool HasImmediateFields(WorkerBulkChangeRow row) => row.Email is not null || row.Phone is not null || row.Nrc is not null
        || row.Tpin is not null || row.NapsaNumber is not null || row.NhimaNumber is not null || row.Status is not null;

    private static Guid? Resolve<T>(string? code, Dictionary<string, T> values, Guid? current) where T : Entity =>
        code is null ? current : values[code].Id;

    private static void ValidateUnique(string field, string? value, HashSet<string> seen, List<Worker> workers,
        Func<Worker, string?> selector, string? employeeNo, Action<string, string> add)
    {
        var clean = Clean(value);
        if (clean is null) return;
        if (!seen.Add(clean)) add(field, $"{field.ToUpperInvariant()} appears more than once in this batch.");
        if (workers.Any(x => !x.EmployeeNo.Equals(employeeNo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Clean(selector(x)), clean, StringComparison.OrdinalIgnoreCase)))
            add(field, $"{field.ToUpperInvariant()} is already assigned to another worker.");
    }

    private static string IssueEmployeeNo(HashSet<string> used)
    {
        for (var n = 1; n <= 999999; n++)
        {
            var candidate = $"EMP-{n:D4}";
            if (used.Add(candidate)) return candidate;
        }
        throw new DomainException("employee-number-exhausted", "No employee number is available.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string SafeFileName(string fileName) => string.IsNullOrWhiteSpace(fileName) ? "worker-import.csv" : Path.GetFileName(fileName)[..Math.Min(Path.GetFileName(fileName).Length, 200)];
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json);
    private static ValidationResult StartValidation(int _) => new();
    private static void EnsureStillValid(ValidationResult result)
    {
        if (result.Errors.Count > 0)
            throw new DomainException("master-data-preview-stale", "The master data changed after preview. Review the validation errors in a new preview.");
    }

    private void RequireActor(string actorSubjectId)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (string.IsNullOrWhiteSpace(actorSubjectId)) throw new DomainException("no-subject-claim", "The request carries no identity claim.");
    }

    private static MasterDataBatch NewBatch(string type, string actor, DateOnly effectiveDate) => new()
    {
        BatchType = type, RequestedBySubjectId = actor, EffectiveDate = effectiveDate,
    };

    private static void ApplyValidation(MasterDataBatch batch, int rows, ValidationResult validation)
    {
        batch.RowCount = rows;
        batch.ReadyCount = validation.Ready;
        batch.UnchangedCount = validation.Unchanged;
        batch.ErrorCount = validation.Errors.Count;
        batch.ErrorsJson = Serialize(validation.Errors);
        batch.SummaryJson = Serialize(validation.Samples);
    }

    private static MasterDataBatchDto Map(MasterDataBatch batch)
    {
        var canRollback = batch.Status == "applied" && batch.AppliedAt >= DateTimeOffset.UtcNow.AddDays(-30);
        return new MasterDataBatchDto(batch.Id, batch.BatchType, batch.FileName, batch.Status,
            batch.EffectiveDate.ToString("yyyy-MM-dd"), batch.RowCount, batch.ReadyCount,
            batch.UnchangedCount, batch.ErrorCount, batch.RequestedBySubjectId, batch.AppliedBySubjectId,
            batch.CreatedAt, batch.AppliedAt, batch.RolledBackAt, canRollback,
            Deserialize<List<MasterDataBatchError>>(batch.ErrorsJson) ?? [],
            Deserialize<List<MasterDataBatchSample>>(batch.SummaryJson) ?? []);
    }

    private static WorkerRecoverySnapshot Snapshot(Worker worker, bool created) => new(
        worker.Id, created, worker.FirstName, worker.MiddleName, worker.LastName, worker.Email, worker.Phone,
        worker.Nrc, worker.Tpin, worker.NapsaNumber, worker.NhimaNumber, worker.WorkerType, worker.Status,
        worker.OrgUnitId, worker.LocationId, worker.ManagerId, worker.Grade, worker.JobTitle,
        worker.StartDate, worker.EndDate, worker.IsArchived, null);

    private static void Restore(Worker worker, WorkerRecoverySnapshot snapshot)
    {
        worker.FirstName = snapshot.FirstName; worker.MiddleName = snapshot.MiddleName; worker.LastName = snapshot.LastName;
        worker.Email = snapshot.Email; worker.Phone = snapshot.Phone; worker.Nrc = snapshot.Nrc; worker.Tpin = snapshot.Tpin;
        worker.NapsaNumber = snapshot.NapsaNumber; worker.NhimaNumber = snapshot.NhimaNumber;
        worker.WorkerType = snapshot.WorkerType; worker.Status = snapshot.Status; worker.OrgUnitId = snapshot.OrgUnitId;
        worker.LocationId = snapshot.LocationId; worker.ManagerId = snapshot.ManagerId; worker.Grade = snapshot.Grade;
        worker.JobTitle = snapshot.JobTitle; worker.StartDate = snapshot.StartDate; worker.EndDate = snapshot.EndDate;
        worker.IsArchived = snapshot.IsArchived; worker.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private sealed class ValidationResult
    {
        public int Ready { get; set; }
        public int Unchanged { get; set; }
        public List<MasterDataBatchError> Errors { get; } = [];
        public List<MasterDataBatchSample> Samples { get; } = [];
    }

    private sealed record WorkerRecoverySnapshot(
        Guid WorkerId, bool Created, string FirstName, string? MiddleName, string LastName,
        string? Email, string? Phone, string? Nrc, string? Tpin, string? NapsaNumber,
        string? NhimaNumber, string WorkerType, string Status, Guid? OrgUnitId, Guid? LocationId,
        Guid? ManagerId, string? Grade, string? JobTitle, DateOnly? StartDate, DateOnly? EndDate,
        bool IsArchived, Guid? MovementId);
}
