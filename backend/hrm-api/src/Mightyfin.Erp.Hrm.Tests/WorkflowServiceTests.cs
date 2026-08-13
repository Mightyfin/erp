using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>No-op effects applier for workflow unit tests.</summary>
internal sealed class NoOpEffects : ILeaveEffectApplier
{
    public Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>M4: generic workflow engine (routing, delegation, escalation, work queue).</summary>
public class WorkflowServiceTests
{
    private static (WorkflowServiceImpl service, HrmDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create("test-tenant");
        var wfRepo = new WorkflowRepository(ctx);
        var svc = new WorkflowServiceImpl(wfRepo, new PermissiveAuthz(), new NoOpEffects());
        return (svc, ctx);
    }

    private static Worker SeedWorker(HrmDbContext ctx, Guid id, string name, Guid? managerId = null)
    {
        var w = new Worker
        {
            Id = id,
            TenantId = "test-tenant",
            EmployeeNo = "EMP-" + id.ToString()[..6],
            FirstName = name.Split(' ')[0],
            LastName = name.Split(' ').Skip(1).FirstOrDefault() ?? "",
            Status = "active",
            WorkerType = "employee",
            ManagerId = managerId,
        };
        ctx.Workers.Add(w);
        ctx.SaveChanges();
        return w;
    }

    [Fact]
    public async Task OpenAsync_RoutesToManagerOfSubject()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");
        var bob = SeedWorker(ctx, Guid.NewGuid(), "Bob Manager");
        alice.ManagerId = bob.Id;
        ctx.SaveChanges();

        var request = await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);

        Assert.Equal("in-review", request.Status);
        Assert.Equal(bob.Id, request.CurrentApproverId);
    }

    [Fact]
    public async Task OpenAsync_RoutesToHrQueueWhenNoManager()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");

        var request = await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);

        Assert.Equal("submitted", request.Status);
        Assert.Null(request.CurrentApproverId); // HR ops queue
    }

    [Fact]
    public async Task OpenAsync_HonorsActiveDelegation()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");
        var bob = SeedWorker(ctx, Guid.NewGuid(), "Bob Manager");
        var carol = SeedWorker(ctx, Guid.NewGuid(), "Carol Delegate");
        alice.ManagerId = bob.Id;
        ctx.SaveChanges();

        ctx.ApprovalDelegations.Add(new ApprovalDelegation
        {
            TenantId = "test-tenant",
            DelegatorId = bob.Id,
            DelegateWorkerId = carol.Id,
            IsActive = true,
            FromDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date).AddDays(-1),
            Scope = "leave",
        });
        ctx.SaveChanges();

        var request = await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);

        Assert.Equal("in-review", request.Status);
        Assert.Equal(carol.Id, request.CurrentApproverId);
    }

    [Fact]
    public async Task DecideAsync_DelegateCanActForDelegator()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");
        var bob = SeedWorker(ctx, Guid.NewGuid(), "Bob Manager");
        var carol = SeedWorker(ctx, Guid.NewGuid(), "Carol Delegate");
        alice.ManagerId = bob.Id;
        ctx.SaveChanges();

        ctx.ApprovalDelegations.Add(new ApprovalDelegation
        {
            TenantId = "test-tenant",
            DelegatorId = bob.Id,
            DelegateWorkerId = carol.Id,
            IsActive = true,
            FromDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date).AddDays(-1),
        });
        ctx.SaveChanges();

        var request = await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);
        var decided = await svc.DecideAsync(request.Id, carol.Id, new WorkflowDecisionRequest("approve"), CancellationToken.None);

        Assert.Equal("approved", decided.Status);
        Assert.Single(decided.Decisions);
        Assert.Contains(decided.Decisions, d => d.Action == "approve");
    }

    [Fact]
    public async Task DecideAsync_ReturnSendsBackToRequester()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");
        var bob = SeedWorker(ctx, Guid.NewGuid(), "Bob Manager");
        alice.ManagerId = bob.Id;
        ctx.SaveChanges();

        var request = await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);
        var decided = await svc.DecideAsync(request.Id, bob.Id,
            new WorkflowDecisionRequest("return", "Please add a comment first."), CancellationToken.None);

        Assert.Equal("returned", decided.Status);
        Assert.Equal(alice.Id, decided.CurrentApproverId);
    }

    [Fact]
    public async Task EscalateAsync_ReassignsToApproversManager()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");
        var bob = SeedWorker(ctx, Guid.NewGuid(), "Bob Manager");
        var ceo = SeedWorker(ctx, Guid.NewGuid(), "Ceo Boss");
        alice.ManagerId = bob.Id;
        bob.ManagerId = ceo.Id;
        ctx.SaveChanges();

        var request = await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);
        var escalated = await svc.EscalateAsync(request.Id, ceo.Id, CancellationToken.None);

        // escalation reassigns: the returned workflow is now awaiting the new approver
        Assert.Equal("in-review", escalated.Status);
        Assert.Equal(ceo.Id, escalated.CurrentApproverId);
        Assert.True(escalated.EscalatedAt.HasValue);
    }

    [Fact]
    public async Task GetWorkQueueAsync_ResolvesNames()
    {
        var (svc, ctx) = Build();
        var alice = SeedWorker(ctx, Guid.NewGuid(), "Alice Subject");
        var bob = SeedWorker(ctx, Guid.NewGuid(), "Bob Manager");
        alice.ManagerId = bob.Id;
        ctx.SaveChanges();

        await svc.OpenAsync("leave", alice.Id, alice.Id, "{}", CancellationToken.None);

        var queue = await svc.GetWorkQueueAsync(CancellationToken.None);

        Assert.Equal(1, queue.TotalCount);
        Assert.Equal("Bob Manager", queue.Items[0].CurrentApproverName);
        Assert.Equal("Alice Subject", queue.Items[0].SubjectName);
    }
}
