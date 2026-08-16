using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

// ===================== Workers =====================
public sealed class WorkerRepository(HrmDbContext db) : IWorkerRepository
{
    public async Task<(List<Worker> Items, int Total)> ListAsync(WorkerListFilters filters, CancellationToken ct)
    {
        var q = db.Workers.AsQueryable();
        // M18 admin CRUD: archived workers stay out of the operational list
        // unless HR explicitly asks for them.
        if (!filters.IncludeArchived) q = q.Where(w => !w.IsArchived);
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.Trim().ToLower();
            q = q.Where(w => w.FullName.ToLower().Contains(s) || w.EmployeeNo.ToLower().Contains(s)
                || (w.Nrc != null && w.Nrc.ToLower().Contains(s))
                || (w.Email != null && w.Email.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(filters.Status)) q = q.Where(w => w.Status == filters.Status);
        if (filters.OrgUnitId.HasValue) q = q.Where(w => w.OrgUnitId == filters.OrgUnitId.Value);
        if (filters.LocationId.HasValue) q = q.Where(w => w.LocationId == filters.LocationId.Value);
        if (!string.IsNullOrWhiteSpace(filters.WorkerType)) q = q.Where(w => w.WorkerType == filters.WorkerType);
        if (!string.IsNullOrWhiteSpace(filters.Grade)) q = q.Where(w => w.Grade == filters.Grade);
        var total = await q.CountAsync(ct);
        var page = Math.Max(filters.Page, 1);
        var size = Math.Clamp(filters.PageSize, 1, 100);
        // Order client-side: EF Core's SQLite provider cannot translate ORDER BY
        // on DateTimeOffset columns (CreatedAt) into SQL.
        var items = await q.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items = items.OrderByDescending(w => w.CreatedAt).ToList();
        return (items, total);
    }

    public async Task<Worker?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Workers.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    // M14 identity link: resolve the worker record bound to a Keycloak subject id.
    // The global tenant query filter on the DbContext keeps the lookup
    // tenant-scoped automatically.
    public async Task<Worker?> FindBySubjectIdAsync(string subjectId, CancellationToken ct)
        => await db.Workers.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .FirstOrDefaultAsync(w => w.SubjectId == subjectId, ct);

    public async Task<Worker> CreateAsync(Worker worker, CancellationToken ct)
    {
        db.Workers.Add(worker);
        await db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<Worker> UpdateAsync(Worker worker, CancellationToken ct)
    {
        db.Workers.Update(worker);
        await db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }

    // Explicit AddRange + Save so the provider issues INSERTs even when the
    // entity has a non-default Guid key (Guid.CreateVersion7 initializer),
    // which otherwise makes collection-attached entities be treated as
    // existing (Modified) by EF Core's change tracker.
    public async Task AddEmergencyContactsAsync(IEnumerable<EmergencyContact> contacts, CancellationToken ct)
    {
        db.EmergencyContacts.AddRange(contacts);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddBankDetailsAsync(IEnumerable<WorkerBankDetail> details, CancellationToken ct)
    {
        db.WorkerBankDetails.AddRange(details);
        await db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        var worker = await GetByIdAsync(id, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {id} does not exist.");
        worker.IsArchived = true;
        worker.Status = "archived";
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(string employeeNo, CancellationToken ct)
        => await db.Workers.AnyAsync(w => w.EmployeeNo == employeeNo, ct);

    public async Task<(List<Assignment> Items, int Total)> ListAssignmentsAsync(Guid workerId, CancellationToken ct)
    {
        var items = await db.Assignments.Include(a => a.OrgUnit).Include(a => a.Location)
            .Where(a => a.WorkerId == workerId).OrderByDescending(a => a.StartDate).ToListAsync(ct);
        return (items, items.Count);
    }

    public async Task<Assignment> CreateAssignmentAsync(Assignment assignment, CancellationToken ct)
    {
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<(List<Movement> Items, int Total)> ListMovementsAsync(Guid workerId, CancellationToken ct)
    {
        var items = (await db.Movements.Where(m => m.WorkerId == workerId).ToListAsync(ct))
            .OrderByDescending(m => m.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<Movement> CreateMovementAsync(Movement movement, CancellationToken ct)
    {
        db.Movements.Add(movement);
        await db.SaveChangesAsync(ct);
        return movement;
    }

    public async Task<Movement?> GetMovementAsync(Guid id, CancellationToken ct)
        => await db.Movements.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task ExecuteMovementAsync(Movement movement, CancellationToken ct)
    {
        db.Movements.Update(movement);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Assignment>> ListAllAssignmentsAsync(CancellationToken ct)
        => await db.Assignments.Include(a => a.LegalEntity).Include(a => a.OrgUnit).Include(a => a.Location)
            .ToListAsync(ct);

    public async Task<Assignment> UpdateAssignmentAsync(Assignment assignment, CancellationToken ct)
    {
        db.Assignments.Update(assignment);
        await db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<List<LegalEntity>> ListAllLegalEntitiesAsync(CancellationToken ct)
        => await db.LegalEntities.ToListAsync(ct);

    public async Task<List<OrgUnit>> ListAllOrgUnitsAsync(CancellationToken ct)
        => await db.OrgUnits.ToListAsync(ct);

    public async Task<List<WorkLocation>> ListAllLocationsAsync(CancellationToken ct)
        => await db.WorkLocations.ToListAsync(ct);

    public async Task<List<Worker>> ListAllWorkersAsync(Guid? orgUnitId, CancellationToken ct)
    {
        var q = db.Workers.AsQueryable();
        if (orgUnitId.HasValue) q = q.Where(w => w.OrgUnitId == orgUnitId.Value);
        return await q.ToListAsync(ct);
    }

    public async Task<EmergencyContact?> GetEmergencyContactAsync(Guid id, CancellationToken ct)
        => await db.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<EmergencyContact> AddEmergencyContactAsync(EmergencyContact contact, CancellationToken ct)
    {
        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task UpdateEmergencyContactAsync(EmergencyContact contact, CancellationToken ct)
    {
        db.EmergencyContacts.Update(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEmergencyContactAsync(Guid id, CancellationToken ct)
    {
        var contact = await GetEmergencyContactAsync(id, ct)
            ?? throw new DomainException("contact-not-found", $"Emergency contact {id} does not exist.");
        db.EmergencyContacts.Remove(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task<WorkerBankDetail?> GetBankDetailAsync(Guid id, CancellationToken ct)
        => await db.WorkerBankDetails.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<WorkerBankDetail> AddBankDetailAsync(WorkerBankDetail detail, CancellationToken ct)
    {
        db.WorkerBankDetails.Add(detail);
        await db.SaveChangesAsync(ct);
        return detail;
    }

    public async Task UpdateBankDetailAsync(WorkerBankDetail detail, CancellationToken ct)
    {
        db.WorkerBankDetails.Update(detail);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteBankDetailAsync(Guid id, CancellationToken ct)
    {
        var detail = await GetBankDetailAsync(id, ct)
            ?? throw new DomainException("bank-detail-not-found", $"Bank detail {id} does not exist.");
        db.WorkerBankDetails.Remove(detail);
        await db.SaveChangesAsync(ct);
    }
}

// ===================== Time =====================
public sealed class TimeRepository(HrmDbContext db) : ITimeRepository
{
    public async Task<(List<LeaveRequest> Items, int Total)> ListLeaveRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.LeaveRequests.AsQueryable();
        if (workerId.HasValue) q = q.Where(l => l.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        // Order client-side: SQLite cannot translate ORDER BY on DateTimeOffset.
        var items = (await q.Include(l => l.Worker).Take(200).ToListAsync(ct))
            .OrderByDescending(l => l.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request, CancellationToken ct)
    {
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<List<LeaveBalanceLedger>> GetBalancesAsync(Guid workerId, string leaveTypeCode, CancellationToken ct)
        => (await db.LeaveBalanceLedgers
            .Where(l => l.WorkerId == workerId && l.LeaveTypeCode == leaveTypeCode)
            .ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();

    public async Task<List<LeaveBalanceLedger>> GetLedgerAsync(Guid workerId, CancellationToken ct)
        => (await db.LeaveBalanceLedgers
            .Where(l => l.WorkerId == workerId)
            .ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();

    public async Task<LeaveType?> GetLeaveTypeAsync(string code, CancellationToken ct)
        => await db.LeaveTypes.FirstOrDefaultAsync(t => t.Code == code && t.IsActive, ct);

    public async Task<List<LeaveType>> GetLeaveTypesAsync(CancellationToken ct)
        => await db.LeaveTypes.Where(t => t.IsActive).ToListAsync(ct);

    public async Task<DateOnly?> GetCurrentCutoffAsync(CancellationToken ct)
    {
        var latest = await db.PayPeriods.Where(p => p.Status == "open")
            .OrderByDescending(p => p.StartDate).Select(p => p.CutoffDate).FirstOrDefaultAsync(ct);
        return latest;
    }

    public async Task ReserveBalanceAsync(Guid workerId, string leaveTypeCode, decimal days, Guid referenceId, CancellationToken ct)
    {
        db.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = workerId,
            LeaveTypeCode = leaveTypeCode,
            Days = days, // caller passes a negative value (-requestedDays) for a reservation
            Reason = "request",
            ReferenceId = referenceId,
            ReferenceType = "leave-request",
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<(List<AttendanceCorrection> Items, int Total)> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.AttendanceCorrections.AsQueryable();
        if (workerId.HasValue) q = q.Where(c => c.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        var items = (await q.Include(c => c.Worker).Take(100).ToListAsync(ct))
            .OrderByDescending(c => c.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<AttendanceCorrection> CreateCorrectionAsync(AttendanceCorrection correction, CancellationToken ct)
    {
        db.AttendanceCorrections.Add(correction);
        await db.SaveChangesAsync(ct);
        return correction;
    }

    // ----- M3 attendance, roster, decisions -----
    public async Task<AttendanceRecord?> GetAttendanceAsync(Guid workerId, DateOnly workDate, CancellationToken ct)
        => await db.AttendanceRecords.FirstOrDefaultAsync(a => a.WorkerId == workerId && a.WorkDate == workDate, ct);

    public async Task<AttendanceRecord> CreateAttendanceAsync(AttendanceRecord record, CancellationToken ct)
    {
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<AttendanceRecord> UpdateAttendanceAsync(AttendanceRecord record, CancellationToken ct)
    {
        if (db.Entry(record).State == EntityState.Detached)
            db.AttendanceRecords.Update(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<List<AttendanceRecord>> ListAttendanceAsync(Guid workerId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var q = db.AttendanceRecords.Where(a => a.WorkerId == workerId);
        if (from.HasValue) q = q.Where(a => a.WorkDate >= from.Value);
        if (to.HasValue) q = q.Where(a => a.WorkDate <= to.Value);
        return await q.Include(a => a.Worker).Take(200).ToListAsync(ct); // already bounded by from/to window; keep insert order
    }

    public async Task<AttendanceCorrection?> GetCorrectionAsync(Guid id, CancellationToken ct)
        => await db.AttendanceCorrections.Include(c => c.Worker).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<AttendanceCorrection> UpdateCorrectionAsync(AttendanceCorrection correction, CancellationToken ct)
    {
        if (db.Entry(correction).State == EntityState.Detached)
            db.AttendanceCorrections.Update(correction);
        await db.SaveChangesAsync(ct);
        return correction;
    }

    public async Task<LeaveRequest?> GetLeaveRequestAsync(Guid id, CancellationToken ct)
        => await db.LeaveRequests.Include(l => l.Worker).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<LeaveRequest> UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken ct)
    {
        if (db.Entry(request).State == EntityState.Detached)
            db.LeaveRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task ReleaseReservationAsync(Guid leaveRequestId, CancellationToken ct)
    {
        // reverse a reservation: rows that were negative "request" entries for this reference become positive
        var rows = await db.LeaveBalanceLedgers
            .Where(l => l.ReferenceId == leaveRequestId && l.ReferenceType == "leave-request" && l.Days < 0 && l.Reason == "request")
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            r.Reason = "request-release";
            r.Days = -r.Days;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ConvertReservationAsync(Guid leaveRequestId, CancellationToken ct)
    {
        // convert an open reservation into a permanent (taken) deduction: keep the negative
        // sign, change reason so balance math counts it under "taken" instead of "reserved"
        var rows = await db.LeaveBalanceLedgers
            .Where(l => l.ReferenceId == leaveRequestId && l.ReferenceType == "leave-request" && l.Days < 0 && l.Reason == "request")
            .ToListAsync(ct);
        foreach (var r in rows)
            r.Reason = "approval";
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct)
        => await db.WorkCalendars.Include(c => c.Holidays).ToListAsync(ct);

    public async Task<List<ShiftDefinition>> ListShiftsAsync(CancellationToken ct)
        => await db.ShiftDefinitions.Where(s => s.IsActive).OrderBy(s => s.Code).ToListAsync(ct);

    public async Task<ShiftDefinition> CreateShiftAsync(ShiftDefinition shift, CancellationToken ct)
    {
        db.ShiftDefinitions.Add(shift);
        await db.SaveChangesAsync(ct);
        return shift;
    }

    public async Task<WorkerShiftAssignment?> GetShiftAssignmentAsync(Guid workerId, DateOnly date, CancellationToken ct)
        => await db.WorkerShiftAssignments.Include(a => a.Shift)
            .Include(a => a.Calendar).ThenInclude(c => c!.Holidays)
            .Where(a => a.WorkerId == workerId && a.EffectiveFrom <= date &&
                (!a.EffectiveTo.HasValue || a.EffectiveTo.Value >= date))
            .OrderByDescending(a => a.EffectiveFrom).FirstOrDefaultAsync(ct);

    public async Task<WorkerShiftAssignment> CreateShiftAssignmentAsync(WorkerShiftAssignment assignment, CancellationToken ct)
    {
        db.WorkerShiftAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        await db.Entry(assignment).Reference(a => a.Calendar).LoadAsync(ct);
        return assignment;
    }

    public async Task CloseOpenShiftAssignmentsAsync(Guid workerId, DateOnly effectiveTo, CancellationToken ct)
    {
        var rows = await db.WorkerShiftAssignments
            .Where(a => a.WorkerId == workerId && !a.EffectiveTo.HasValue && a.EffectiveFrom <= effectiveTo)
            .ToListAsync(ct);
        foreach (var row in rows) row.EffectiveTo = effectiveTo;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Worker?> FindWorkerByEmployeeNoAsync(string employeeNo, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.EmployeeNo == employeeNo && w.Status == "active", ct);

    public async Task<AttendanceImportBatch> CreateImportBatchAsync(AttendanceImportBatch batch, CancellationToken ct)
    {
        db.AttendanceImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return batch;
    }

    public async Task UpdateImportBatchAsync(AttendanceImportBatch batch, CancellationToken ct)
    {
        if (db.Entry(batch).State == EntityState.Detached) db.AttendanceImportBatches.Update(batch);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<AttendanceImportBatch>> ListImportBatchesAsync(CancellationToken ct)
        => (await db.AttendanceImportBatches.Take(50).ToListAsync(ct))
            .OrderByDescending(batch => batch.CreatedAt).ToList();

    public async Task<LeaveAccrualRun?> GetAccrualRunAsync(string period, CancellationToken ct)
        => await db.LeaveAccrualRuns.FirstOrDefaultAsync(r => r.Period == period, ct);

    public async Task<LeaveAccrualRun> CreateAccrualRunAsync(LeaveAccrualRun run, CancellationToken ct)
    {
        db.LeaveAccrualRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task UpdateAccrualRunAsync(LeaveAccrualRun run, CancellationToken ct)
    {
        if (db.Entry(run).State == EntityState.Detached) db.LeaveAccrualRuns.Update(run);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<LeaveAccrualRun>> ListAccrualRunsAsync(CancellationToken ct)
        => (await db.LeaveAccrualRuns.Take(50).ToListAsync(ct))
            .OrderByDescending(run => run.CreatedAt).ToList();

    public async Task<List<Worker>> ListAccrualWorkersAsync(CancellationToken ct)
        => await db.Workers.Where(w => w.Status == "active").ToListAsync(ct);

    public async Task<LeaveBalanceLedger> AddLedgerEntryAsync(LeaveBalanceLedger entry, CancellationToken ct)
    {
        db.LeaveBalanceLedgers.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<LeaveBalanceAdjustment> CreateAdjustmentAsync(LeaveBalanceAdjustment adjustment, CancellationToken ct)
    {
        db.LeaveBalanceAdjustments.Add(adjustment);
        await db.SaveChangesAsync(ct);
        return adjustment;
    }

    public async Task<List<LeaveBalanceAdjustment>> ListAdjustmentsAsync(CancellationToken ct)
        => (await db.LeaveBalanceAdjustments.Include(adjustment => adjustment.Worker).Take(50).ToListAsync(ct))
            .OrderByDescending(adjustment => adjustment.CreatedAt).ToList();
}

// ===================== Workflow =====================
public sealed class WorkflowRepository(HrmDbContext db) : IWorkflowRepository
{
    public async Task<WorkflowRequest> CreateRequestAsync(WorkflowRequest request, CancellationToken ct)
    {
        db.WorkflowRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<WorkflowRequest?> GetRequestAsync(Guid id, CancellationToken ct)
        => await db.WorkflowRequests.Include(w => w.Decisions).FirstOrDefaultAsync(w => w.Id == id, ct);

    // M16: the leave request id is the workflow subject id for type "leave".
    public async Task<WorkflowRequest?> GetOpenBySubjectAsync(string workflowType, Guid subjectWorkerId, CancellationToken ct)
        => await db.WorkflowRequests.Include(w => w.Decisions)
            .FirstOrDefaultAsync(w => w.WorkflowType == workflowType
                && w.SubjectWorkerId == subjectWorkerId
                && (w.Status == "submitted" || w.Status == "in-review" || w.Status == "returned"), ct);

    public async Task<WorkflowRequest> UpdateRequestAsync(WorkflowRequest request, CancellationToken ct)
    {
        // EF Core 10 demotes children added via the parent's collection to Modified
        // when the parent is Modified (state-propagation behavior change), turning the
        // child INSERT into an UPDATE with 0 rows affected. Re-attach brand-new
        // (detached) children directly to the context as Added with the FK set, which
        // keeps them as INSERTs alongside the parent UPDATE.
        foreach (var decision in request.Decisions)
        {
            if (db.Entry(decision).State == EntityState.Detached)
            {
                decision.RequestId = request.Id;
                db.WorkflowDecisions.Add(decision);
            }
        }
        if (db.Entry(request).State == EntityState.Detached)
            db.WorkflowRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<(List<WorkflowRequest> Items, int Total)> ListOpenRequestsAsync(CancellationToken ct)
    {
        var items = (await db.WorkflowRequests.Include(w => w.Decisions)
            .Where(w => w.Status == "submitted" || w.Status == "in-review")
            .ToListAsync(ct)).OrderByDescending(w => w.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<Guid?> FindManagerOfAsync(Guid workerId, CancellationToken ct)
        => await db.Workers.Where(w => w.Id == workerId).Select(w => w.ManagerId).FirstOrDefaultAsync(ct);

    public async Task<bool> IsDelegateForAsync(Guid delegatorId, Guid actorId, string workflowType, DateOnly date, CancellationToken ct)
        => await db.ApprovalDelegations.AnyAsync(d =>
            d.DelegatorId == delegatorId && d.DelegateWorkerId == actorId && d.IsActive &&
            d.FromDate <= date && (d.ToDate == null || d.ToDate >= date) &&
            (d.Scope == null || d.Scope == workflowType), ct);

    public async Task<Guid?> GetActiveDelegationForAsync(Guid delegatorId, string workflowType, DateOnly date, CancellationToken ct)
    {
        var delegation = await db.ApprovalDelegations
            .Where(d => d.DelegatorId == delegatorId && d.IsActive &&
                        d.FromDate <= date && (d.ToDate == null || d.ToDate >= date) &&
                        (d.Scope == null || d.Scope == workflowType))
            .OrderBy(d => d.Scope == null ? 1 : 0) // type-specific scope wins over blanket scope
            .Select(d => (Guid?)d.DelegateWorkerId).FirstOrDefaultAsync(ct);
        return delegation;
    }

    public async Task<Dictionary<Guid, string>> GetWorkerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
        => await db.Workers.Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, Name = $"{w.FirstName} {w.LastName}".Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
}

// ===================== Experience =====================
public sealed class ExperienceRepository(HrmDbContext db) : IExperienceRepository
{
    public async Task<(List<HrRequest> Items, int Total)> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.HrRequests.AsQueryable();
        if (workerId.HasValue) q = q.Where(r => r.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var items = (await q.Include(r => r.Messages).Include(r => r.Worker)
            .Take(100).ToListAsync(ct)).OrderByDescending(r => r.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<HrRequest?> GetRequestAsync(Guid id, CancellationToken ct)
        => await db.HrRequests.Include(r => r.Messages).Include(r => r.Worker).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<HrRequest> CreateRequestAsync(HrRequest request, CancellationToken ct)
    {
        db.HrRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<HrRequest> UpdateRequestAsync(HrRequest request, CancellationToken ct)
    {
        // EF Core 10 demotes children added via the parent's collection to Modified
        // when the parent is Modified (state-propagation behavior change). Re-attach
        // brand-new (detached) messages directly to the context as Added with the FK
        // set, which keeps them as INSERTs alongside the parent UPDATE.
        // M22: EF Core 10 also demotes EXISTING tracked children to Modified when
        // the parent is Modified, producing 0-row UPDATEs (DbUpdateConcurrencyException).
        // Pin every existing (non-detached) message to Unchanged so the tracker skips it.
        foreach (var msg in request.Messages.ToList())
        {
            var entry = db.Entry(msg);
            if (entry.State == EntityState.Detached)
            {
                msg.RequestId = request.Id;
                db.HrRequestMessages.Add(msg);
            }
            else if (entry.State != EntityState.Unchanged && entry.State != EntityState.Added)
            {
                entry.State = EntityState.Unchanged;
            }
        }
        if (db.Entry(request).State == EntityState.Detached)
            db.HrRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    // M22: explicit top-level insert so EF Core 10's Modified-parent demotion can
    // never turn the new message into a 0-row UPDATE. Same explicit-Add pattern
    // used for emergency contacts / bank details.
    public async Task<HrRequest> AddMessageAsync(HrRequest request, HrRequestMessage message, CancellationToken ct)
    {
        message.RequestId = request.Id;
        db.Set<HrRequestMessage>().Add(message);
        // status transition decided by the caller is already on the tracked parent
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<(List<HrLetter> Items, int Total)> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.HrLetters.AsQueryable();
        if (workerId.HasValue) q = q.Where(l => l.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        var items = (await q.Include(l => l.Worker)
            .Take(100).ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<HrLetter?> GetLetterAsync(Guid id, CancellationToken ct)
        => await db.HrLetters.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<HrLetter> CreateLetterAsync(HrLetter letter, CancellationToken ct)
    {
        db.HrLetters.Add(letter);
        await db.SaveChangesAsync(ct);
        return letter;
    }

    public async Task<HrLetter> UpdateLetterAsync(HrLetter letter, CancellationToken ct)
    {
        if (db.Entry(letter).State == EntityState.Detached)
            db.HrLetters.Update(letter);
        await db.SaveChangesAsync(ct);
        return letter;
    }

    public async Task<int> CountDisclosuresThisYearAsync(CancellationToken ct)
        => await db.ProtectedDisclosures.CountAsync(ct);

    public async Task<ProtectedDisclosure> CreateDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct)
    {
        db.ProtectedDisclosures.Add(disclosure);
        await db.SaveChangesAsync(ct);
        return disclosure;
    }

    public async Task<ProtectedDisclosure?> GetDisclosureByCaseReferenceAsync(string caseReference, CancellationToken ct)
        => await db.ProtectedDisclosures.FirstOrDefaultAsync(d => d.CaseReference == caseReference, ct);
}

// ===================== Payroll =====================
public sealed class PayrollRepository(HrmDbContext db) : IPayrollRepository
{
    public async Task<List<SalaryComponent>> ListComponentsAsync(string? type, CancellationToken ct)
    {
        var q = db.SalaryComponents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(c => c.ComponentType == type);
        return await q.Where(c => c.IsActive).OrderBy(c => c.Priority).ToListAsync(ct);
    }
    public async Task<SalaryStructure?> FindStructureAsync(string code, CancellationToken ct) =>
        await db.SalaryStructures.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code && s.IsActive, ct);
    public async Task<SalaryStructure?> FindStructureByCodeAsync(string code, CancellationToken ct) =>
        await db.SalaryStructures.FirstOrDefaultAsync(s => s.Code == code && !s.IsArchived, ct);
    public async Task<List<SalaryStructure>> ListStructuresAsync(CancellationToken ct) =>
        await db.SalaryStructures.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Component)
            .Where(s => !s.IsArchived && s.IsActive).OrderBy(s => s.Code).ToListAsync(ct);
    public async Task<SalaryStructure?> GetStructureAsync(Guid id, CancellationToken ct) =>
        await db.SalaryStructures
            .Include(s => s.Items).ThenInclude(i => i.Component)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsArchived, ct);
    public async Task<SalaryStructure> CreateStructureAsync(SalaryStructure structure, CancellationToken ct)
    {
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync(ct);
        return structure;
    }
    public async Task UpdateStructureAsync(SalaryStructure structure, CancellationToken ct)
    {
        if (db.Entry(structure).State == EntityState.Detached)
            db.SalaryStructures.Update(structure);
        await db.SaveChangesAsync(ct);
    }
    /// <summary>EF Core 10 + SQLite Guid-V7 bug: adding child entities via the
    /// navigation after the parent was saved throws a spurious
    /// DbUpdateConcurrencyException, so items are attached explicitly and saved
    /// in a separate phase with no Update() call.</summary>
    public async Task SetStructureItemsExplicitlyAsync(SalaryStructure structure,
        List<SalaryStructureItem> items, CancellationToken ct)
    {
        foreach (var i in items)
        {
            i.StructureId = structure.Id;
            i.TenantId = structure.TenantId;
        }
        db.Set<SalaryStructureItem>().AddRange(items);
        await db.SaveChangesAsync(ct);
    }
    public async Task ClearStructureItemsAsync(Guid structureId, CancellationToken ct)
    {
        var items = await db.SalaryStructureItems.Where(i => i.StructureId == structureId).ToListAsync(ct);
        db.SalaryStructureItems.RemoveRange(items);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Worker?> GetWorkerAsync(Guid id, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.Id == id, ct);
    // M25: resolve the worker linked to a Keycloak subject (self-service).
    public async Task<Worker?> GetWorkerBySubjectAsync(string subjectId, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.SubjectId == subjectId, ct);

    public async Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct)
        => await db.PayGroups.ToListAsync(ct);
    public async Task<List<PayGroup>> ListPayGroupsAllAsync(CancellationToken ct)
        => await db.PayGroups.ToListAsync(ct);
    public async Task<PayGroup?> GetPayGroupAsync(Guid id, CancellationToken ct)
        => await db.PayGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
    public async Task<List<SalaryComponent>> ListAllComponentsAsync(CancellationToken ct)
        => await db.SalaryComponents.Where(c => c.IsActive).OrderBy(c => c.Priority).ToListAsync(ct);
    public async Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken ct)
        => await db.SalaryComponents.FirstOrDefaultAsync(c => c.Id == id, ct);
    public async Task<List<WorkerPayrollProfile>> ListProfilesAsync(Guid? workerId, CancellationToken ct)
    {
        var q = db.WorkerPayrollProfiles
            .Include(p => p.Worker).Include(p => p.PayGroup)
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            .Where(p => !p.EffectiveTo.HasValue || p.EffectiveTo >= DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime));
        if (workerId.HasValue) q = q.Where(p => p.WorkerId == workerId.Value);
        return (await q.OrderByDescending(p => p.EffectiveFrom).ToListAsync(ct))
            .GroupBy(p => p.WorkerId).Select(g => g.First()).ToList();
    }
    public async Task<WorkerPayrollProfile?> FindOpenProfileAsync(Guid workerId, CancellationToken ct)
        => await db.WorkerPayrollProfiles
            .Include(p => p.Worker).Include(p => p.PayGroup)
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            .Where(p => p.WorkerId == workerId && (!p.EffectiveTo.HasValue || p.EffectiveTo >= DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime)))
            .OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
    public async Task<WorkerPayrollProfile> CreateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct)
    {
        db.WorkerPayrollProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }
    public async Task<WorkerPayrollProfile> UpdateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct)
    {
        // EF Core 10 state-propagation: re-attach detached component values as Added
        foreach (var v in profile.ComponentValues)
            if (db.Entry(v).State == EntityState.Detached)
            {
                v.ProfileId = profile.Id;
                db.WorkerComponentValues.Add(v);
            }
        if (db.Entry(profile).State == EntityState.Detached)
            db.WorkerPayrollProfiles.Update(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }
    public async Task DeleteProfileValuesAsync(Guid profileId, CancellationToken ct)
    {
        var values = await db.WorkerComponentValues.Where(v => v.ProfileId == profileId).ToListAsync(ct);
        db.WorkerComponentValues.RemoveRange(values);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PayPeriod>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct)
        => await db.PayPeriods.Where(p => p.PayGroupId == payGroupId).OrderByDescending(p => p.StartDate).ToListAsync(ct);

    public async Task<PayPeriod?> GetPeriodAsync(Guid id, CancellationToken ct)
        => await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<TaxSlab>> ListTaxSlabsAsync(string taxYear, CancellationToken ct)
        => await db.TaxSlabs.Where(s => s.TaxYear == taxYear && s.IsActive).OrderBy(s => s.Sequence).ToListAsync(ct);

    public async Task<TaxSlab?> GetTaxSlabAsync(Guid id, CancellationToken ct)
        => await db.TaxSlabs.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task UpdateTaxSlabAsync(TaxSlab slab, CancellationToken ct)
    {
        if (db.Entry(slab).State == EntityState.Detached)
            db.TaxSlabs.Update(slab);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ContributionRule>> ListContributionRulesAsync(CancellationToken ct)
        => await db.ContributionRules.Where(r => r.IsActive).ToListAsync(ct);

    public async Task<ContributionRule?> GetContributionRuleAsync(Guid id, CancellationToken ct)
        => await db.ContributionRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task UpdateContributionRuleAsync(ContributionRule rule, CancellationToken ct)
    {
        if (db.Entry(rule).State == EntityState.Detached)
            db.ContributionRules.Update(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePayGroupAsync(PayGroup group, CancellationToken ct)
    {
        if (db.Entry(group).State == EntityState.Detached)
            db.PayGroups.Update(group);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnsetDefaultPayGroupsAsync(CancellationToken ct, Guid keepId)
    {
        await db.PayGroups.Where(g => g.IsDefault && g.Id != keepId).ForEachAsync(g => g.IsDefault = false, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateComponentAsync(SalaryComponent component, CancellationToken ct)
    {
        if (db.Entry(component).State == EntityState.Detached)
            db.SalaryComponents.Update(component);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PayrollRun?> GetRunAsync(Guid id, CancellationToken ct)
        => await db.PayrollRuns.Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<List<PayrollRun>> ListRunsAsync(CancellationToken ct)
        => await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod)
            .OrderByDescending(r => r.PayPeriod!.StartDate).ThenByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<PayrollRun?> FindRunByPeriodAsync(Guid payPeriodId, CancellationToken ct)
        // Only an open (non-terminal) run blocks a new run: reversed/closed runs are
        // historical and a fresh replacement run may be created after reversal.
        => await db.PayrollRuns.FirstOrDefaultAsync(
            r => r.PayPeriodId == payPeriodId && r.Status != "reversed" && r.Status != "closed" && !r.IsReversal,
            ct);

    public async Task<PayrollRun> CreateRunAsync(PayrollRun run, CancellationToken ct)
    {
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<PayrollRun> UpdateRunAsync(PayrollRun run, CancellationToken ct)
    {
        if (db.Entry(run).State == EntityState.Detached)
            db.PayrollRuns.Update(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<(List<WorkerPayrollProfile> Profiles, List<SalaryComponent> Components, List<ContributionRule> Rules, List<TaxSlab> Slabs, DateOnly? Cutoff)>
        LoadCalculationInputsAsync(Guid payPeriodId, CancellationToken ct)
    {
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var profiles = (await db.WorkerPayrollProfiles
            .Include(p => p.Worker).ThenInclude(w => w!.BankDetails)
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            .Where(p => p.PayGroupId == period.PayGroupId)
            .ToListAsync(ct))
            .Where(p => !p.EffectiveTo.HasValue || p.EffectiveTo >= DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime))
            .ToList();
        var components = await db.SalaryComponents.Where(c => c.IsActive).OrderBy(c => c.Priority).ToListAsync(ct);
        var rules = await db.ContributionRules.Where(r => r.IsActive).ToListAsync(ct);
        var year = period.StartDate.Year.ToString();
        var slabs = await db.TaxSlabs.Where(s => s.TaxYear == year && s.IsActive).OrderBy(s => s.Sequence).ToListAsync(ct);
        return (profiles, components, rules, slabs, period.CutoffDate);
    }

    public async Task ClearRunLinesAsync(Guid runId, CancellationToken ct)
    {
        var lines = await db.PayrollRunLines.Where(l => l.RunId == runId).ToListAsync(ct);
        db.PayrollRunLines.RemoveRange(lines);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRunLineAsync(PayrollRunLine line, CancellationToken ct)
    {
        db.PayrollRunLines.Add(line);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(List<PayrollRunLine> Items, int Total)> ListRunLinesAsync(Guid runId, CancellationToken ct)
    {
        var items = await db.PayrollRunLines
            .Include(l => l.Worker).Include(l => l.Components)
            .Where(l => l.RunId == runId).OrderBy(l => l.Worker!.EmployeeNo).ToListAsync(ct);
        return (items, items.Count);
    }

    public async Task<PayrollRunLine?> GetRunLineAsync(Guid runId, Guid lineId, CancellationToken ct)
        => await db.PayrollRunLines.Include(l => l.Worker).Include(l => l.Components)
            .FirstOrDefaultAsync(l => l.RunId == runId && l.Id == lineId, ct);

    public async Task UpdateRunLineAsync(PayrollRunLine line, CancellationToken ct)
    {
        if (db.Entry(line).State == EntityState.Detached) db.PayrollRunLines.Update(line);
        await db.SaveChangesAsync(ct);
    }

    public async Task RecalculateRunTotalsAsync(PayrollRun run, CancellationToken ct)
    {
        var lines = await db.PayrollRunLines.Where(l => l.RunId == run.Id && !l.IsExcluded).ToListAsync(ct);
        run.EmployeeCount = lines.Count;
        run.TotalGross = lines.Sum(l => l.GrossPay);
        run.TotalDeductions = lines.Sum(l => l.TotalDeductions);
        run.TotalNet = lines.Sum(l => l.NetPay);
        run.TotalEmployerCost = lines.Sum(l => l.GrossPay + l.EmployerCost);
        run.ExceptionCount = lines.Count(l => l.HasException && l.ExceptionStatus == "open");
        await UpdateRunAsync(run, ct);
    }

    public async Task<List<PayrollPaymentRow>> ListPaymentRowsAsync(Guid runId, CancellationToken ct)
    {
        var lines = await db.PayrollRunLines.AsNoTracking().Include(l => l.Worker).ThenInclude(w => w!.BankDetails)
            .Where(l => l.RunId == runId && !l.IsExcluded).OrderBy(l => l.Worker!.EmployeeNo).ToListAsync(ct);
        return lines.Select(l =>
        {
            var bank = l.Worker?.BankDetails.FirstOrDefault(b => b.IsPrimary);
            return new PayrollPaymentRow(l.WorkerId, l.Worker?.EmployeeNo ?? "", l.Worker?.FullName ?? "",
                bank?.BankName ?? "", bank?.BranchCode ?? "", bank?.AccountName ?? "", bank?.AccountNumber ?? "", l.NetPay);
        }).ToList();
    }

    public async Task AddRunEventAsync(PayrollRunEvent item, CancellationToken ct)
    {
        db.PayrollRunEvents.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PayrollRunEvent>> ListRunEventsAsync(Guid runId, CancellationToken ct)
        => (await db.PayrollRunEvents.AsNoTracking().Where(e => e.RunId == runId).ToListAsync(ct))
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();

    public async Task<List<PayslipNotificationTarget>> FinalizePayslipsAsync(Guid runId, CancellationToken ct)
    {
        var run = await db.PayrollRuns.Include(r => r.PayPeriod)
            .FirstAsync(r => r.Id == runId, ct);
        var lines = await db.PayrollRunLines.Include(l => l.Worker)
            .Where(l => l.RunId == runId && !l.IsExcluded).ToListAsync(ct);

        // YTD accumulation: released (non-reversed) runs in the same pay group
        // and tax year, ordered chronologically by period label.
        var taxYear = run.PayPeriod?.StartDate.Year.ToString() ?? DateTime.UtcNow.Year.ToString();
        var periodStartYear = run.PayPeriod?.StartDate.Year ?? DateTime.UtcNow.Year;
        var prior = await (from r in db.PayrollRuns
                           join p in db.PayPeriods on r.PayPeriodId equals p.Id
                           where r.PayGroupId == run.PayGroupId
                              && r.PayGroupId == p.PayGroupId
                              && r.Status == "released" && !r.IsReversal && r.Id != run.Id
                              && p.StartDate.Year == periodStartYear
                           select new { Run = r, Period = p }).ToListAsync(ct);
        prior = prior.OrderBy(x => x.Period.StartDate).ThenBy(x => x.Run.CreatedAt).ToList();

        // If this run is a replacement for a reversal, chain each replacement
        // payslip to the original slip it supersedes.
        Func<PayrollRunLine, Guid?> originalSlipFor = _ => null;
        var originalSlips = new Dictionary<Guid, Guid>();
        if (run.IsReversal && run.ReversesRunId is not null)
        {
            var originals = await db.Payslips
                .Join(db.PayrollRunLines, s => s.RunLineId, l => l.Id, (s, l) => new { Slip = s, Line = l })
                .Where(x => x.Line.RunId == run.ReversesRunId)
                .Where(x => x.Slip.Status == "superseded")
                .Select(x => new { x.Slip.Id, x.Line.WorkerId }).ToListAsync(ct);
            foreach (var o in originals)
                originalSlips[o.WorkerId] = o.Id;
            originalSlipFor = ln => originalSlips.TryGetValue(ln.WorkerId, out var oid) ? oid : null;
        }

        // Sequence the payslip number past any existing slips for the same
        // worker's monthly prefix so the unique payslip_no index never collides.
        var prefix = $"PSL-{DateTime.UtcNow:yyyyMM}";
        var maxSeq = (await db.Payslips
            .Where(s => s.PayslipNo.StartsWith(prefix))
            .Select(s => (string?)s.PayslipNo)
            .ToListAsync(ct))
            .Select(n => n!.Substring(prefix.Length + 1))
            .Select(part => int.TryParse(part.Split('-').Last(), out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        int idx = maxSeq;
        var notifications = new List<PayslipNotificationTarget>();
        foreach (var line in lines)
        {
            idx++;
            var ytdGross = prior.Sum(x => x.Run.TotalGross) + line.GrossPay;
            var ytdNet = prior.Sum(x => x.Run.TotalNet) + line.NetPay;
            var ytdTax = prior.Sum(x => x.Run.TotalDeductions) + line.TotalDeductions;
            var slip = new Payslip
            {
                RunLineId = line.Id,
                WorkerId = line.WorkerId,
                SupersedesId = originalSlipFor(line),
                PayslipNo = $"PSL-{DateTime.UtcNow:yyyyMM}-{line.Worker?.EmployeeNo ?? "???"}-{idx:D3}",
                Version = 1,
                GrossPay = line.GrossPay,
                TotalDeductions = line.TotalDeductions,
                NetPay = line.NetPay,
                YtdGross = ytdGross.ToString("F2"),
                YtdTax = ytdTax.ToString("F2"),
                YtdNet = ytdNet.ToString("F2"),
                Status = "final",
                ReleasedAt = DateTimeOffset.UtcNow,
                // M24: snapshot the worker's statutory identity pack at payment
                // time — the payslip keeps these values even if the worker
                // record is updated later.
                WorkerNrc = line.Worker?.Nrc,
                WorkerTpin = line.Worker?.Tpin,
                WorkerNapsaNumber = line.Worker?.NapsaNumber,
                WorkerNhimaNumber = line.Worker?.NhimaNumber,
            };
            db.Payslips.Add(slip);
            notifications.Add(new PayslipNotificationTarget(
                slip.Id,
                slip.PayslipNo,
                run.PayPeriod?.PeriodLabel ?? "",
                line.WorkerId,
                line.Worker?.SubjectId,
                line.Worker?.Email,
                line.Worker?.FirstName ?? "",
                line.Worker?.LastName ?? ""));
        }
        await db.SaveChangesAsync(ct);
        return notifications;
    }

    /// <summary>On release of a reversal run, supersede the original run's
    /// payslips (supersedes idempotently: only payslips still final get voided).</summary>
    public async Task<int> SupersedeOriginalPayslipsAsync(Guid originalRunId, CancellationToken ct)
    {
        var originals = await db.Payslips
            .Join(db.PayrollRunLines, s => s.RunLineId, l => l.Id, (s, l) => new { Slip = s, Line = l })
            .Where(x => x.Line.RunId == originalRunId)
            .Where(x => x.Slip.Status == "final")
            .Select(x => x.Slip).ToListAsync(ct);
        int count = 0;
        foreach (var slip in originals)
        {
            slip.Status = "superseded";
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<(List<Payslip> Items, int Total)> ListPayslipsAsync(Guid workerId, CancellationToken ct)
    {
        var items = (await db.Payslips.Where(p => p.WorkerId == workerId).ToListAsync(ct))
            .OrderByDescending(p => p.ReleasedAt).ToList();
        return (items, items.Count);
    }

    public async Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken ct)
        => await db.Payslips.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PayrollRun?> FindRunByReversesIdAsync(Guid reversesRunId, CancellationToken ct)
        => await db.PayrollRuns.FirstOrDefaultAsync(r => r.ReversesRunId == reversesRunId && r.IsReversal, ct);

    public async Task<List<PayrollRunLine>> ListReleasedRunLinesForPeriodAsync(Guid payPeriodId, CancellationToken ct)
    {
        // Statutory aggregates across all released, non-reversed runs in the period.
        var runIds = await db.PayrollRuns
            .Where(r => r.PayPeriodId == payPeriodId && r.Status == "released" && !r.IsReversal)
            .Select(r => r.Id).ToListAsync(ct);
        if (runIds.Count == 0) return [];
        return await db.PayrollRunLines.Include(l => l.Components).Include(l => l.Worker)
            .Include(l => l.Run).ThenInclude(r => r!.PayPeriod)
            .Where(l => runIds.Contains(l.RunId)).ToListAsync(ct);
    }

    public async Task<PayrollRunLine?> GetRunLineForPayslipAsync(Guid payslipId, CancellationToken ct)
    {
        var slip = await db.Payslips.FirstOrDefaultAsync(s => s.Id == payslipId, ct);
        if (slip is null) return null;
        return await db.PayrollRunLines.Include(l => l.Components)
            .FirstOrDefaultAsync(l => l.Id == slip.RunLineId, ct);
    }

    public async Task UpdatePayslipAsync(Payslip payslip, CancellationToken ct)
    {
        db.Payslips.Update(payslip);
        await db.SaveChangesAsync(ct);
    }
}

// ===================== Config / extras =====================
public sealed class ConfigRepository(HrmDbContext db) : IConfigRepository
{
    public async Task<List<LegalEntity>> ListLegalEntitiesAsync(CancellationToken ct) => await db.LegalEntities.ToListAsync(ct);
    public async Task<List<WorkLocation>> ListLocationsAsync(CancellationToken ct) => await db.WorkLocations.ToListAsync(ct);
    public async Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => await db.OrgUnits.ToListAsync(ct);
    public async Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct) => await db.WorkCalendars.Include(c => c.Holidays).ToListAsync(ct);
    public async Task<List<LeaveType>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct)
        => await db.LeaveTypes.Where(t => includeInactive || t.IsActive).ToListAsync(ct);
    public async Task<List<CapabilityConfig>> ListCapabilitiesAsync(CancellationToken ct) => await db.CapabilityConfigs.ToListAsync(ct);
    public async Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct) => await db.PayGroups.ToListAsync(ct);
    public async Task<List<Worker>> ListAllWorkersAsync(string? status, CancellationToken ct)
    {
        var q = db.Workers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(w => w.Status == status);
        return await q.Include(w => w.OrgUnit).ToListAsync(ct);
    }
    public async Task<List<LeaveRequest>> ListLeaveRequestsAllAsync(string? status, CancellationToken ct)
    {
        var q = db.LeaveRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(l => l.Status == status);
        return await q.ToListAsync(ct);
    }
    public async Task<List<PayrollRunLine>> ListRunLinesAllAsync(string periodFrom, string periodTo, CancellationToken ct)
        => await db.PayrollRunLines.Include(l => l.Run).ThenInclude(r => r!.PayPeriod)
            .Where(l => l.Run != null && l.Run.PayPeriod != null
                && (string.IsNullOrWhiteSpace(periodFrom) || l.Run.PayPeriod.StartDate >= DateOnly.Parse(periodFrom))
                && (string.IsNullOrWhiteSpace(periodTo) || l.Run.PayPeriod.EndDate <= DateOnly.Parse(periodTo)))
            .ToListAsync(ct);

    // ---- M1 CRUD ----
    public async Task<LegalEntity?> GetLegalEntityAsync(Guid id, CancellationToken ct) => await db.LegalEntities.FirstOrDefaultAsync(e => e.Id == id, ct);
    public async Task<LegalEntity> CreateLegalEntityAsync(LegalEntity entity, CancellationToken ct)
    { db.LegalEntities.Add(entity); await db.SaveChangesAsync(ct); return entity; }
    public async Task<LegalEntity> UpdateLegalEntityAsync(LegalEntity entity, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return entity; }
    public async Task<WorkLocation?> GetLocationAsync(Guid id, CancellationToken ct) => await db.WorkLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
    public async Task<WorkLocation> CreateLocationAsync(WorkLocation location, CancellationToken ct)
    { db.WorkLocations.Add(location); await db.SaveChangesAsync(ct); return location; }
    public async Task<WorkLocation> UpdateLocationAsync(WorkLocation location, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return location; }
    public async Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
    public async Task<OrgUnit> CreateOrgUnitAsync(OrgUnit unit, CancellationToken ct)
    { db.OrgUnits.Add(unit); await db.SaveChangesAsync(ct); return unit; }
    public async Task<OrgUnit> UpdateOrgUnitAsync(OrgUnit unit, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return unit; }
    public async Task<WorkCalendar> CreateCalendarAsync(WorkCalendar calendar, CancellationToken ct)
    { db.WorkCalendars.Add(calendar); await db.SaveChangesAsync(ct); return calendar; }
    public async Task<WorkCalendar> UpdateCalendarAsync(WorkCalendar calendar, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return calendar; }
    public async Task<PublicHoliday> CreateHolidayAsync(PublicHoliday holiday, CancellationToken ct)
    { db.PublicHolidays.Add(holiday); await db.SaveChangesAsync(ct); return holiday; }
    public async Task<PublicHoliday?> GetHolidayAsync(Guid id, CancellationToken ct) => await db.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id, ct);
    public async Task<PublicHoliday> UpdateHolidayAsync(PublicHoliday holiday, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return holiday; }
    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct)
    {
        var holiday = await db.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (holiday is not null) { db.PublicHolidays.Remove(holiday); await db.SaveChangesAsync(ct); }
    }
    public async Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
    public async Task<LeaveType> CreateLeaveTypeAsync(LeaveType leaveType, CancellationToken ct)
    { db.LeaveTypes.Add(leaveType); await db.SaveChangesAsync(ct); return leaveType; }
    public async Task<LeaveType> UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return leaveType; }
    public async Task<CapabilityConfig> UpdateCapabilityAsync(CapabilityConfig capability, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return capability; }
}

public sealed class RecruitmentRepository(HrmDbContext db) : IRecruitmentRepository
{
    public async Task<(List<Vacancy> Items, int Total)> ListVacanciesAsync(string? status, CancellationToken ct)
    {
        var q = db.Vacancies.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(v => v.Status == status);
        var items = await q.Include(v => v.OrgUnit).ToListAsync(ct);
        return (items, items.Count);
    }
    public async Task<Vacancy> CreateVacancyAsync(Vacancy vacancy, CancellationToken ct)
    {
        db.Set<Vacancy>().Add(vacancy);
        await db.SaveChangesAsync(ct);
        return vacancy;
    }
    public async Task<Vacancy?> GetVacancyAsync(Guid id, CancellationToken ct)
        => await db.Vacancies.Include(v => v.OrgUnit).FirstOrDefaultAsync(v => v.Id == id, ct);
    public async Task<Vacancy> UpdateVacancyAsync(Vacancy vacancy, CancellationToken ct)
    {
        db.Set<Vacancy>().Update(vacancy);
        await db.SaveChangesAsync(ct);
        return vacancy;
    }
    public async Task<(List<Candidate> Items, int Total)> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct)
    {
        var q = db.Candidates.Where(c => c.VacancyId == vacancyId);
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(c => c.Stage == stage);
        var items = await q.ToListAsync(ct);
        return (items, items.Count);
    }
    public async Task<Candidate> CreateCandidateAsync(Candidate candidate, CancellationToken ct)
    {
        // Candidates are either freshly created or re-saved after stage advances
        // (tracked by GetCandidateAsync navigation load), so use the entry state.
        var entry = db.Entry(candidate);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            db.Set<Candidate>().Add(candidate);
        else
            db.Set<Candidate>().Update(candidate);
        await db.SaveChangesAsync(ct);
        return candidate;
    }
    public async Task<Candidate?> GetCandidateAsync(Guid id, CancellationToken ct)
        => await db.Candidates.Include(c => c.Vacancy).FirstOrDefaultAsync(c => c.Id == id, ct);
    public async Task<List<CandidateStageEvent>> ListStageEventsAsync(Guid candidateId, CancellationToken ct)
        => (await db.CandidateStageEvents.Where(x => x.CandidateId == candidateId).ToListAsync(ct)).OrderBy(x => x.CreatedAt).ToList();
    public async Task<CandidateStageEvent> CreateStageEventAsync(CandidateStageEvent entry, CancellationToken ct)
    { db.CandidateStageEvents.Add(entry); await db.SaveChangesAsync(ct); return entry; }
    public async Task<List<CandidateInterview>> ListInterviewsAsync(Guid candidateId, CancellationToken ct)
        => await db.CandidateInterviews.Where(x => x.CandidateId == candidateId).OrderBy(x => x.ScheduledAt).ToListAsync(ct);
    public async Task<CandidateInterview> CreateInterviewAsync(CandidateInterview interview, CancellationToken ct)
    { db.CandidateInterviews.Add(interview); await db.SaveChangesAsync(ct); return interview; }
    public async Task<CandidateInterview?> GetInterviewAsync(Guid id, CancellationToken ct)
        => await db.CandidateInterviews.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<CandidateInterview> UpdateInterviewAsync(CandidateInterview interview, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return interview; }
    public async Task<Offer> CreateOfferAsync(Offer offer, CancellationToken ct)
    {
        db.Set<Offer>().Add(offer);
        await db.SaveChangesAsync(ct);
        return offer;
    }
    public async Task<Offer?> GetOfferAsync(Guid id, CancellationToken ct)
        => await db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
    public async Task<Offer> UpdateOfferAsync(Offer offer, CancellationToken ct)
    {
        db.Set<Offer>().Update(offer);
        await db.SaveChangesAsync(ct);
        return offer;
    }
    public async Task<(List<Offer> Items, int Total)> ListOffersAsync(string? status, CancellationToken ct)
    {
        var q = db.Offers.Include(x => x.Candidate)!.ThenInclude(x => x!.Vacancy).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var items = (await q.ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<PreboardingCase?> GetPreboardingAsync(Guid id, CancellationToken ct)
        => await db.PreboardingCases.Include(x => x.Tasks).Include(x => x.Candidate).Include(x => x.Worker).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PreboardingCase?> GetPreboardingForCandidateAsync(Guid candidateId, CancellationToken ct)
        => await db.PreboardingCases.Include(x => x.Tasks).Include(x => x.Candidate).Include(x => x.Worker).FirstOrDefaultAsync(x => x.CandidateId == candidateId, ct);
    public async Task<(List<PreboardingCase> Items, int Total)> ListPreboardingAsync(string? status, CancellationToken ct)
    {
        var q = db.PreboardingCases.Include(x => x.Tasks).Include(x => x.Candidate).Include(x => x.Worker).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var items = (await q.ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<PreboardingCase> CreatePreboardingAsync(PreboardingCase record, CancellationToken ct)
    { db.PreboardingCases.Add(record); await db.SaveChangesAsync(ct); return record; }
    public async Task<PreboardingCase> UpdatePreboardingAsync(PreboardingCase record, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return record; }
    public async Task<PreboardingTask?> GetPreboardingTaskAsync(Guid id, CancellationToken ct)
        => await db.PreboardingTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PreboardingTask> CreatePreboardingTaskAsync(PreboardingTask task, CancellationToken ct)
    { db.PreboardingTasks.Add(task); await db.SaveChangesAsync(ct); return task; }
    public async Task<PreboardingTask> UpdatePreboardingTaskAsync(PreboardingTask task, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return task; }
    public async Task<List<CandidateDocument>> ListCandidateDocumentsAsync(Guid candidateId, CancellationToken ct)
        => (await db.CandidateDocuments.Where(x => x.CandidateId == candidateId).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
    public async Task<CandidateDocument> CreateCandidateDocumentAsync(CandidateDocument document, CancellationToken ct)
    { db.CandidateDocuments.Add(document); await db.SaveChangesAsync(ct); return document; }
    public async Task<CandidateDocument?> GetCandidateDocumentAsync(Guid id, CancellationToken ct)
        => await db.CandidateDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<int> CountCandidatesForVacancyAsync(Guid vacancyId, CancellationToken ct)
        => await db.Candidates.CountAsync(c => c.VacancyId == vacancyId, ct);
}

public sealed class RelationsRepository(HrmDbContext db) : IRelationsRepository
{
    public async Task<(List<RelationsCase> Items, int Total)> ListCasesAsync(string? category, CancellationToken ct)
    {
        var q = db.RelationsCases.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(c => c.Category == category);
        var items = await q.Include(c => c.SubjectWorker).ToListAsync(ct);
        return (items, items.Count);
    }
    public async Task<RelationsCase> CreateCaseAsync(RelationsCase caseRecord, CancellationToken ct)
    {
        db.Set<RelationsCase>().Add(caseRecord);
        await db.SaveChangesAsync(ct);
        return caseRecord;
    }
    public async Task<RelationsCase?> GetCaseAsync(Guid id, CancellationToken ct)
        => await db.RelationsCases.FirstOrDefaultAsync(c => c.Id == id, ct);
    public async Task<RelationsCase> UpdateCaseAsync(RelationsCase caseRecord, CancellationToken ct)
    {
        db.Set<RelationsCase>().Update(caseRecord);
        await db.SaveChangesAsync(ct);
        return caseRecord;
    }
}

public sealed class DocumentsRepository(HrmDbContext db) : IDocumentsRepository
{
    public async Task<(List<WorkerDocument> Items, int Total)> ListDocumentsAsync(Guid workerId, CancellationToken ct)
    {
        var items = (await db.WorkerDocuments.Where(d => d.WorkerId == workerId).ToListAsync(ct))
            .OrderByDescending(d => d.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<WorkerDocument> CreateDocumentAsync(WorkerDocument document, CancellationToken ct)
    {
        db.WorkerDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return document;
    }
    public async Task<WorkerDocument?> GetDocumentAsync(Guid id, CancellationToken ct) =>
        await db.WorkerDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
    public async Task<List<WorkerDocument>> ListAllDocumentsAsync(CancellationToken ct) =>
        await db.WorkerDocuments.ToListAsync(ct);
}
