using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Application.Payroll;

namespace Mightyfin.Erp.Hrm.Api.Routing;

// Minimal-API routes grouped by the frontend client interfaces (PeopleClient,
// TimeClient, WorkflowClient, ExperienceClient, PayrollClient,
// AdminConfigClient, RecruitmentClient, RelationsClient, DocumentsClient).
public static class Routes
{
    // Helper: read a JSON body manually inside a minimal-API handler.
    private static async Task<T?> ReadBodyAsync<T>(HttpContext http, CancellationToken ct)
    {
        http.Request.EnableBuffering();
        var stream = http.Request.Body;
        http.Request.Body.Position = 0;
        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }

    public static void RegisterWorkers(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/workers").RequireAuthorization();

        g.MapGet("/", async ([AsParameters] WorkerListFilters filters, IWorkerService svc, CancellationToken ct)
            => await svc.ListAsync(filters, ct));

        g.MapGet("/{id:guid}", async (Guid id, IWorkerService svc, CancellationToken ct)
            => await svc.GetByIdAsync(id, ct));

        g.MapPost("/", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var errors = ValidateWorkerCreate(request);
            if (errors.Count != 0)
                return Results.UnprocessableEntity(new ApiError("validation-failed", string.Join("; ", errors), []));
            var created = await svc.CreateAsync(request, ct);
            return Results.Created($"/api/hrm/workers/{created.Id}", created);
        });

        g.MapPut("/{id:guid}", async (Guid id, HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateAsync(id, request, ct));
        });

        g.MapPost("/{id:guid}/archive", async (Guid id, IWorkerService svc, CancellationToken ct) =>
        {
            await svc.ArchiveAsync(id, ct);
            return Results.Ok();
        });

        g.MapGet("/{id:guid}/assignments", async (Guid id, IWorkerService svc, CancellationToken ct)
            => await svc.ListAssignmentsAsync(id, ct));
        g.MapPost("/assignments", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AssignmentCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateAssignmentAsync(request, ct));
        });
        g.MapGet("/{id:guid}/movements", async (Guid id, IWorkerService svc, CancellationToken ct)
            => await svc.ListMovementsAsync(id, ct));
        g.MapPost("/movements", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<MovementCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateMovementAsync(request, ct));
        });
        g.MapPost("/movements/{movementId:guid}/execute", async (Guid movementId, IWorkerService svc, CancellationToken ct) =>
        {
            await svc.ExecuteMovementAsync(movementId, ct);
            return Results.Ok();
        });
    }

    private static List<string> ValidateWorkerCreate(WorkerCreateRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.EmployeeNo)) errors.Add("employeeNo is required");
        if (string.IsNullOrWhiteSpace(request.FirstName)) errors.Add("firstName is required");
        if (string.IsNullOrWhiteSpace(request.LastName)) errors.Add("lastName is required");
        if (request.WorkerType is not ("employee" or "contingent" or "intern" or "volunteer"))
            errors.Add("workerType must be employee|contingent|intern|volunteer");
        return errors;
    }

    public static void RegisterTime(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/time").RequireAuthorization();
        g.MapGet("/leave", async ([FromQuery] Guid? workerId, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => await svc.ListLeaveAsync(workerId, status, ct));
        g.MapPost("/leave", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLeaveAsync(request, ct));
        });
        g.MapGet("/leave/balances/{workerId:guid}", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => await svc.GetBalancesAsync(workerId, ct));
        g.MapGet("/corrections", async ([FromQuery] Guid? workerId, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => await svc.ListCorrectionsAsync(workerId, status, ct));
        g.MapPost("/corrections", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AttendanceCorrectionCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCorrectionAsync(request, ct));
        });
    }

    public static void RegisterWorkflow(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/workflow").RequireAuthorization();
        g.MapGet("/queue", async (IWorkflowService svc, CancellationToken ct)
            => await svc.GetWorkQueueAsync(ct));
        g.MapGet("/requests/{id:guid}", async (Guid id, IWorkflowService svc, CancellationToken ct)
            => await svc.GetByIdAsync(id, ct));
        g.MapPost("/requests/{id:guid}/decisions", async (Guid id, HttpContext http, IWorkflowService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkflowDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var actorSubject = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(actorSubject))
                return Results.Unauthorized();
            return Results.Ok(await svc.DecideAsync(id, Guid.Parse(actorSubject), request, ct));
        });
    }

    public static void RegisterExperience(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/experience").RequireAuthorization();
        g.MapGet("/requests", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IExperienceService svc, CancellationToken ct)
            => await svc.ListRequestsAsync(workerId, status, ct));
        g.MapPost("/requests", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateRequestAsync(request, ct));
        });
        g.MapPost("/requests/{id:guid}/messages", async (Guid id, HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestMessageCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            await svc.AddMessageAsync(id, request, ct);
            return Results.Ok();
        });
        g.MapGet("/letters", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IExperienceService svc, CancellationToken ct)
            => await svc.ListLettersAsync(workerId, status, ct));
        g.MapPost("/letters", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrLetterCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLetterAsync(request, ct));
        });
        g.MapPost("/letters/{id:guid}/approve", async (Guid id, IExperienceService svc, CancellationToken ct) =>
        {
            await svc.ApproveLetterAsync(id, ct);
            return Results.Ok();
        });
        g.MapPost("/speak-up", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ProtectedDisclosureCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("/api/hrm/experience/speak-up/status", await svc.SubmitDisclosureAsync(request, ct));
        });
        g.MapGet("/speak-up/status", async ([FromQuery] string caseReference, [FromQuery] string accessCode, IExperienceService svc, CancellationToken ct) =>
            await svc.GetDisclosureStatusAsync(caseReference, accessCode, ct));
    }

    public static void RegisterPayroll(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/payroll").RequireAuthorization();
        g.MapGet("/components", async ([FromQuery] string? type, IPayrollService svc, CancellationToken ct)
            => await svc.ListComponentsAsync(type, ct));
        g.MapGet("/pay-groups", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListPayGroupsAsync(ct));
        g.MapGet("/pay-groups/{groupId:guid}/periods", async (Guid groupId, IPayrollService svc, CancellationToken ct)
            => await svc.ListPeriodsAsync(groupId, ct));
        g.MapGet("/tax-slabs", async ([FromQuery] string taxYear, IPayrollService svc, CancellationToken ct)
            => await svc.ListTaxSlabsAsync(taxYear, ct));
        g.MapGet("/contribution-rules", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListContributionRulesAsync(ct));
        g.MapPost("/runs", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateRunAsync(request, ct));
        });
        g.MapGet("/runs/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.GetRunAsync(id, ct));
        g.MapPost("/runs/{id:guid}/calculate", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            await svc.CalculateRunAsync(id, ct));
        g.MapGet("/runs/{id:guid}/lines", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.GetRunLinesAsync(id, ct));
        g.MapPost("/runs/{id:guid}/approve", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var note = await ReadBodyAsync<PayrollRunApprovalNote>(http, ct);
            await svc.ApproveRunAsync(id, note?.Note, ct);
            return Results.Ok();
        });
        g.MapPost("/runs/{id:guid}/release", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            await svc.ReleaseRunAsync(id, ct));
        g.MapGet("/payslips/{workerId:guid}", async (Guid workerId, IPayrollService svc, CancellationToken ct)
            => await svc.GetPayslipsAsync(workerId, ct));
        g.MapGet("/payslips/id/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.GetPayslipByIdAsync(id, ct));
    }

    public static void RegisterConfig(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/admin").RequireAuthorization();
        g.MapGet("/config", async (IConfigService svc, CancellationToken ct) => await svc.GetConfigAsync(ct));
        g.MapGet("/leave-types", async ([FromQuery] bool includeInactive, IConfigService svc, CancellationToken ct) =>
            await svc.ListLeaveTypesAsync(includeInactive, ct));

        // ---------- M1: organization configuration CRUD ----------
        g.MapGet("/legal-entities", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListLegalEntitiesAsync(ct));
        g.MapGet("/legal-entities/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) => await svc.GetLegalEntityAsync(id, ct));
        g.MapPost("/legal-entities", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalEntityCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created($"/api/hrm/admin/legal-entities/{request.Code}", await svc.CreateLegalEntityAsync(request, ct));
        });
        g.MapPatch("/legal-entities/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalEntityUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateLegalEntityAsync(id, request, ct));
        });

        g.MapGet("/locations", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListLocationsAsync(ct));
        g.MapPost("/locations", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkLocationCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLocationAsync(request, ct));
        });
        g.MapPatch("/locations/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkLocationUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateLocationAsync(id, request, ct));
        });

        g.MapGet("/org-units", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListOrgUnitsAsync(ct));
        g.MapGet("/org-units/tree", async (IConfigAdminService svc, CancellationToken ct) => await svc.GetOrgUnitTreeAsync(ct));
        g.MapPost("/org-units", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OrgUnitCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateOrgUnitAsync(request, ct));
        });
        g.MapPatch("/org-units/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OrgUnitUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateOrgUnitAsync(id, request, ct));
        });
        g.MapPost("/org-units/{id:guid}/close", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OrgUnitCloseRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            await svc.CloseOrgUnitAsync(id, request, ct);
            return Results.Ok();
        });

        g.MapGet("/calendars", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListCalendarsAsync(ct));
        g.MapGet("/calendars/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) => await svc.GetCalendarAsync(id, ct));
        g.MapPost("/calendars", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkCalendarCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCalendarAsync(request, ct));
        });
        g.MapPatch("/calendars/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkCalendarUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateCalendarAsync(id, request, ct));
        });
        g.MapPost("/holidays", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PublicHolidayCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.AddHolidayAsync(request, ct));
        });
        g.MapPatch("/holidays/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PublicHolidayUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateHolidayAsync(id, request, ct));
        });
        g.MapDelete("/holidays/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) =>
        {
            await svc.DeleteHolidayAsync(id, ct);
            return Results.Ok();
        });

        g.MapGet("/leave-types/full", async ([FromQuery] bool includeInactive, IConfigAdminService svc, CancellationToken ct) =>
            await svc.ListLeaveTypesAsync(includeInactive, ct));
        g.MapPost("/leave-types", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveTypeCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLeaveTypeAsync(request, ct));
        });
        g.MapPatch("/leave-types/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveTypeUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateLeaveTypeAsync(id, request, ct));
        });

        g.MapGet("/capabilities", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListCapabilitiesAsync(ct));
        g.MapPatch("/capabilities/{featureKey}", async (string featureKey, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<CapabilityUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateCapabilityAsync(featureKey, request, ct));
        });
    }

    public static void RegisterRecruitment(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/recruitment").RequireAuthorization();
        g.MapGet("/vacancies", async ([FromQuery] string? status, IRecruitmentService svc, CancellationToken ct) =>
            await svc.ListVacanciesAsync(status, ct));
        g.MapPost("/vacancies", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<VacancyCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateVacancyAsync(request, ct));
        });
        g.MapGet("/vacancies/{vacancyId:guid}/candidates", async (Guid vacancyId, [FromQuery] string? stage, IRecruitmentService svc, CancellationToken ct) =>
            await svc.ListCandidatesAsync(vacancyId, stage, ct));
        g.MapPost("/candidates", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<CandidateCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCandidateAsync(request, ct));
        });
        g.MapPost("/candidates/{id:guid}/advance", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<CandidateAdvanceRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            await svc.AdvanceCandidateAsync(id, request, ct);
            return Results.Ok();
        });
        g.MapPost("/offers", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OfferCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateOfferAsync(request, ct));
        });
    }

    public static void RegisterRelations(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/relations").RequireAuthorization();
        g.MapGet("/cases", async ([FromQuery] string? category, IRelationsService svc, CancellationToken ct) =>
            await svc.ListCasesAsync(category, ct));
        g.MapPost("/cases", async (HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCaseAsync(request, ct));
        });
    }

    public static void RegisterDocuments(WebApplication app)
    {
        var g = app.MapGroup("/api/hrm/documents").RequireAuthorization();
        g.MapGet("/worker/{workerId:guid}", async (Guid workerId, IDocumentsService svc, CancellationToken ct) =>
            await svc.ListDocumentsAsync(workerId, ct));
        g.MapPost("/", async (HttpContext http, IDocumentsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerDocumentCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.RegisterDocumentAsync(request, "/tmp/erp-docs", 0, ct));
        });
        var reports = app.MapGroup("/api/hrm/reports").RequireAuthorization();
        reports.MapGet("/", async ([AsParameters] ReportQuery query, IDocumentsService svc, CancellationToken ct) =>
            await svc.GetReportAsync(query, ct));
    }
}

// Route-local binding types.
public sealed record PayrollRunApprovalNote(string? Note);
