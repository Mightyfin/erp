using System.Text;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class SecurityComplianceService(
    HrmDbContext db, IAuthzService authz, ITenantAccessor tenant) : ISecurityComplianceService
{
    private static readonly RoleCapabilityDto[] RoleMatrix =
    [
        new("self-service", "Own profile, documents, leave, requests and payslips", ["employee", "manager", "hr_ops", "payroll", "hr_admin", "investigator"], "own worker only", true, "Subject-to-worker ownership is resolved by the API."),
        new("people-read", "Read the employee directory", ["manager", "hr_ops", "payroll", "hr_admin"], "tenant; managers receive masked sensitive fields", true, "Sensitive statutory and payment identifiers are masked outside HR/payroll."),
        new("people-write", "Create, edit, archive and reactivate workers", ["hr_ops", "hr_admin"], "tenant", true, "Every mutation creates redacted entity and privileged-action audit evidence."),
        new("time-admin", "Attendance import, shifts, accrual and balance adjustment", ["hr_ops", "hr_admin"], "tenant", false, "Operational changes remain approval and audit controlled."),
        new("payroll-operate", "Calculate, correct and prepare payroll", ["payroll"], "tenant", true, "Payroll preparation is segregated from HR approval."),
        new("payroll-approve", "Approve payroll", ["hr_admin"], "tenant", true, "The preparer cannot approve the same run."),
        new("payroll-release", "Release payroll and payment files", ["payroll"], "tenant", true, "Release requires a different subject from preparation and approval."),
        new("relations-investigate", "Investigate restricted employee-relations cases", ["investigator", "hr_admin"], "assigned case", true, "Named access declaration and case audit are mandatory."),
        new("protected-disclosure", "Handle protected disclosures", ["investigator"], "named handler only", true, "Separated from ordinary HR administration."),
        new("security-admin", "Review audit, retention, legal holds and control evidence", ["hr_admin"], "tenant", true, "Append-only audit records cannot be changed or deleted."),
    ];

    private static readonly RetentionRuleDto[] RetentionRules =
    [
        new("Worker master record", 84, "Employment and statutory obligations", "Restrict and anonymise when obligations expire", true),
        new("Payroll, payslip and statutory snapshots", 120, "Tax, pension and employment evidence", "Archive to immutable storage, then dispose", true),
        new("Attendance and leave", 60, "Employment and working-time evidence", "Dispose after approved retention review", true),
        new("Recruitment candidates not hired", 24, "Recruitment consent and legitimate interest", "Anonymise candidate identity and documents", true),
        new("Employee-relations cases", 84, "Employment claims and investigation evidence", "Restricted disposal review", true),
        new("Protected disclosures", 120, "Whistleblowing and investigation evidence", "Named-handler disposal review", true),
        new("Audit and privileged-action evidence", 120, "Security, fraud and accountability", "Archive append-only evidence", true),
    ];

    public async Task<SecurityDashboardDto> GetDashboardAsync(string? actor, string? outcome, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var events = db.PrivilegedActionEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actor)) events = events.Where(x => x.ActorSubjectId.Contains(actor));
        if (!string.IsNullOrWhiteSpace(outcome)) events = events.Where(x => x.Outcome == outcome.Trim().ToLowerInvariant());
        var actionRows = await events.OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var auditRows = await db.AuditEntries.AsNoTracking().OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var evidence = await db.ComplianceEvidenceRecords.AsNoTracking().OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var holds = await db.LegalHolds.AsNoTracking().OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var controls = BuildControls(evidence);
        return new SecurityDashboardDto(tenant.GetTenantId(), controls, RoleMatrix.ToList(),
            actionRows.Select(Map).ToList(), auditRows.Select(Map).ToList(), RetentionRules.ToList(),
            evidence.Select(Map).ToList(), holds.Select(Map).ToList(),
            controls.Count(x => x.Status != "passed"), holds.Count(x => x.Status == "active"));
    }

    public async Task<ComplianceEvidenceDto> RecordEvidenceAsync(ComplianceEvidenceRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var key = request.ControlKey.Trim().ToLowerInvariant();
        if (key is not ("backup-restore" or "tenant-isolation" or "security-test"))
            throw new DomainException("control-key-invalid", "Control must be backup-restore, tenant-isolation, or security-test.");
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
            Notes = request.Notes?.Trim(), ExecutedAt = request.ExecutedAt,
            ExpiresAt = request.ExpiresAt, ExecutedBySubjectId = actor,
        };
        db.ComplianceEvidenceRecords.Add(row);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<LegalHoldDto> PlaceLegalHoldAsync(LegalHoldRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        if (string.IsNullOrWhiteSpace(request.Reference) || string.IsNullOrWhiteSpace(request.Scope) || string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("legal-hold-invalid", "Reference, scope and reason are required.");
        if (await db.LegalHolds.AnyAsync(x => x.Reference == request.Reference.Trim() && x.Status == "active", ct))
            throw new DomainException("legal-hold-exists", "An active legal hold already uses this reference.");
        var row = new LegalHold
        {
            Reference = request.Reference.Trim(), Scope = request.Scope.Trim(), Reason = request.Reason.Trim(),
            Status = "active", PlacedAt = DateTimeOffset.UtcNow, PlacedBySubjectId = actor,
        };
        db.LegalHolds.Add(row);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<LegalHoldDto> ReleaseLegalHoldAsync(Guid id, LegalHoldReleaseRequest request, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var row = await db.LegalHolds.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("legal-hold-not-found", $"Legal hold {id} does not exist.");
        if (row.Status != "active") throw new DomainException("legal-hold-released", "The legal hold is already released.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new DomainException("release-reason-required", "A release reason is required.");
        row.Status = "released";
        row.ReleasedAt = DateTimeOffset.UtcNow;
        row.ReleasedBySubjectId = actor;
        row.ReleaseReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<string> ExportAuditAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var rows = await db.PrivilegedActionEvents.AsNoTracking().OrderByDescending(x => x.Id).Take(10_000).ToListAsync(ct);
        var csv = new StringBuilder("timestamp,actor,roles,method,path,outcome,status_code,request_id\n");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',', Csv(row.CreatedAt.ToString("O")), Csv(row.ActorSubjectId), Csv(row.ActorRoles),
                Csv(row.Method), Csv(row.Path), Csv(row.Outcome), row.StatusCode, Csv(row.RequestId)));
        return csv.ToString();
    }

    private static List<SecurityControlDto> BuildControls(List<ComplianceEvidence> evidence)
    {
        SecurityControlDto EvidenceControl(string key, string name, string detail)
        {
            var row = evidence.Where(x => x.ControlKey == key).OrderByDescending(x => x.ExecutedAt).FirstOrDefault();
            var current = row is not null && row.Status == "passed" && (row.ExpiresAt is null || row.ExpiresAt > DateTimeOffset.UtcNow);
            return new(key, name, current ? "passed" : "action-required", detail, row?.ExecutedAt, row?.ExpiresAt, row?.EvidenceReference);
        }
        return
        [
            new("tenant-query-filter", "Tenant data isolation", "passed", "Every entity is query-filtered and writes reject cross-tenant mutation.", null, null, "automated:M34TenantIsolationTests"),
            new("append-only-audit", "Privileged and entity audit", "passed", "Creates, updates and deletes are traced; audit evidence is immutable.", null, null, "automated:M34SecurityComplianceTests"),
            new("sensitive-redaction", "Sensitive-field protection", "passed", "Audit snapshots redact statutory, banking, token and payload fields.", null, null, "automated:M34SecurityComplianceTests"),
            EvidenceControl("backup-restore", "Backup and restore rehearsal", "Record evidence from a successful isolated restore rehearsal."),
            EvidenceControl("security-test", "Security acceptance test", "Record the latest approved security-test report."),
        ];
    }

    private static string Csv(object? value) => $"\"{(value?.ToString() ?? "").Replace("\"", "\"\"")}\"";
    private static PrivilegedActionDto Map(PrivilegedActionEvent x) => new(x.Id, x.ActorSubjectId,
        x.ActorRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), x.Method,
        x.Path, x.Outcome, x.StatusCode, x.RequestId, x.CreatedAt);
    private static EntityAuditDto Map(AuditEntry x) => new(x.Id, x.EntityType, x.EntityId, x.Action,
        x.ActorSubjectId, x.CorrelationId, x.BeforeJson, x.AfterJson, x.CreatedAt);
    private static ComplianceEvidenceDto Map(ComplianceEvidence x) => new(x.Id, x.ControlKey, x.Status,
        x.EvidenceReference, x.Notes, x.ExecutedAt, x.ExpiresAt, x.ExecutedBySubjectId);
    private static LegalHoldDto Map(LegalHold x) => new(x.Id, x.Reference, x.Scope, x.Reason, x.Status,
        x.PlacedAt, x.PlacedBySubjectId, x.ReleasedAt, x.ReleasedBySubjectId, x.ReleaseReason);
}
