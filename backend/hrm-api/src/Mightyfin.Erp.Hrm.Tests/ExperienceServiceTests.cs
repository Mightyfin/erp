using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Fake merge-data provider returning a deterministic snapshot.</summary>
internal sealed class FakeMergeDataProvider : IMergeDataProvider
{
    public Task<LetterMergeData> GetMergeDataAsync(Guid workerId, string letterType, CancellationToken ct) =>
        Task.FromResult(new LetterMergeData(
            "Dev Operator", "DEV-001", "HR Administrator", "A",
            new DateOnly(2026, 1, 4), "Zambia Mining Ltd", 12500.00m, "HRM/DEV-001"));
}

/// <summary>M4: HR requests (HRM-052) and HR letters with template merge (UI-XPR-002).</summary>
public class ExperienceServiceTests
{
    private static HrmDbContext Ctx() => TestDbContextFactory.Create("test-tenant");

    private static ExperienceServiceImpl NewService(HrmDbContext ctx) =>
        new(new ExperienceRepository(ctx), new PermissiveAuthz(),
            new WorkflowServiceImpl(new WorkflowRepository(ctx), new PermissiveAuthz(), new NoOpEffects()),
            new LetterTemplatesImpl(), new FakeMergeDataProvider());

    private static Worker SeedWorker(HrmDbContext ctx)
    {
        var w = new Worker
        {
            TenantId = "test-tenant",
            EmployeeNo = "EMP-TEST",
            FirstName = "Test",
            LastName = "Worker",
            Status = "active",
            WorkerType = "employee",
        };
        ctx.Workers.Add(w);
        ctx.SaveChanges();
        return w;
    }

    [Fact]
    public async Task CreateRequestAsync_SetsWorkerIdAndOpensWorkflow()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);

        var result = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("benefits", "Health plan query", "Which plan covers dependents?"), CancellationToken.None);

        Assert.Equal(worker.Id, result.WorkerId);
        Assert.Equal("open", result.Status);
        var workflow = await ctx.WorkflowRequests.FirstOrDefaultAsync(w => w.WorkflowType == "hr-request");
        Assert.NotNull(workflow);
        Assert.Equal("hr-request", workflow.WorkflowType);
    }

    [Fact]
    public async Task AddMessageAsync_SetsCorrectFromFieldAndTransitionsStatus()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);
        var request = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("benefits", "Plan query", "Body"), CancellationToken.None);

        await svc.AddMessageAsync(request.Id, null, "hr_ops",
            new HrRequestMessageCreate("We'll check your coverage.", false), CancellationToken.None);

        var msg = (await ctx.HrRequestMessages
            .Where(m => m.RequestId == request.Id).ToListAsync())
            .OrderBy(m => m.CreatedAt).Last();
        Assert.Equal("hr", msg.From);
        var refreshed = await ctx.HrRequests.FirstAsync(r => r.Id == request.Id);
        Assert.Equal("in-progress", refreshed.Status);
    }

    [Fact]
    public async Task ResolveRequestAsync_TogglesBetweenResolvedAndClosed()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);
        var request = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("benefits", "Query", "Body"), CancellationToken.None);

        var resolved = await svc.ResolveRequestAsync(request.Id, CancellationToken.None);
        Assert.Equal("resolved", resolved.Status);

        var closed = await svc.ResolveRequestAsync(request.Id, CancellationToken.None);
        Assert.Equal("closed", closed.Status);
    }

    [Fact]
    public async Task CreateLetterAsync_MergesTemplateBodyForApprovalTypes()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);

        var result = await svc.CreateLetterAsync(worker.Id,
            new HrLetterCreate("salary-confirmation", "Bank", "Visa application"), CancellationToken.None);

        Assert.Equal("pending-approval", result.Status);
        Assert.NotNull(result.VerificationCode);
        Assert.Contains("DEV-001", result.TemplateBody ?? "");
        Assert.Contains("Zambia Mining Ltd", result.TemplateBody ?? "");
    }

    [Fact]
    public async Task ApproveLetterAsync_MarksGeneratedWithFinalRender()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);
        var letter = await svc.CreateLetterAsync(worker.Id,
            new HrLetterCreate("salary-confirmation", "Bank", "Loan application"), CancellationToken.None);

        var approved = await svc.ApproveLetterAsync(letter.Id, CancellationToken.None);

        Assert.Equal("generated", approved.Status);
        Assert.Contains("12,500.00", approved.TemplateBody ?? "");
    }

    // ---- M22: HR requests inbox round-trip ----

    [Fact]
    public async Task ListRequestsAsync_IncludesMessagesAndHonoursStatusFilter()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);
        var open = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("payroll", "Payslip query", "Body"), CancellationToken.None);
        var resolved = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("payroll", "Old query", "Body"), CancellationToken.None);
        await svc.ResolveRequestAsync(resolved.Id, CancellationToken.None);
        await svc.AddMessageAsync(open.Id, null, "hr_ops",
            new HrRequestMessageCreate("We're on it.", false), CancellationToken.None);

        // An HR reply transitions the open case to in-progress, so the inbox
        // filter by status must match the transitioned value.
        var openPage = await svc.ListRequestsAsync(null, "open", CancellationToken.None);
        var progressPage = await svc.ListRequestsAsync(null, "in-progress", CancellationToken.None);
        var resolvedPage = await svc.ListRequestsAsync(null, "resolved", CancellationToken.None);

        Assert.Empty(openPage.Items);
        Assert.Single(progressPage.Items);
        Assert.Equal(open.Id, progressPage.Items[0].Id);
        Assert.Single(progressPage.Items[0].Messages);
        Assert.Single(resolvedPage.Items);
        Assert.Equal(resolved.Id, resolvedPage.Items[0].Id);
    }

    [Fact]
    public async Task AddMessageAsync_FromEmployeeOnAwaitingEmployee_MovesToInProgress()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);
        var request = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("benefits", "Coverage", "Body"), CancellationToken.None);
        // HR replies, opening the case toward the employee.
        await svc.AddMessageAsync(request.Id, null, "hr_ops",
            new HrRequestMessageCreate("Please confirm your plan tier.", false), CancellationToken.None);
        // Flip the status from a fresh context so the service's next call (which
        // tracks its own query results) is not fighting a stale tracked instance.
        // Re-use the same tracked instance: after the HR reply the request's
        // tracked state is Unchanged, so a single SaveChanges here updates only
        // the status column (no EF Core 10 state-propagation conflict with the
        // first message's child row).
        var tracked = await ctx.HrRequests.Include(r => r.Messages)
            .FirstAsync(r => r.Id == request.Id);
        tracked.Status = "awaiting-employee";
        await ctx.SaveChangesAsync();

        var updated = await svc.AddMessageAsync(request.Id, worker.Id, "employee",
            new HrRequestMessageCreate("Here is the code: 4421.", false), CancellationToken.None);

        Assert.Equal("in-progress", updated.Status);
        // Verify through the shared tracked context: each Ctx() call builds a
        // fresh isolated in-memory database, so only this ctx sees the rows.
        Assert.Equal(2, ctx.HrRequestMessages.Count(m => m.RequestId == request.Id));
    }

    [Fact]
    public async Task HrRequestDto_CarriesWorkerNameForKnownWorkers()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);

        var result = await svc.CreateRequestAsync(worker.Id,
            new HrRequestCreate("benefits", "Coverage question", "Body"), CancellationToken.None);

        Assert.Equal("open", result.Status);
        Assert.Equal(worker.Id, result.WorkerId);
        Assert.Equal("Test Worker", result.WorkerName);
    }

    [Fact]
    public async Task CreateLetterAsync_DraftTypeStoresPurposeAsBody()
    {
        var ctx = Ctx();
        var svc = NewService(ctx);
        var worker = SeedWorker(ctx);
        var result = await svc.CreateLetterAsync(worker.Id,
            new HrLetterCreate("custom", "A partner", "Attendance verification for March"), CancellationToken.None);

        Assert.Equal("draft", result.Status);
        Assert.Contains("Attendance verification for March", result.TemplateBody ?? "");
    }
}
