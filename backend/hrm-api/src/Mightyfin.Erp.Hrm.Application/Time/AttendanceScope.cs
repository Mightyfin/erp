using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Time;

public static class AttendanceScope
{
    public static bool Allows(Worker worker, ShellContext? scope) => scope is null ||
        ((!scope.LocationId.HasValue || worker.LocationId == scope.LocationId)
        && (!scope.OrgUnitId.HasValue || worker.OrgUnitId == scope.OrgUnitId)
        && (!scope.IsConfined || (worker.LocationId.HasValue && scope.AllowedLocationIds.Contains(worker.LocationId.Value))));
}
