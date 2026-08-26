using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Benefits;

/// <summary>M41 Gap 6b: flexible benefit claims. HR defines claimable benefit
/// types, sets per-worker annual allowances, and employees (or HR on their
/// behalf) submit reimbursement claims against those allowances. Approval
/// reduces the remaining allowance; a paid transition is separate so finance
/// can confirm the money actually went out.</summary>
public sealed class BenefitServiceImpl(
    IBenefitRepository repo,
    IAuthzService authz,
    IWorkerRepository workers,
    Application.ShellContext? scope = null) : IBenefitService
{
    private bool IsEmployeeOnly =>
        authz.IsRole("employee") && !authz.IsRole("hr_ops", "hr_admin", "manager", "payroll");

    private async Task RequireWorkerScopeAsync(Guid workerId, CancellationToken ct)
    {
        if (!IsEmployeeOnly) return;
        var worker = await workers.GetByIdAsync(workerId, ct)
            ?? throw new DomainException("worker-not-found", $"Employee {workerId} does not exist.");
        if (string.IsNullOrWhiteSpace(authz.CurrentSubjectId) ||
            !string.Equals(worker.SubjectId, authz.CurrentSubjectId, StringComparison.Ordinal))
            throw new DomainException("worker-access-denied", "Employees can only claim for themselves.");
    }

    public async Task<List<BenefitTypeDto>> ListBenefitTypesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "employee");
        var types = await repo.ListBenefitTypesAsync(ct);
        return types.Select(t => new BenefitTypeDto(t.Id, t.Code, t.Name, t.Description,
            t.AnnualCap, t.RequiresEvidence, t.IsActive)).ToList();
    }

    public async Task<BenefitTypeDto> CreateBenefitTypeAsync(BenefitTypeCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("benefit-type-invalid", "Code and name are required.");
        var existing = await repo.GetBenefitTypeByCodeAsync(request.Code, ct);
        if (existing is not null)
            throw new DomainException("benefit-type-duplicate", $"A benefit type with code {request.Code} already exists.");
        var type = new BenefitType
        {
            Code = request.Code, Name = request.Name, Description = request.Description,
            AnnualCap = request.AnnualCap, RequiresEvidence = request.RequiresEvidence,
            IsActive = true,
        };
        await repo.CreateBenefitTypeAsync(type, ct);
        return MapType(type);
    }

    public async Task<BenefitTypeDto> UpdateBenefitTypeAsync(Guid id, BenefitTypeUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var types = await repo.ListBenefitTypesAsync(ct);
        var type = types.FirstOrDefault(x => x.Id == id)
            ?? throw new DomainException("benefit-type-not-found", "Benefit type not found.");
        type.Code = request.Code;
        type.Name = request.Name;
        type.Description = request.Description;
        type.AnnualCap = request.AnnualCap;
        type.RequiresEvidence = request.RequiresEvidence;
        type.IsActive = request.IsActive;
        await repo.UpdateBenefitTypeAsync(type, ct);
        return MapType(type);
    }

    public async Task DeleteBenefitTypeAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        await repo.DeleteBenefitTypeAsync(id, ct);
    }

    public async Task<List<BenefitAllowanceDto>> ListAllowancesAsync(Guid? workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "manager");
        if (IsEmployeeOnly)
            throw new DomainException("worker-access-denied", "Allowances are managed by HR only.");
        var allowances = await repo.ListAllowancesAsync(workerId, ct);
        return allowances.Select(a => new BenefitAllowanceDto(a.Id, a.WorkerId,
            a.Worker?.FullName ?? $"Worker {a.WorkerId}", a.Worker?.EmployeeNo,
            a.BenefitType?.Code ?? "?", a.BenefitType?.Name ?? "?",
            a.AnnualAmount, a.Year)).ToList();
    }

    public async Task SetAllowanceAsync(AllowanceSetRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops");
        var worker = await workers.GetByIdAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", "Employee not found.");
        var type = await repo.GetBenefitTypeByCodeAsync(request.BenefitTypeCode, ct)
            ?? throw new DomainException("benefit-type-not-found", $"Unknown benefit type {request.BenefitTypeCode}.");
        if (!type.IsActive)
            throw new DomainException("benefit-type-inactive", "Inactive benefit types cannot receive allowances.");
        if (request.AnnualAmount < 0)
            throw new DomainException("benefit-allowance-invalid", "The annual amount cannot be negative.");
        if (request.AnnualAmount > type.AnnualCap)
            throw new DomainException("benefit-allowance-over-cap",
                $"{type.Name} allowance cannot exceed the configured annual cap of {type.AnnualCap:N2}.");
        var allowance = new WorkerBenefitAllowance
        {
            WorkerId = request.WorkerId,
            BenefitTypeId = type.Id,
            AnnualAmount = request.AnnualAmount,
            Year = request.Year,
        };
        await repo.SetAllowanceAsync(allowance, ct);
    }

    public async Task<(List<BenefitClaimDto> Items, int Total)> ListClaimsAsync(Guid? workerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "manager", "payroll", "employee");
        if (IsEmployeeOnly && workerId is null)
            throw new DomainException("worker-access-denied", "Employees must list their own claims.");
        if (workerId.HasValue) await RequireWorkerScopeAsync(workerId.Value, ct);
        var (items, total) = await repo.ListClaimsAsync(workerId, status, page, pageSize, ct);
        // M44 branch scoping: scoped operators see only their branch's claims.
        if (!IsEmployeeOnly && (scope?.IsScopedToBranch ?? false))
        {
            items = items.Where(c => c.LocationId == scope?.LocationId || c.LocationId == null).ToList();
            total = items.Count;
        }
        return (items.Select(Map).ToList(), total);
    }
    public async Task<BenefitClaimDto> CreateClaimAsync(BenefitClaimCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        await RequireWorkerScopeAsync(request.WorkerId, ct);

        var type = await repo.GetBenefitTypeByCodeAsync(request.BenefitTypeCode, ct)
            ?? throw new DomainException("benefit-type-not-found", $"Unknown benefit type {request.BenefitTypeCode}.");
        if (!type.IsActive)
            throw new DomainException("benefit-type-inactive", "Inactive benefit types cannot be claimed.");
        if (type.RequiresEvidence && !request.EvidenceAttached)
            throw new DomainException("benefit-claim-evidence",
                $"Evidence is required for {type.Name} claims.");
        if (request.AmountClaimed <= 0)
            throw new DomainException("benefit-claim-invalid", "The claimed amount must be positive.");

        var year = DateTime.UtcNow.Year;
        var allowance = await repo.GetAllowanceAsync(request.WorkerId, type.Id, year, ct);
        var annualLimit = allowance?.AnnualAmount ?? type.AnnualCap;
        if (annualLimit <= 0)
            throw new DomainException("benefit-claim-no-allowance",
                $"No allowance is configured for this employee and {type.Name} (set a worker allowance or an org-level annual cap).");
        var spent = await repo.SumApprovedAsync(request.WorkerId, type.Id, year, ct);
        if (spent + request.AmountClaimed > annualLimit + 0.0001m)
            throw new DomainException("benefit-claim-over-limit",
                $"Claim of {request.AmountClaimed} exceeds the remaining {type.Name} allowance ({annualLimit - spent} available for {year}).");

        var claim = new BenefitClaim
        {
            WorkerId = request.WorkerId,
            BenefitTypeId = type.Id,
            AmountClaimed = request.AmountClaimed,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "ZMW" : request.Currency,
            Note = request.Note,
            EvidenceAttached = request.EvidenceAttached,
            // M44 branch scoping: claims inherit the operator's work scope!.
            LocationId = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null,
            Status = "submitted",
            CreatedBySubjectId = authz.CurrentSubjectId,
        };
        await repo.CreateClaimAsync(claim, ct);
        return Map(claim);
    }

    public async Task<BenefitClaimDto> DecideClaimAsync(Guid id, ClaimDecideRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "manager");
        var claim = await repo.GetClaimAsync(id, ct)
            ?? throw new DomainException("benefit-claim-not-found", "Claim not found.");
        if (claim.Status is not ("submitted" or "returned"))
            throw new DomainException("benefit-claim-state",
                $"A claim with status {claim.Status} cannot be decided again.");

        var action = request.Action.ToLowerInvariant();
        if (action is "approve" or "accepted")
        {
            var amount = request.ApprovedAmount is null or <= 0 ? claim.AmountClaimed : request.ApprovedAmount;
            if (amount > claim.AmountClaimed + 0.0001m)
                throw new DomainException("benefit-claim-invalid", "The approved amount cannot exceed the claimed amount.");
            var year = claim.CreatedAt.Year == 1 ? DateTime.UtcNow.Year : claim.CreatedAt.Year;
            var allowance = await repo.GetAllowanceAsync(claim.WorkerId, claim.BenefitTypeId, year, ct);
            var annualLimit = allowance?.AnnualAmount ?? claim.BenefitType?.AnnualCap ?? 0m;
            var spent = await repo.SumApprovedAsync(claim.WorkerId, claim.BenefitTypeId, year, ct);
            if (annualLimit <= 0 || spent + (amount ?? 0m) > annualLimit + 0.0001m)
                throw new DomainException("benefit-claim-over-limit",
                    $"Approving {amount:N2} would exceed the remaining {claim.BenefitType?.Name ?? "benefit"} allowance ({annualLimit - spent:N2} available for {year}).");
            claim.Status = "approved";
            claim.ApprovedAmount = Math.Round(amount ?? 0m, 2);
        }
        else if (action is "reject" or "declined")
        {
            claim.Status = "rejected";
        }
        else if (action is "return" or "returned" or "request-more-info")
        {
            claim.Status = "returned";
        }
        else
        {
            throw new DomainException("benefit-claim-invalid-action",
                "Action must be approve, reject, or return.");
        }
        claim.DecisionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason;
        claim.DecidedBySubjectId = authz.CurrentSubjectId;
        claim.DecidedAt = DateTimeOffset.UtcNow;
        await repo.UpdateClaimAsync(claim, ct);
        return Map(claim);
    }

    public async Task<BenefitClaimDto> PayClaimAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "payroll");
        var claim = await repo.GetClaimAsync(id, ct)
            ?? throw new DomainException("benefit-claim-not-found", "Claim not found.");
        if (claim.Status != "approved")
            throw new DomainException("benefit-claim-state", "Only approved claims can be marked as paid.");
        claim.Status = "paid";
        claim.PaidBySubjectId = authz.CurrentSubjectId;
        claim.PaidAt = DateTimeOffset.UtcNow;
        await repo.UpdateClaimAsync(claim, ct);
        return Map(claim);
    }

    private static BenefitTypeDto MapType(BenefitType t) =>
        new(t.Id, t.Code, t.Name, t.Description, t.AnnualCap, t.RequiresEvidence, t.IsActive);

    private static BenefitClaimDto Map(BenefitClaim c) =>
        new(c.Id, c.WorkerId, c.Worker?.FullName ?? $"Employee {c.WorkerId}",
            c.Worker?.EmployeeNo, c.BenefitTypeId, c.BenefitType?.Code ?? "?",
            c.BenefitType?.Name ?? "?", c.AmountClaimed, c.Currency, c.Note,
            c.EvidenceAttached, c.Status, c.DecisionReason, c.ApprovedAmount,
            c.CreatedBySubjectId, c.DecidedBySubjectId, c.DecidedAt,
            c.PaidBySubjectId, c.PaidAt, c.CreatedAt, c.LocationId);
}
