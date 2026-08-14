using System;
using System.IO;
using System.Linq;
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
    /// <summary>Base path for all HRM endpoints; set before RegisterAll so both
    /// the versioned /api/v1/hrm and legacy /api/hrm surfaces resolve to the
    /// same handlers.</summary>
    public static string HrmPrefix { get; set; } = "/api/hrm";

    /// <summary>Registers every HRM route group. Called twice: once with
    /// HrmPrefix="/api/v1/hrm" and once with the legacy "/api/hrm".</summary>
    public static void RegisterAll(WebApplication app)
    {
        RegisterWorkers(app);
        RegisterTime(app);
        RegisterWorkflow(app);
        RegisterExperience(app);
        RegisterPayroll(app);
        RegisterConfig(app);
        RegisterRecruitment(app);
        RegisterRelations(app);
        RegisterDocuments(app);
        RegisterDq(app);
        RegisterStatutory(app);
        RegisterMe(app);
    }


    // Helper: read a JSON body manually inside a minimal-API handler.
    private static async Task<T?> ReadBodyAsync<T>(HttpContext http, CancellationToken ct)
    {
        http.Request.EnableBuffering();
        var stream = http.Request.Body;
        http.Request.Body.Position = 0;
        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }

    // Helper: resolve the calling worker from the authenticated principal.
    // Only the explicit `worker_id` claim is trusted; the raw subject id is
    // deliberately NOT used (a Keycloak subject uuid parses as a Guid but is
    // never a worker record — the subject→worker mapping lives in M14 and is
    // resolved via IWorkerService.GetBySubjectAsync where needed).
    private static Guid? ResolveWorkerId(HttpContext http)
    {
        var raw = http.User.FindFirst("worker_id")?.Value;
        return string.IsNullOrEmpty(raw) || !System.Guid.TryParse(raw, out var id) ? null : id;
    }

    // Helper: read the Keycloak subject id from the current principal.
    // JwtSecurityTokenHandler maps the JWT "sub" claim to the
    // NameIdentifier claim type, so check both the raw "sub" name and the
    // mapped NameIdentifier type.
    private static string? ResolveSubjectId(HttpContext http)
        => http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    // M14 identity link: resolve the worker record bound to the caller's
    // Keycloak subject. Registered once (not per prefix) because the route is
    // identical on both surfaces.
    public static void RegisterMe(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/me").RequireAuthorization();
        g.MapGet("/", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                return Results.Ok(new { linked = false, worker = (object?)null, reason = "no-subject-claim" });
            var worker = await svc.GetBySubjectAsync(subject, ct);
            return worker is null
                ? Results.Ok(new { linked = false, worker = (object?)null, subject })
                : Results.Ok(new { linked = true, worker, subject });
        });

        // M15 self-service: workers update their own profile. The subject is
        // read from the token and merged into the request; admin-only fields
        // are not part of the request shape and can never be changed here.
        g.MapPut("/profile", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            var raw = await ReadBodyAsync<WorkerSubjectUpdateRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateOwnProfileAsync(raw with { SubjectId = subject }, ct));
        });

        // M16 self-service: the signed-in worker's own leave inbox (balances +
        // own requests + cancel) — always keyed on the token subject.
        g.MapGet("/leave", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            return Results.Ok(await svc.MyLeaveAsync(subject ?? "", ct));
        });
        g.MapPost("/leave/{id:guid}/cancel", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            return Results.Ok(await svc.CancelLeaveAsync(id, subject, ct));
        });
    }

    public static void RegisterWorkers(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/workers").RequireAuthorization();

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
            return Results.Created($"{HrmPrefix}/workers/{created.Id}", created);
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

        // M2 lifecycle surface
        RegisterWorkerLifecycleRoutes(g);
    }

    private static void RegisterWorkerLifecycleRoutes(RouteGroupBuilder g)
    {
        g.MapGet("/{workerId:guid}/assignments", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListAssignmentsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/assignments", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AssignmentCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.CreateAssignmentAsync(workerId, request, ct);
            return Results.Created($"{HrmPrefix}/workers/{workerId}/assignments/{created.Id}", created);
        });
        g.MapPatch("/{workerId:guid}/assignments/{assignmentId:guid}", async (Guid workerId, Guid assignmentId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AssignmentUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateAssignmentAsync(workerId, assignmentId, request, ct));
        });
        g.MapPost("/{workerId:guid}/assignments/{assignmentId:guid}/end", async (Guid workerId, Guid assignmentId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.EndAssignmentAsync(workerId, assignmentId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/movements", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListMovementsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/movements", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<MovementCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.CreateMovementAsync(workerId, request, ct);
            return Results.Created($"{HrmPrefix}/workers/{workerId}/movements/{created.Id}", created);
        });
        g.MapGet("/{workerId:guid}/movements/{movementId:guid}", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.GetMovementAsync(workerId, movementId, ct));
        g.MapGet("/{workerId:guid}/movements/{movementId:guid}/preview", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.PreviewMovementAsync(workerId, movementId, ct));
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/submit", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.SubmitMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/approve", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.ApproveMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/reject", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.RejectMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/cancel", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.CancelMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/emergency-contacts", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListEmergencyContactsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/emergency-contacts", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<EmergencyContactRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddEmergencyContactAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/emergency-contacts/{contactId:guid}", async (Guid workerId, Guid contactId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<EmergencyContactRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateEmergencyContactAsync(workerId, contactId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/emergency-contacts/{contactId:guid}", async (Guid workerId, Guid contactId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteEmergencyContactAsync(workerId, contactId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/bank-details", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListBankDetailsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/bank-details", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<BankDetailRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddBankDetailAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/bank-details/{bankId:guid}", async (Guid workerId, Guid bankId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<BankDetailRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateBankDetailAsync(workerId, bankId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/bank-details/{bankId:guid}", async (Guid workerId, Guid bankId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteBankDetailAsync(workerId, bankId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/onboarding", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.GetOnboardingAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/offboard", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var result = await svc.OffboardAsync(workerId, ct);
            if (!result.Cleared)
                return Results.Conflict(new ApiError("offboarding-blocked", "Offboarding blocked by open clearance items.", result.OpenItems));
            return Results.Ok(result);
        });
    }

    private static List<string> ValidateWorkerCreate(WorkerCreateRequest request)
    {
        var errors = new List<string>();
        // Employee number is auto-issued by the backend when the request leaves it
        // empty — the UI deliberately never asks HR to type one ("issued automatically").
        if (string.IsNullOrWhiteSpace(request.FirstName)) errors.Add("firstName is required");
        if (string.IsNullOrWhiteSpace(request.LastName)) errors.Add("lastName is required");
        if (request.WorkerType is not ("employee" or "contingent" or "intern" or "volunteer"))
            errors.Add("workerType must be employee|contingent|intern|volunteer");
        return errors;
    }

    public static void RegisterTime(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/time").RequireAuthorization();
        g.MapGet("/leave", async ([FromQuery] Guid? workerId, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => await svc.ListLeaveAsync(workerId, status, ct));
        g.MapPost("/leave", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLeaveAsync(request, ct));
        });
        g.MapPost("/leave/{id:guid}/decide", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<TimeDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideLeaveAsync(id, request, ct));
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
        g.MapPost("/corrections/{id:guid}/decide", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<TimeDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideCorrectionAsync(id, request, ct));
        });

        // M3 attendance: punch, today record, range and roster
        g.MapPost("/attendance/{workerId:guid}/clock-in", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.ClockInAsync(workerId, ct)));
        g.MapPost("/attendance/{workerId:guid}/clock-out", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.ClockOutAsync(workerId, ct)));
        g.MapGet("/attendance/{workerId:guid}/today", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.GetTodayAsync(workerId, ct)));
        g.MapGet("/attendance/{workerId:guid}", async (Guid workerId, [FromQuery] string? from, [FromQuery] string? to, ITimeService svc, CancellationToken ct)
            => await svc.ListAttendanceAsync(workerId, from, to, ct));
        g.MapGet("/roster/{workerId:guid}", async (Guid workerId, [FromQuery] string? from, [FromQuery] string? to, ITimeService svc, CancellationToken ct)
            => await svc.GetRosterAsync(workerId, from, to, ct));
    }

    public static void RegisterWorkflow(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/workflow").RequireAuthorization();
        g.MapGet("/queue", async (IWorkflowService svc, CancellationToken ct)
            => await svc.GetWorkQueueAsync(ct));
        g.MapGet("/requests/{id:guid}", async (Guid id, IWorkflowService svc, CancellationToken ct)
            => await svc.GetByIdAsync(id, ct));
        g.MapPost("/requests/{id:guid}/decisions", async (Guid id, HttpContext http, IWorkflowService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkflowDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var actorSubject = http.User.FindFirst("worker_id")?.Value ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(actorSubject) || !System.Guid.TryParse(actorSubject, out var actorId))
                return Results.Json(new ApiError("missing-actor", "The authenticated actor could not be resolved to a worker id; pass a 'worker_id' claim or use the actor_id query parameter.", []), statusCode: 401);
            return Results.Ok(await svc.DecideAsync(id, actorId, request, ct));
        });
        g.MapPost("/requests/{id:guid}/escalate", async (Guid id, HttpContext http, IWorkflowService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkflowEscalateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.EscalateAsync(id, request.ActorId, ct));
        });
    }

    public static void RegisterExperience(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/experience").RequireAuthorization();
        g.MapGet("/requests", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IExperienceService svc, CancellationToken ct)
            => await svc.ListRequestsAsync(workerId, status, ct));
        g.MapPost("/requests", async (HttpContext http, IExperienceService svc, IWorkerService ws, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var workerId = request.WorkerId ?? ResolveWorkerId(http);
            // M22: without a worker_id claim, resolve the caller via the M14
            // subject identity link instead of the raw sub Guid (a Keycloak
            // subject uuid parses as a Guid but is never a worker record).
            if (workerId is null && http.User.FindFirst("worker_id")?.Value is null)
            {
                var subject = ResolveSubjectId(http);
                if (!string.IsNullOrEmpty(subject))
                    workerId = (await ws.GetBySubjectAsync(subject, ct))?.Id;
            }
            // workerId null = HR-initiated internal request (no worker record).
            return Results.Created("", await svc.CreateRequestAsync(workerId, request, ct));
        });
        g.MapPost("/requests/{id:guid}/messages", async (Guid id, HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestMessageCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var actorRole = http.User.IsInRole("hr_ops") || http.User.IsInRole("hr_admin") ? "hr_ops" : "employee";
            await svc.AddMessageAsync(id, ResolveWorkerId(http), actorRole, request, ct);
            return Results.Ok();
        });
        g.MapPost("/requests/{id:guid}/resolve", async (Guid id, IExperienceService svc, CancellationToken ct) =>
        {
            return Results.Ok(await svc.ResolveRequestAsync(id, ct));
        });
        g.MapGet("/letters", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IExperienceService svc, CancellationToken ct)
            => await svc.ListLettersAsync(workerId, status, ct));
        g.MapPost("/letters", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrLetterCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var workerId = request.WorkerId ?? ResolveWorkerId(http);
            if (workerId is null)
                return Results.UnprocessableEntity(new ApiError("missing-worker", "WorkerId is required; either include worker_id in the body or authenticate as the worker.", []));
            return Results.Created("", await svc.CreateLetterAsync(workerId.Value, request, ct));
        });
        g.MapPost("/letters/{id:guid}/approve", async (Guid id, IExperienceService svc, CancellationToken ct) =>
        {
            await svc.ApproveLetterAsync(id, ct);
            return Results.Ok();
        });
        g.MapPost("/speak-up", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ProtectedDisclosureCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created($"{HrmPrefix}/experience/speak-up/status", await svc.SubmitDisclosureAsync(request, ct));
        });
        g.MapGet("/speak-up/status", async ([FromQuery] string caseReference, [FromQuery] string accessCode, IExperienceService svc, CancellationToken ct) =>
            await svc.GetDisclosureStatusAsync(caseReference, accessCode, ct));
    }

    public static void RegisterPayroll(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/payroll").RequireAuthorization();
        g.MapGet("/components", async ([FromQuery] string? type, IPayrollService svc, CancellationToken ct)
            => await svc.ListComponentsAsync(type, ct));
        g.MapGet("/pay-groups", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListPayGroupsAsync(ct));
        g.MapGet("/pay-groups/full", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListPayGroupsFullAsync(ct));
        g.MapPatch("/pay-groups/{groupId:guid}", async (Guid groupId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayGroupUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdatePayGroupAsync(groupId, request, ct));
        });
        g.MapPatch("/tax-slabs/{slabId:guid}", async (Guid slabId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<TaxSlabUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateTaxSlabAsync(slabId, request, ct));
        });
        g.MapPatch("/contribution-rules/{ruleId:guid}", async (Guid ruleId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ContributionRuleUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateContributionRuleAsync(ruleId, request, ct));
        });
        g.MapPatch("/components/{componentId:guid}", async (Guid componentId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryComponentUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateSalaryComponentAsync(componentId, request, ct));
        });
        // M21: salary structure administration
        g.MapGet("/structures", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListStructuresAsync(ct));
        g.MapGet("/structures/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.GetStructureAsync(id, ct)));
        g.MapPost("/structures", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryStructureCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateStructureAsync(request, ct));
        });
        g.MapPatch("/structures/{id:guid}", async (Guid id, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryStructureUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateStructureAsync(id, request, ct));
        });
        g.MapGet("/pay-groups/{groupId:guid}/periods", async (Guid groupId, IPayrollService svc, CancellationToken ct)
            => await svc.ListPeriodsAsync(groupId, ct));
        g.MapGet("/tax-slabs", async ([FromQuery] string taxYear, IPayrollService svc, CancellationToken ct)
            => await svc.ListTaxSlabsAsync(taxYear, ct));
        g.MapGet("/contribution-rules", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListContributionRulesAsync(ct));
        g.MapGet("/profiles", async ([FromQuery] Guid? workerId, IPayrollService svc, CancellationToken ct)
            => await svc.ListProfilesAsync(workerId, ct));
        g.MapPost("/profiles/{workerId:guid}", async (Guid workerId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerPayrollProfileCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpsertProfileAsync(workerId, request, ct));
        });
        g.MapPost("/runs", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateRunAsync(request, ct));
        });
        g.MapGet("/runs/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.GetRunAsync(id, ct));
        g.MapPost("/runs/{id:guid}/lock", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            await svc.LockRunAsync(id, ct));
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

        // ---------- M6: reversal, liability reports, payslip documents ----------
        g.MapPost("/runs/{id:guid}/reverse", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunReverseCreate>(http, ct) ?? new PayrollRunReverseCreate();
            return Results.Ok(await svc.ReverseRunAsync(id, request, ct));
        });
        g.MapGet("/reports/employer-liability/{periodId:guid}", async (Guid periodId, IPayrollService svc, CancellationToken ct)
            => await svc.EmployerLiabilityReportAsync(periodId, ct));
        g.MapPost("/payslips/{id:guid}/generate", async (Guid id, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.GeneratePayslipDocumentAsync(id, ct)));
    }

    public static void RegisterConfig(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/admin").RequireAuthorization();
        g.MapGet("/config", async (IConfigService svc, CancellationToken ct) => await svc.GetConfigAsync(ct));
        g.MapGet("/leave-types", async ([FromQuery] bool includeInactive, IConfigService svc, CancellationToken ct) =>
            await svc.ListLeaveTypesAsync(includeInactive, ct));

        // ---------- M1: organization configuration CRUD ----------
        g.MapGet("/legal-entities", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListLegalEntitiesAsync(ct));
        g.MapGet("/legal-entities/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) => await svc.GetLegalEntityAsync(id, ct));
        g.MapPost("/legal-entities", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalEntityCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created($"{HrmPrefix}/admin/legal-entities/{request.Code}", await svc.CreateLegalEntityAsync(request, ct));
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
        var g = app.MapGroup($"{HrmPrefix}/recruitment").RequireAuthorization();
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
        g.MapPost("/vacancies/{id:guid}/publish", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.PublishVacancyAsync(id, ct));
        g.MapPost("/vacancies/{id:guid}/close", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.CloseVacancyAsync(id, ct));
        g.MapPost("/offers/{id:guid}/accept", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OfferAcceptRequest>(http, ct) ?? new OfferAcceptRequest();
            return Results.Ok(await svc.AcceptOfferAsync(id, request, ct));
        });
        g.MapPost("/offers/{id:guid}/issue", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.IssueOfferAsync(id, ct));
    }

    public static void RegisterRelations(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/relations").RequireAuthorization();
        g.MapGet("/cases", async ([FromQuery] string? category, IRelationsService svc, CancellationToken ct) =>
            await svc.ListCasesAsync(category, ct));
        g.MapPost("/cases", async (HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCaseAsync(request, ct));
        });
        g.MapPatch("/cases/{id:guid}", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseUpdate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateCaseAsync(id, request, ct));
        });
    }

    public static void RegisterDocuments(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/documents").RequireAuthorization();
        g.MapGet("/worker/{workerId:guid}", async (Guid workerId, IDocumentsService svc, CancellationToken ct) =>
            await svc.ListDocumentsAsync(workerId, ct));
        g.MapPost("/upload", async (HttpContext http, IDocumentsService svc, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault(f => f.Name.Equals("file", StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException("bad-request", "No file uploaded in the 'file' part.");
            if (file.Length == 0)
                throw new DomainException("bad-request", "Uploaded file is empty.");
            var workerId = Guid.Parse(form["workerId"].ToString());
            var category = form["category"].ToString();
            var title = form["title"].ToString();
            var storageDir = Path.Combine(Path.GetTempPath(), "erp-docs");
            Directory.CreateDirectory(storageDir);
            var storagePath = Path.Combine(storageDir, $"{Guid.NewGuid():N}-{file.FileName}");
            await using (var fs = File.Create(storagePath))
                await file.CopyToAsync(fs, ct);
            return Results.Created("", await svc.UploadDocumentAsync(
                workerId, category, title, file.FileName, file.ContentType ?? "application/octet-stream",
                file.Length, storagePath, ct));
        });
        g.MapGet("/{id:guid}/download", async (Guid id, IDocumentsService svc, CancellationToken ct) =>
        {
            var (doc, stream) = await svc.GetDocumentStreamAsync(id, ct);
            return Results.File(stream, doc.ContentType, doc.FileName);
        });
        var reports = app.MapGroup($"{HrmPrefix}/reports").RequireAuthorization();
        reports.MapGet("/", async ([AsParameters] ReportQuery query, IDocumentsService svc, CancellationToken ct) =>
            await svc.GetReportAsync(query, ct));
    }

    public static void RegisterDq(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/dq").RequireAuthorization();
        g.MapGet("/checks", async (IDqService svc, CancellationToken ct) => await svc.RunChecksAsync(ct));
    }

    public static void RegisterStatutory(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/statutory-exports").RequireAuthorization();
        g.MapGet("/", async ([AsParameters] StatutoryExportQuery q, IStatutoryExportService svc, CancellationToken ct) =>
        {
            var file = await svc.GenerateAsync(q.ExportType, q.PeriodId, ct);
            var bytes = await File.ReadAllBytesAsync(file, ct);
            File.Delete(file);
            return Results.File(bytes, "text/csv", $"{q.ExportType}-{q.PeriodId:N}.csv");
        });
        // M23: aggregate statutory liability summary (PAYE/NAPSA/NHIMA totals)
        // for the reports UI — totals visible without downloading a file.
        g.MapGet("/summary", async (Guid periodId, IStatutoryExportService svc, CancellationToken ct) =>
            Results.Ok(await svc.SummaryAsync(periodId, ct)));
    }
}

// Route-local binding types.
public sealed record PayrollRunApprovalNote(string? Note);
public sealed record StatutoryExportQuery(string ExportType, Guid PeriodId);
/// <summary>Current API version resolved from the URL path by Program.cs.</summary>
public sealed class ApiVersioning
{
    public int CurrentVersion { get; set; }
}
