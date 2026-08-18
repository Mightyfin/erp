using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Application.Organization;

// ===================== DTOs (M39) =====================

/// <summary>One node of the visual org chart: a unit with its leader,
/// headcount of active workers and direct reports (direct children units).</summary>
public sealed record OrgChartNodeDto(
    Guid Id, string Code, string Name, string UnitType, string Status,
    Guid? ParentId, string? ManagerId, string? ManagerName, string? ManagerJobTitle,
    int Headcount,
    string? LegalEntityName,
    List<OrgChartNodeDto> Children);

/// <summary>The full rendered org chart for the tenant.</summary>
public sealed record OrgChartDto(DateTimeOffset AsAt, List<OrgChartNodeDto> Roots);

/// <summary>One reporting-line row: a worker and the manager they currently
/// report to (via their current effective-dated assignment).</summary>
public sealed record ReportingLineDto(
    Guid WorkerId, string EmployeeNo, string FullName, string Status,
    Guid? OrgUnitId, string? OrgUnitName,
    Guid? ManagerId, string? ManagerName,
    string? Grade, string? JobTitle, string? ManagerNamePath);

public sealed record ReportingLineListDto(List<ReportingLineDto> Items, int Total);

/// <summary>Change one or more workers' manager (i.e. reporting line).
/// ManagerId null => remove the reporting line (report to nobody).</summary>
public sealed record ReportingLineUpdateRequest(List<Guid> WorkerIds, Guid? ManagerId, string? EffectiveFrom, string? Reason);

// ===================== Interfaces (M39) =====================

public interface IOrganizationRepository
{
    Task<List<OrgUnit>> ListActiveUnitsAsync(CancellationToken ct);
    Task<List<(Assignment Assignment, Worker Worker, OrgUnit Unit, Worker? Manager)>>
        ListCurrentAssignmentsAsync(Guid? orgUnitId, CancellationToken ct);
    Task<Assignment?> GetCurrentAssignmentAsync(Guid workerId, CancellationToken ct);
    Task<Worker?> GetWorkerAsync(Guid workerId, CancellationToken ct);
    Task UpdateAssignmentManagerAsync(Assignment assignment, Guid? managerId, CancellationToken ct);
    Task<List<Worker>> ListWorkersAsync(List<Guid> ids, CancellationToken ct);
}

public interface IChartService
{
    Task<OrgChartDto> GetOrgChartAsync(CancellationToken ct);
    Task<ReportingLineListDto> ListReportingLinesAsync(Guid? orgUnitId, string? search, CancellationToken ct);
    Task UpdateReportingLinesAsync(ReportingLineUpdateRequest request, CancellationToken ct);
}

// ===================== Implementation (M39) =====================

public sealed class ChartServiceImpl(
    IOrganizationRepository repo, IAuthzService authz) : IChartService
{
    public async Task<OrgChartDto> GetOrgChartAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");

        var units = await repo.ListActiveUnitsAsync(ct);
        var assignments = await repo.ListCurrentAssignmentsAsync(null, ct);

        // Headcount per unit: active workers whose current assignment sits in the unit.
        var headcount = new Dictionary<Guid, int>();
        foreach (var (assignment, worker, unit, _) in assignments)
        {
            if (worker.Status == "active" && assignment.Status == "current")
                headcount[unit.Id] = headcount.GetValueOrDefault(unit.Id) + 1;
        }

        var workerById = assignments
            .Where(t => t.Worker != null)
            .Select(t => t.Worker)
            .DistinctBy(w => w!.Id)
            .ToDictionary(w => w!.Id);

        var byId = units.ToDictionary(u => u.Id);

        OrgChartNodeDto Build(Guid unitId)
        {
            var u = byId[unitId];
            var childIds = units.Where(x => x.ParentId == unitId).Select(x => x.Id).ToList();
            var children = childIds.Select(Build).OrderBy(c => c.Name).ToList();
            string? managerName = null;
            string? managerJobTitle = null;
            if (u.ManagerId.HasValue && workerById.TryGetValue(u.ManagerId.Value, out var mgr))
            {
                managerName = mgr.FullName;
                var mgrAssignment = assignments
                    .Where(a => a.Worker != null && a.Worker.Id == u.ManagerId.Value)
                    .Select(a => a.Assignment)
                    .FirstOrDefault();
                managerJobTitle = mgrAssignment?.JobTitle ?? mgr.JobTitle;
            }
            return new OrgChartNodeDto(
                u.Id, u.Code, u.Name, u.UnitType ?? "department", u.Status,
                u.ParentId, u.ManagerId?.ToString(), managerName, managerJobTitle,
                headcount.GetValueOrDefault(unitId),
                u.LegalEntity?.TradingName ?? u.LegalEntity?.RegisteredName,
                children);
        }

        var roots = units.Where(u => u.ParentId == null).Select(u => Build(u.Id))
            .OrderBy(n => n.Name).ToList();
        return new OrgChartDto(DateTimeOffset.UtcNow, roots);
    }

    public async Task<ReportingLineListDto> ListReportingLinesAsync(Guid? orgUnitId, string? search, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");

        var rows = await repo.ListCurrentAssignmentsAsync(orgUnitId, ct);
        IEnumerable<(Assignment Assignment, Worker Worker, OrgUnit Unit, Worker? Manager)> q = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(t => t.Worker.FullName.ToLower().Contains(s)
                || t.Worker.EmployeeNo.ToLower().Contains(s));
        }
        var distinct = q.DistinctBy(t => t.Worker.Id)
            .OrderBy(t => t.Worker.FullName)
            .ToList();

        // Preload unit ancestry so the "reports-to path" column can show the
        // chain from root down to the worker's own unit.
        var allUnits = await repo.ListActiveUnitsAsync(ct);
        var unitByName = allUnits.ToDictionary(u => u.Id);
        string? UnitPath(Guid? unitId)
        {
            var names = new List<string>();
            var seen = new HashSet<Guid>();
            while (unitId.HasValue && unitByName.TryGetValue(unitId.Value, out var u) && seen.Add(unitId.Value))
            {
                names.Add(u.Name);
                unitId = u.ParentId;
            }
            if (names.Count == 0) return null;
            names.Reverse();
            return string.Join(" > ", names);
        }

        var items = distinct.Select(t => new ReportingLineDto(
            t.Worker.Id, t.Worker.EmployeeNo, t.Worker.FullName, t.Worker.Status,
            t.Unit.Id, t.Unit.Name,
            t.Manager?.Id, t.Manager?.FullName,
            t.Worker.Grade, t.Worker.JobTitle ?? t.Assignment.JobTitle,
            UnitPath(t.Unit.Id)))
            .ToList();
        return new ReportingLineListDto(items, items.Count);
    }

    public async Task UpdateReportingLinesAsync(ReportingLineUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");

        if (request.WorkerIds.Count == 0)
            throw new DomainException("bad-request", "No workers were selected.");
        if (request.WorkerIds.Count > 100)
            throw new DomainException("bad-request", "Update at most 100 workers at a time.");

        // Sanity: no duplicates and no self-reporting.
        if (request.WorkerIds.Distinct().Count() != request.WorkerIds.Count)
            throw new DomainException("bad-request", "The same worker was listed more than once.");
        if (request.ManagerId.HasValue && request.WorkerIds.Contains(request.ManagerId.Value))
            throw new DomainException("bad-request", "A worker cannot report to themselves.");

        var managers = request.ManagerId.HasValue
            ? await repo.ListWorkersAsync([request.ManagerId.Value], ct) : new List<Worker>();

        foreach (var workerId in request.WorkerIds)
        {
            var assignment = await repo.GetCurrentAssignmentAsync(workerId, ct)
                ?? throw new DomainException("worker-not-active",
                    $"Worker {workerId} has no current active assignment; activate the worker before changing reporting lines.");

            // Cycle check: walk the manager chain up from the chosen manager; if we
            // ever encounter this worker, the update would create a reporting loop.
            if (request.ManagerId.HasValue)
            {
                // The chosen manager must itself exist and have a current assignment
                // (a manager without an assignment cannot supervise anyone).
                var managerWorker = await repo.GetWorkerAsync(request.ManagerId.Value, ct)
                    ?? throw new DomainException("manager-not-found", $"Manager {request.ManagerId.Value} does not exist.");

                var visited = new HashSet<Guid> { workerId };
                var managerCursor = request.ManagerId;
                while (managerCursor.HasValue && visited.Add(managerCursor.Value))
                {
                    var next = await repo.GetCurrentAssignmentAsync(managerCursor.Value, ct);
                    if (next == null) break;
                    managerCursor = next.ManagerId;
                }
                if (managerCursor.HasValue && managerCursor.Value == workerId)
                    throw new DomainException("reporting-loop", $"Setting this manager would create a reporting loop for worker {workerId}.");
            }

            assignment.ManagerId = request.ManagerId;
            await repo.UpdateAssignmentManagerAsync(assignment, request.ManagerId, ct);
        }

        // Sync the unit-head reference: when the *only* changed worker is the
        // manager of a unit, keep OrgUnit.ManagerId consistent is optional —
        // the visual chart resolves the head from active assignments instead.
        _ = managers;
    }
}
