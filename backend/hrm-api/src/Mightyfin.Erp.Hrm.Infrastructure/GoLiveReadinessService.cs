using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Computes a tenant-scoped, fail-closed go-live decision from live
/// system state, current evidence and role-specific human acceptance.</summary>
public sealed class GoLiveReadinessService(HrmDbContext db, IAuthzService authz) : IGoLiveReadinessService
{
    private static readonly (string Key, string Name)[] EvidenceGates =
    [
        ("backup-restore", "Backup and restore rehearsal"),
        ("security-test", "Security acceptance test"),
        ("migration-rehearsal", "Production migration rehearsal"),
        ("performance-test", "Performance acceptance test"),
        ("monitoring-alerts", "Monitoring and alert validation"),
        ("incident-runbook", "Incident runbook walkthrough"),
        ("rollback-rehearsal", "Rollback rehearsal"),
        ("uat-hr", "HR user acceptance testing"),
        ("uat-payroll", "Payroll user acceptance testing"),
        ("training-hr", "HR administrator training"),
        ("training-payroll", "Payroll operator training"),
    ];

    private static readonly Dictionary<string, (string Name, string[] Roles)> RequiredSignoffs = new()
    {
        ["hr-owner"] = ("HR owner", ["hr_admin"]),
        ["payroll-owner"] = ("Payroll owner", ["payroll"]),
        ["finance-owner"] = ("Finance owner", ["finance_approver"]),
        ["technical-owner"] = ("Technical owner", ["hr_admin"]),
        ["tenant-owner"] = ("Tenant executive owner", ["hr_admin"]),
    };

    public async Task<GoLiveReadinessDto> GetAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "payroll", "finance_approver");
        var now = DateTimeOffset.UtcNow;
        var gates = new List<GoLiveGateDto>();

        var pendingMigrations = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true
            ? (await db.Database.GetPendingMigrationsAsync(ct)).ToArray()
            : [];
        Add(gates, "database-migrations", "technical", "Database migrations",
            pendingMigrations.Length == 0, pendingMigrations.Length == 0
                ? "The database is reachable and the model has no pending migrations."
                : $"{pendingMigrations.Length} migration(s) are pending.", "automated:ef-migrations", now);

        var legalEntities = await db.LegalEntities.AsNoTracking().ToListAsync(ct);
        var employerReady = legalEntities.Any(x => x.IsDefault && !string.IsNullOrWhiteSpace(x.Tpin)
            && !string.IsNullOrWhiteSpace(x.NapsaEmployerRef) && !string.IsNullOrWhiteSpace(x.NhimaEmployerRef));
        Add(gates, "employer-reference-data", "reference-data", "Employer statutory references", employerReady,
            employerReady ? "A default legal entity has TPIN, NAPSA and NHIMA employer references."
                : "Configure one default legal entity with TPIN, NAPSA and NHIMA employer references.");

        var hasPayGroup = await db.PayGroups.AsNoTracking().AnyAsync(ct);
        var hasTax = await db.TaxSlabs.AsNoTracking().AnyAsync(x => x.IsActive, ct);
        var contributionCodes = await db.ContributionRules.AsNoTracking().Where(x => x.IsActive)
            .Select(x => x.Code.ToLower()).Distinct().ToListAsync(ct);
        var payrollConfigReady = hasPayGroup && hasTax && contributionCodes.Any(x => x.Contains("napsa"))
            && contributionCodes.Any(x => x.Contains("nhima"));
        Add(gates, "payroll-reference-data", "reference-data", "Payroll reference data", payrollConfigReady,
            payrollConfigReady ? "Pay groups, PAYE slabs, NAPSA and NHIMA rules are active."
                : "Pay groups, active PAYE slabs, NAPSA and NHIMA rules are all required.");

        var activeWorkers = db.Workers.AsNoTracking().Where(x => x.Status == "active" || x.Status == "on-leave" || x.Status == "notice");
        var workerCount = await activeWorkers.CountAsync(ct);
        var incompleteWorkers = await activeWorkers.CountAsync(x => string.IsNullOrWhiteSpace(x.Nrc)
            || string.IsNullOrWhiteSpace(x.Tpin) || string.IsNullOrWhiteSpace(x.NapsaNumber)
            || string.IsNullOrWhiteSpace(x.NhimaNumber), ct);
        Add(gates, "worker-statutory-data", "data-quality", "Worker statutory identity", workerCount > 0 && incompleteWorkers == 0,
            workerCount == 0 ? "No active workers exist for the production rehearsal."
                : incompleteWorkers == 0 ? $"All {workerCount} active workers have the required statutory identity pack."
                : $"{incompleteWorkers} of {workerCount} active workers are missing statutory identifiers.");

        var payrollRehearsed = await db.PayrollRuns.AsNoTracking().AnyAsync(x =>
            (x.Status == "released" || x.Status == "closed") && x.PaymentStatus == "reconciled", ct);
        Add(gates, "payroll-cycle-rehearsal", "operations", "Reconciled payroll cycle", payrollRehearsed,
            payrollRehearsed ? "A released payroll and its payment workflow have been reconciled."
                : "Complete and reconcile one end-to-end payroll rehearsal.");

        var failedOutbox = await db.OutboxMessages.AsNoTracking().CountAsync(x => x.Status == "failed", ct);
        var failedIntegrations = await db.IntegrationOperations.AsNoTracking()
            .CountAsync(x => x.Status == "failed" || x.Status == "rejected", ct);
        Add(gates, "delivery-backlog", "operations", "Delivery and integration backlog",
            failedOutbox == 0 && failedIntegrations == 0,
            failedOutbox == 0 && failedIntegrations == 0
                ? "No failed notification or external-integration operations are waiting."
                : $"Resolve {failedOutbox} failed notification(s) and {failedIntegrations} failed/rejected integration operation(s).");

        var evidenceRows = (await db.ComplianceEvidenceRecords.AsNoTracking()
            .Where(x => EvidenceGates.Select(g => g.Key).Contains(x.ControlKey))
            .ToListAsync(ct)).OrderByDescending(x => x.ExecutedAt).ToList();
        foreach (var definition in EvidenceGates)
        {
            var evidence = evidenceRows.FirstOrDefault(x => x.ControlKey == definition.Key);
            var current = evidence is not null && evidence.Status == "passed"
                && (evidence.ExpiresAt is null || evidence.ExpiresAt > now);
            gates.Add(new GoLiveGateDto(definition.Key, "evidence", definition.Name,
                current ? "passed" : "blocked", evidence is null ? "No evidence has been recorded."
                    : evidence.Status != "passed" ? "The latest evidence is marked failed."
                    : "The latest evidence has expired.", evidence?.EvidenceReference, evidence?.ExecutedAt));
        }

        var rows = (await db.GoLiveSignoffs.AsNoTracking().ToListAsync(ct))
            .OrderByDescending(x => x.SignedAt).ToList();
        var signoffs = RequiredSignoffs.Select(definition =>
        {
            var row = rows.FirstOrDefault(x => x.RoleKey == definition.Key);
            return row is null
                ? new GoLiveSignoffDto(Guid.Empty, definition.Key, definition.Value.Name, "pending", null, "", DateTimeOffset.MinValue)
                : Map(row, definition.Value.Name);
        }).ToList();

        var blockers = gates.Where(x => x.Status != "passed").Select(x => x.Name).ToList();
        if (signoffs.Any(x => x.Decision == "rejected"))
            blockers.AddRange(signoffs.Where(x => x.Decision == "rejected").Select(x => $"{x.RoleName} rejected acceptance"));
        var allApproved = signoffs.All(x => x.Decision == "approved");
        var decision = blockers.Count > 0 ? "blocked" : allApproved ? "approved" : "ready-for-signoff";
        return new GoLiveReadinessDto(decision, decision == "approved", now,
            gates.Count(x => x.Status == "passed"), gates.Count, blockers, gates, signoffs);
    }

    public async Task<ComplianceEvidenceDto> RecordEvidenceAsync(GoLiveEvidenceRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var key = request.ControlKey.Trim().ToLowerInvariant();
        if (!EvidenceGates.Any(x => x.Key == key))
            throw new DomainException("control-key-invalid", "The evidence control is not part of the M36 go-live checklist.");
        var status = request.Status.Trim().ToLowerInvariant();
        if (status is not ("passed" or "failed"))
            throw new DomainException("control-status-invalid", "Evidence status must be passed or failed.");
        if (string.IsNullOrWhiteSpace(request.EvidenceReference))
            throw new DomainException("evidence-reference-required", "An evidence reference is required.");
        if (request.ExecutedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new DomainException("evidence-date-invalid", "Evidence cannot be dated in the future.");
        var row = new ComplianceEvidence
        {
            ControlKey = key, Status = status, EvidenceReference = request.EvidenceReference.Trim(),
            Notes = request.Notes?.Trim(), ExecutedAt = request.ExecutedAt, ExpiresAt = request.ExpiresAt,
            ExecutedBySubjectId = actor,
        };
        db.ComplianceEvidenceRecords.Add(row);
        await db.SaveChangesAsync(ct);
        return new(row.Id, row.ControlKey, row.Status, row.EvidenceReference, row.Notes,
            row.ExecutedAt, row.ExpiresAt, row.ExecutedBySubjectId);
    }

    public async Task<GoLiveSignoffDto> RecordSignoffAsync(string roleKey, GoLiveSignoffRequest request, string actor, CancellationToken ct)
    {
        roleKey = roleKey.Trim().ToLowerInvariant();
        if (!RequiredSignoffs.TryGetValue(roleKey, out var definition))
            throw new DomainException("signoff-role-invalid", "The sign-off role is not recognised.");
        authz.RequireAnyRole(definition.Roles);
        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision is not ("approved" or "rejected" or "withdrawn"))
            throw new DomainException("signoff-decision-invalid", "Decision must be approved, rejected, or withdrawn.");
        if (decision == "rejected" && string.IsNullOrWhiteSpace(request.Notes))
            throw new DomainException("signoff-notes-required", "A rejection reason is required.");
        if (decision == "approved" && (await GetAsync(ct)).Blockers.Count > 0)
            throw new DomainException("go-live-not-ready", "Approval is blocked until every technical and operational gate has passed.");
        var row = new GoLiveSignoff
        {
            RoleKey = roleKey, Decision = decision, Notes = request.Notes?.Trim(),
            ActorSubjectId = actor, SignedAt = DateTimeOffset.UtcNow,
        };
        db.GoLiveSignoffs.Add(row);
        await db.SaveChangesAsync(ct);
        return Map(row, definition.Name);
    }

    private static void Add(List<GoLiveGateDto> gates, string key, string category, string name,
        bool passed, string detail, string? reference = null, DateTimeOffset? verified = null) =>
        gates.Add(new(key, category, name, passed ? "passed" : "blocked", detail, reference, verified));

    private static GoLiveSignoffDto Map(GoLiveSignoff row, string name) =>
        new(row.Id, row.RoleKey, name, row.Decision, row.Notes, row.ActorSubjectId, row.SignedAt);
}
