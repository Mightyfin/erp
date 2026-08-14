using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Experience;

public interface IExperienceRepository
{
    Task<(List<HrRequest> Items, int Total)> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrRequest?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<HrRequest> CreateRequestAsync(HrRequest request, CancellationToken ct);
    Task<HrRequest> UpdateRequestAsync(HrRequest request, CancellationToken ct);
    /// M22: inserts the message as a top-level Added entity (immune to EF Core 10's
    /// Modified-parent demotion of navigation-added children) and persists the
    /// parent's status transition in the same unit of work.
    Task<HrRequest> AddMessageAsync(HrRequest request, HrRequestMessage message, CancellationToken ct);
    Task<(List<HrLetter> Items, int Total)> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrLetter?> GetLetterAsync(Guid id, CancellationToken ct);
    Task<HrLetter> CreateLetterAsync(HrLetter letter, CancellationToken ct);
    Task<HrLetter> UpdateLetterAsync(HrLetter letter, CancellationToken ct);
    Task<int> CountDisclosuresThisYearAsync(CancellationToken ct);
    Task<ProtectedDisclosure> CreateDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct);
    Task<ProtectedDisclosure?> GetDisclosureByCaseReferenceAsync(string caseReference, CancellationToken ct);
}
