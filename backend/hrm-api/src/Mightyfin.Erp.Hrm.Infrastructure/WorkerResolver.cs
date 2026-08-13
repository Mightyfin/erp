using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Maps the authenticated identity to a worker record: OIDC maps the
/// Keycloak subject id to Worker.SubjectId; in dev mode it ensures a single
/// deterministic "developer" worker exists so smoke tests and the UI can act
/// as a real worker (HRM-004 subject mapping, simplified).</summary>
public interface IWorkerResolver
{
    /// <summary>The worker GUID the current dev principal should act as.</summary>
    string ResolveDev();
}

public sealed class WorkerResolver(HrmDbContext db) : IWorkerResolver
{
    private static readonly Guid DevWorkerId = Guid.Parse("019ffa92-0000-0000-0000-000000000001");

    public string ResolveDev()
    {
        var existing = db.Workers.FirstOrDefault(w => w.Id == DevWorkerId);
        if (existing is null)
        {
            db.Workers.Add(new Domain.Entities.Worker
            {
                Id = DevWorkerId,
                EmployeeNo = "DEV-001",
                FirstName = "Dev",
                LastName = "Operator",
                WorkerType = "employee",
                Status = "active",
                JobTitle = "HR Administrator",
                SubjectId = "dev-user-001",
            });
            db.SaveChanges();
            return DevWorkerId.ToString();
        }
        return DevWorkerId.ToString();
    }
}
