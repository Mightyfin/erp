using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class IntegrationOperationsService(
    HrmDbContext db,
    IAuthzService authz,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : IIntegrationOperationsService
{
    private static readonly IntegrationContractDto[] Contracts =
    [
        new("finance", "ERP finance journal", "outbound", "1.0", "NATS/JetStream",
            "Finance operations", "Exponential retry for failed hand-offs; idempotency key prevents duplicate journals.",
            "Finance returns its journal reference; debit and credit control totals must match before reconciliation.", "available", null),
        new("payments", "Bank payment instruction", "outbound", "1.0", "NATS/JetStream + provider file",
            "Treasury / payroll", "Failed hand-offs are manually reviewed then retried with the original idempotency key.",
            "Treasury records the provider batch reference and matches paid amount to payroll net pay.", "available", null),
        new("zra", "ZRA PAYE return", "outbound", "1.0", "CSV hand-off",
            "Payroll compliance", "Regenerate only after correcting source payroll; identical periods remain idempotent.",
            "Record the ZRA filing receipt and compare accepted PAYE to the export control total.", "available", null),
        new("napsa", "NAPSA contribution return", "outbound", "1.0", "CSV hand-off",
            "Payroll compliance", "Regenerate only after correcting source payroll; identical periods remain idempotent.",
            "Record the NAPSA receipt and compare employee/employer totals.", "available", null),
        new("nhima", "NHIMA contribution return", "outbound", "1.0", "CSV hand-off",
            "Payroll compliance", "Regenerate only after correcting source payroll; identical periods remain idempotent.",
            "Record the NHIMA receipt and compare employee/employer totals.", "available", null),
        new("documents", "HR document storage", "outbound", "1.0", "Storage provider adapter",
            "HR systems administration", "Uploads are retried by the caller; metadata is retained when storage fails.",
            "Scheduled storage checks verify that retained object references remain readable.", "available", null),
        new("identity", "Workforce identity synchronisation", "bidirectional", "1.0", "NATS/JetStream",
            "Identity administration", "Delta sync can be replayed safely; worker subject links are unique per tenant.",
            "Review linked/unlinked worker counts and resolve conflicts in the IdP before replay.", "available", null),
    ];

    public async Task<IntegrationDashboardDto> GetDashboardAsync(string? integrationKey, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var query = db.Set<IntegrationOperation>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(integrationKey)) query = query.Where(x => x.IntegrationKey == integrationKey.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim().ToLowerInvariant());
        var operations = await query.OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        var all = db.Set<IntegrationOperation>().AsNoTracking();
        var activeWorkers = await db.Workers.CountAsync(x => !x.IsArchived && x.Status == "active", ct);
        var linkedWorkers = await db.Workers.CountAsync(x => !x.IsArchived && x.Status == "active" && x.SubjectId != null, ct);
        var storageMode = configuration["HRM:DocumentStorage:Provider"] ?? "local-filesystem";
        return new IntegrationDashboardDto(
            Contracts.Select(c => c.Key == "documents" ? c with { Detail = $"Current adapter: {storageMode}" } : c).ToList(),
            operations.Select(Map).ToList(),
            await all.CountAsync(x => x.Status == "ready", ct),
            await all.CountAsync(x => x.Status == "delivered", ct),
            await all.CountAsync(x => x.Status == "failed" || x.Status == "rejected", ct),
            await all.CountAsync(x => x.Status == "reconciled", ct),
            activeWorkers, linkedWorkers, activeWorkers - linkedWorkers, storageMode);
    }

    public async Task<IntegrationOperationDto> CreateFinancePostingAsync(Guid runId, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await db.PayrollRuns.Include(x => x.PayPeriod).ThenInclude(x => x!.PayGroup)
            .FirstOrDefaultAsync(x => x.Id == runId, ct)
            ?? throw new DomainException("payroll-run-not-found", $"Payroll run {runId} does not exist.");
        if (run.Status != "released" && run.Status != "closed")
            throw new DomainException("integration-source-not-ready", "Only a released payroll run can be posted to finance.");
        var employerContributions = run.TotalEmployerCost - run.TotalGross;
        var runLines = await db.PayrollRunLines.AsNoTracking()
            .Where(x => x.RunId == run.Id && !x.IsExcluded)
            .Include(x => x.Worker).ThenInclude(x => x!.OrgUnit)
            .Include(x => x.Components)
            .ToListAsync(ct);
        var journalDetails = runLines.SelectMany(line => new[]
        {
            new { employeeNo = line.Worker?.EmployeeNo, costCentre = line.Worker?.OrgUnit?.Code,
                department = line.Worker?.OrgUnit?.Name, account = "PAYROLL_EXPENSE", debit = line.GrossPay, credit = 0m },
            new { employeeNo = line.Worker?.EmployeeNo, costCentre = line.Worker?.OrgUnit?.Code,
                department = line.Worker?.OrgUnit?.Name, account = "PAYROLL_EMPLOYER_EXPENSE", debit = line.EmployerCost - line.GrossPay, credit = 0m },
            new { employeeNo = line.Worker?.EmployeeNo, costCentre = line.Worker?.OrgUnit?.Code,
                department = line.Worker?.OrgUnit?.Name, account = "PAYROLL_NET_PAYABLE", debit = 0m, credit = line.NetPay },
            new { employeeNo = line.Worker?.EmployeeNo, costCentre = line.Worker?.OrgUnit?.Code,
                department = line.Worker?.OrgUnit?.Name, account = "PAYROLL_DEDUCTIONS_PAYABLE", debit = 0m, credit = line.TotalDeductions },
            new { employeeNo = line.Worker?.EmployeeNo, costCentre = line.Worker?.OrgUnit?.Code,
                department = line.Worker?.OrgUnit?.Name, account = "PAYROLL_EMPLOYER_CONTRIBUTIONS", debit = 0m, credit = line.EmployerCost - line.GrossPay },
        }).ToList();
        var componentSummary = runLines.SelectMany(x => x.Components)
            .GroupBy(x => new { x.ComponentCode, x.ComponentName, x.ComponentType })
            .Select(g => new { g.Key.ComponentCode, g.Key.ComponentName, g.Key.ComponentType, amount = g.Sum(x => x.Amount) })
            .OrderBy(x => x.ComponentCode).ToList();
        var payload = new
        {
            schema = "mightyfin.hrm.finance-journal.v1", payrollRunId = run.Id,
            period = run.PayPeriod?.PeriodLabel, currency = run.PayPeriod?.PayGroup?.Currency ?? "ZMW",
            postingDate = run.PayPeriod?.PayDate.ToString("yyyy-MM-dd"),
            journal = new[]
            {
                new { account = "PAYROLL_EXPENSE", debit = run.TotalEmployerCost, credit = 0m },
                new { account = "PAYROLL_NET_PAYABLE", debit = 0m, credit = run.TotalNet },
                new { account = "PAYROLL_DEDUCTIONS_PAYABLE", debit = 0m, credit = run.TotalDeductions },
                new { account = "PAYROLL_EMPLOYER_CONTRIBUTIONS", debit = 0m, credit = employerContributions },
            },
            journalDetails,
            componentSummary,
            controlTotals = new { debit = run.TotalEmployerCost, credit = run.TotalNet + run.TotalDeductions + employerContributions }
        };
        return await CreateAsync("finance", "payroll-journal", run.Id, run.PayPeriod?.PeriodLabel,
            $"finance:payroll:{run.Id:N}:v1", JsonSerializer.Serialize(payload), "application/json", actor, ct);
    }

    public async Task<IntegrationOperationDto> CreatePaymentHandoffAsync(Guid runId, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await db.PayrollRuns.Include(x => x.PayPeriod)
            .FirstOrDefaultAsync(x => x.Id == runId, ct)
            ?? throw new DomainException("payroll-run-not-found", $"Payroll run {runId} does not exist.");
        if (run.Status != "released" && run.Status != "closed")
            throw new DomainException("integration-source-not-ready", "Payroll must be released before a payment hand-off is created.");
        if (run.PaymentStatus != "released" && run.PaymentStatus != "reconciled")
            throw new DomainException("integration-source-not-ready", "The payment file must be approved and released first.");
        var rows = await db.PayrollRunLines.AsNoTracking().Where(x => x.RunId == runId && !x.IsExcluded)
            .Join(db.Workers.AsNoTracking(), l => l.WorkerId, w => w.Id, (l, w) => new { l, w })
            .GroupJoin(db.WorkerBankDetails.AsNoTracking().Where(x => x.IsPrimary), x => x.w.Id, b => b.WorkerId,
                (x, banks) => new { x.l, x.w, Bank = banks.FirstOrDefault() }).ToListAsync(ct);
        var missing = rows.Where(x => x.Bank is null).Select(x => x.w.EmployeeNo).ToList();
        if (missing.Count > 0)
            throw new DomainException("payment-bank-details-missing", $"Primary bank details are missing for: {string.Join(", ", missing.Take(10))}.");
        var csv = new StringBuilder("employee_no,account_name,bank,branch_code,account_number,amount,currency\n");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',', Csv(row.w.EmployeeNo), Csv(row.Bank!.AccountName), Csv(row.Bank.BankName), Csv(row.Bank.BranchCode), Csv(row.Bank.AccountNumber), row.l.NetPay.ToString("0.00", CultureInfo.InvariantCulture), "ZMW"));
        return await CreateAsync("payments", "bank-payment-batch", run.Id, run.PaymentFileReference ?? run.PayPeriod?.PeriodLabel,
            $"payments:payroll:{run.Id:N}:{run.PaymentFileReference ?? "v1"}", csv.ToString(), "text/csv", actor, ct);
    }

    public async Task<IntegrationOperationDto> CreateStatutoryHandoffAsync(StatutoryHandoffRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var kind = request.ExportType.Trim().ToLowerInvariant();
        if (kind is not ("zra" or "napsa" or "nhima"))
            throw new DomainException("integration-type-invalid", "Export type must be zra, napsa, or nhima.");
        var period = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", $"Pay period {request.PeriodId} does not exist.");
        var runIds = await db.PayrollRuns.AsNoTracking()
            .Where(x => x.PayPeriodId == request.PeriodId && (x.Status == "released" || x.Status == "closed"))
            .Select(x => x.Id).ToListAsync(ct);
        if (runIds.Count == 0) throw new DomainException("integration-source-not-ready", "The period has no released payroll run.");
        var runLines = await db.PayrollRunLines.AsNoTracking().Where(x => runIds.Contains(x.RunId) && !x.IsExcluded)
            .Include(x => x.Worker).Include(x => x.Components).ToListAsync(ct);
        var employer = await db.LegalEntities.AsNoTracking().OrderByDescending(x => x.IsDefault).FirstOrDefaultAsync(ct);
        var employerReference = kind switch
        {
            "zra" => employer?.Tpin,
            "napsa" => employer?.NapsaEmployerRef,
            "nhima" => employer?.NhimaEmployerRef,
            _ => null,
        };
        var csv = new StringBuilder("scheme,employer_reference,period,employee_no,last_name,first_name,nrc,tpin,napsa_number,nhima_number,gross_pay,employee_contribution,employer_contribution,total,currency\n");
        foreach (var line in runLines.OrderBy(x => x.Worker!.EmployeeNo))
        {
            var relevant = line.Components.Where(x => x.IsStatutory && MatchesScheme(kind, x.ComponentCode)).ToList();
            var employeeAmount = relevant.Where(x => x.ComponentType is "deduction" or "tax" || x.ComponentCode.Contains("ee", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var employerAmount = relevant.Where(x => x.ComponentType == "employer-contribution" || x.ComponentCode.Contains("er", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var worker = line.Worker!;
            csv.AppendLine(string.Join(',', kind.ToUpperInvariant(), Csv(employerReference), Csv(period.PeriodLabel), Csv(worker.EmployeeNo),
                Csv(worker.LastName), Csv(worker.FirstName), Csv(worker.Nrc), Csv(worker.Tpin), Csv(worker.NapsaNumber), Csv(worker.NhimaNumber),
                line.GrossPay.ToString("0.00", CultureInfo.InvariantCulture), employeeAmount.ToString("0.00", CultureInfo.InvariantCulture),
                employerAmount.ToString("0.00", CultureInfo.InvariantCulture), (employeeAmount + employerAmount).ToString("0.00", CultureInfo.InvariantCulture), "ZMW"));
        }
        return await CreateAsync(kind, "statutory-return", period.Id, period.PeriodLabel,
            $"statutory:{kind}:{period.Id:N}:v1", csv.ToString(), "text/csv", actor, ct);
    }

    public async Task<IntegrationOperationDto> CreateIdentitySyncAsync(IdentitySyncRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var mode = request.Mode.Trim().ToLowerInvariant();
        if (mode is not ("delta" or "full")) throw new DomainException("integration-mode-invalid", "Identity sync mode must be delta or full.");
        var workers = await db.Workers.AsNoTracking().Where(x => !x.IsArchived && x.Status == "active")
            .OrderBy(x => x.EmployeeNo).Select(x => new { x.Id, x.EmployeeNo, x.Email, x.SubjectId, x.Status }).ToListAsync(ct);
        var payload = JsonSerializer.Serialize(new
        {
            schema = "mightyfin.hrm.workforce-identity.v1", mode,
            generatedAt = DateTimeOffset.UtcNow,
            workers = workers.Select(x => new { workerId = x.Id, x.EmployeeNo, x.Email, x.SubjectId, x.Status })
        });
        var keyDate = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        return await CreateAsync("identity", "workforce-sync", null, mode,
            $"identity:{mode}:{keyDate}", payload, "application/json", actor, ct);
    }

    public async Task<IntegrationOperationDto> RetryAsync(Guid id, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var row = await RequireAsync(id, ct);
        if (row.Status is not ("failed" or "rejected"))
            throw new DomainException("integration-not-retryable", "Only failed or rejected operations can be retried.");
        row.Status = "ready"; row.AttemptCount++; row.LastAttemptAt = DateTimeOffset.UtcNow;
        row.NextAttemptAt = null; row.LastError = null; row.UpdatedAt = DateTimeOffset.UtcNow; row.UpdatedBy = actor;
        await unitOfWork.ExecuteAsync(async innerCt =>
        {
            await db.SaveChangesAsync(innerCt);
            await outbox.EnqueueAsync(HrmEventTypes.IntegrationReady, "integration-system",
                new { operationId = row.PublicId, row.IntegrationKey, row.OperationType, row.ContractVersion, row.IdempotencyKey, requestedBySubjectId = actor }, innerCt);
        }, ct);
        return Map(row);
    }

    public async Task<IntegrationOperationDto> ReconcileAsync(Guid id, IntegrationReconciliationRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var outcome = request.Outcome.Trim().ToLowerInvariant();
        if (outcome is not ("accepted" or "matched" or "failed" or "rejected"))
            throw new DomainException("integration-outcome-invalid", "Outcome must be accepted, matched, failed, or rejected.");
        if (string.IsNullOrWhiteSpace(request.ExternalReference))
            throw new DomainException("integration-reference-required", "The external system reference is required.");
        var row = await RequireAsync(id, ct);
        row.Status = outcome switch { "accepted" => "delivered", "matched" => "reconciled", _ => outcome };
        row.ExternalReference = request.ExternalReference.Trim(); row.ReconciliationOutcome = outcome;
        row.ReconciliationNote = request.Note?.Trim(); row.ReconciledAt = DateTimeOffset.UtcNow;
        row.ReconciledBySubjectId = actor; row.UpdatedAt = DateTimeOffset.UtcNow; row.UpdatedBy = actor;
        row.LastError = outcome is "failed" or "rejected" ? request.Note?.Trim() ?? $"External system reported {outcome}." : null;
        if (row.Status == "failed") row.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<(string Payload, string ContentType, string FileName)> DownloadAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var row = await RequireAsync(id, ct);
        var extension = row.ContentType == "text/csv" ? "csv" : "json";
        return (row.PayloadJson, row.ContentType, $"{row.IntegrationKey}-{row.PublicId}.{extension}");
    }

    private async Task<IntegrationOperationDto> CreateAsync(string key, string type, Guid? sourceId, string? sourceReference,
        string idempotencyKey, string payload, string contentType, string actor, CancellationToken ct)
    {
        var existing = await db.Set<IntegrationOperation>().FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) return Map(existing);
        var row = new IntegrationOperation
        {
            PublicId = $"int_{Guid.NewGuid():N}", IntegrationKey = key, OperationType = type,
            IdempotencyKey = idempotencyKey, SourceId = sourceId, SourceReference = sourceReference,
            PayloadJson = payload, ContentType = contentType, CreatedBySubjectId = actor, CreatedBy = actor,
            Status = "ready", NextAttemptAt = DateTimeOffset.UtcNow,
        };
        await unitOfWork.ExecuteAsync(async innerCt =>
        {
            db.Set<IntegrationOperation>().Add(row);
            await db.SaveChangesAsync(innerCt);
            await outbox.EnqueueAsync(HrmEventTypes.IntegrationReady, "integration-system",
                new { operationId = row.PublicId, row.IntegrationKey, row.OperationType, row.ContractVersion, row.IdempotencyKey, requestedBySubjectId = actor }, innerCt);
        }, ct);
        return Map(row);
    }

    private async Task<IntegrationOperation> RequireAsync(Guid id, CancellationToken ct) =>
        await db.Set<IntegrationOperation>().FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("integration-operation-not-found", $"Integration operation {id} does not exist.");

    private static bool MatchesScheme(string scheme, string code)
    {
        var value = code.ToLowerInvariant();
        return scheme switch { "zra" => value.Contains("paye") || value.Contains("tax"), "napsa" => value.Contains("napsa"), "nhima" => value.Contains("nhima"), _ => false };
    }

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static IntegrationOperationDto Map(IntegrationOperation x) => new(
        x.Id, x.PublicId, x.IntegrationKey, x.OperationType, x.ContractVersion, x.IdempotencyKey,
        x.Status, x.SourceId, x.SourceReference, x.ContentType, x.AttemptCount, x.LastAttemptAt,
        x.NextAttemptAt, x.LastError, x.ExternalReference, x.ReconciliationOutcome,
        x.ReconciliationNote, x.ReconciledAt, x.CreatedBySubjectId, x.ReconciledBySubjectId, x.CreatedAt);
}
