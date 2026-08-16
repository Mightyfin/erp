using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M26: notification events are durable, privacy-safe, and committed
/// atomically with the business operation that produced them.</summary>
public sealed class M26NotificationOutboxTests
{
    private sealed class FakeDocumentService : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct) =>
            Task.FromResult("https://storage.example/payslip.pdf");
    }

    private sealed class ThrowingOutboxWriter : IOutboxWriter
    {
        public Task<OutboxMessage> EnqueueAsync(string eventType, string subjectId, object privacySafePayload, CancellationToken ct) =>
            throw new InvalidOperationException("simulated outbox write failure");
    }

    private static EfOutboxWriter Writer(HrmDbContext ctx) => new(
        ctx,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HRM:Environment"] = "test",
        }).Build(),
        new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "m26-test-correlation" },
        });

    private static async Task<Guid> PrepareApprovedRunAsync(PayrollServiceImpl service, Guid periodId, Guid groupId)
    {
        var run = await service.CreateRunAsync(new PayrollRunCreate(periodId, groupId), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        await service.ApproveRunAsync(run.Id, "approved for M26 test", CancellationToken.None);
        return run.Id;
    }

    private static async Task<(TimeServiceImpl Service, HrmDbContext Context, Worker Worker)> PrepareLeaveAsync(
        string databaseName, IOutboxWriter? writer = null)
    {
        var ctx = TestDbContextFactory.Create(databaseName);
        var worker = new Worker
        {
            EmployeeNo = "M26-L001", FirstName = "Leave", LastName = "Worker",
            Status = "active", WorkerType = "employee", Email = "leave@example.test",
            SubjectId = "keycloak-leave-123", TenantId = databaseName,
        };
        ctx.Workers.Add(worker);
        ctx.LeaveTypes.Add(new LeaveType
        {
            Code = "annual", Name = "Annual Leave", Category = "annual",
            DefaultDaysPerYear = 20, IsActive = true, AllowNegative = false,
            MaxConsecutiveDays = 20, RequiresEvidence = false, MinNoticeDays = 0,
            AllowsPartialDays = false, CarryForwardDays = 0, CarryForwardExpiryMonths = 0,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            TenantId = databaseName,
        });
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 20,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = databaseName,
        });
        await ctx.SaveChangesAsync();
        var workflow = new WorkflowServiceImpl(
            new WorkflowRepository(ctx), new PermissiveAuthz(), new NoOpEffects());
        var service = new TimeServiceImpl(
            new TimeRepository(ctx), new PermissiveAuthz(), workflow, new WorkerRepository(ctx),
            writer ?? Writer(ctx), new EfUnitOfWork(ctx));
        return (service, ctx, worker);
    }

    private static LeaveRequestCreate NewLeave(Guid workerId, int offset = 1) => new(
        workerId, "annual",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(offset)).ToString("yyyy-MM-dd"),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(offset + 1)).ToString("yyyy-MM-dd"),
        EvidenceAttached: true);

    [Fact]
    public async Task PayrollRelease_CommitsPrivacySafeOutboxEvent()
    {
        await using var ctx = TestDbContextFactory.Create("m26-payroll");
        var (group, _, period, profile, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        var worker = await ctx.Workers.SingleAsync(w => w.Id == profile.WorkerId);
        worker.Email = "employee@example.test";
        worker.SubjectId = "keycloak-subject-123";
        await ctx.SaveChangesAsync();

        var service = new PayrollServiceImpl(
            new PayrollRepository(ctx), new PermissiveAuthz(), new FakeDocumentService(),
            Writer(ctx), new EfUnitOfWork(ctx));
        var runId = await PrepareApprovedRunAsync(service, period.Id, group.Id);

        await service.ReleaseRunAsync(runId, CancellationToken.None);

        var message = await ctx.OutboxMessages.SingleAsync();
        Assert.Equal(HrmEventTypes.PayslipReleased, message.EventType);
        Assert.Equal("keycloak-subject-123", message.SubjectId);
        Assert.Equal("m26-test-correlation", message.CorrelationId);
        Assert.Equal("pending", message.Status);
        using var payload = JsonDocument.Parse(message.PayloadJson);
        Assert.Equal("employee@example.test", payload.RootElement.GetProperty("email").GetString());
        Assert.Equal("Jul 2026", payload.RootElement.GetProperty("period_label").GetString());
        Assert.False(payload.RootElement.TryGetProperty("gross_pay", out _));
        Assert.False(payload.RootElement.TryGetProperty("net_pay", out _));
        Assert.False(payload.RootElement.TryGetProperty("nrc", out _));
        Assert.False(payload.RootElement.TryGetProperty("tpin", out _));
    }

    [Fact]
    public async Task RequestDecision_CommitsEventWithoutRequestContent()
    {
        await using var ctx = TestDbContextFactory.Create("m26-request");
        var worker = new Worker
        {
            EmployeeNo = "M26-001", FirstName = "Test", LastName = "Employee",
            Status = "active", WorkerType = "employee", Email = "requester@example.test",
            SubjectId = "keycloak-requester-123",
        };
        ctx.Workers.Add(worker);
        await ctx.SaveChangesAsync();
        var repository = new ExperienceRepository(ctx);
        var service = new ExperienceServiceImpl(
            repository, new PermissiveAuthz(),
            new WorkflowServiceImpl(new WorkflowRepository(ctx), new PermissiveAuthz(), new NoOpEffects()),
            new LetterTemplatesImpl(), new FakeMergeDataProvider(), null,
            Writer(ctx), new EfUnitOfWork(ctx));
        const string sensitiveSubject = "Private medical request";
        const string sensitiveBody = "Sensitive employee-provided details";
        var request = await service.CreateRequestAsync(worker.Id,
            new HrRequestCreate("benefits", sensitiveSubject, sensitiveBody), CancellationToken.None);

        await service.ResolveRequestAsync(request.Id, CancellationToken.None);

        var message = await ctx.OutboxMessages.SingleAsync();
        Assert.Equal(HrmEventTypes.RequestDecided, message.EventType);
        Assert.Equal("keycloak-requester-123", message.SubjectId);
        using var payload = JsonDocument.Parse(message.PayloadJson);
        Assert.Equal("resolved", payload.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain(sensitiveSubject, message.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveBody, message.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxFailure_RollsBackPayrollReleaseAndPayslipCreation()
    {
        await using var ctx = TestDbContextFactory.Create("m26-atomicity");
        var (group, _, period, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        var service = new PayrollServiceImpl(
            new PayrollRepository(ctx), new PermissiveAuthz(), new FakeDocumentService(),
            new ThrowingOutboxWriter(), new EfUnitOfWork(ctx));
        var runId = await PrepareApprovedRunAsync(service, period.Id, group.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReleaseRunAsync(runId, CancellationToken.None));

        ctx.ChangeTracker.Clear();
        Assert.Equal("approved", (await ctx.PayrollRuns.SingleAsync(r => r.Id == runId)).Status);
        Assert.Empty(await ctx.Payslips.ToListAsync());
    }

    [Fact]
    public async Task LeaveLifecycle_CommitsThreePrivacySafeEvents()
    {
        var (service, ctx, worker) = await PrepareLeaveAsync("m26-leave-events");

        var approved = await service.CreateLeaveAsync(NewLeave(worker.Id), CancellationToken.None);
        await service.DecideLeaveAsync(approved.Id,
            new TimeDecisionRequest("approve", "private manager decision"), CancellationToken.None);
        var cancelled = await service.CreateLeaveAsync(NewLeave(worker.Id, 5), CancellationToken.None);
        await service.CancelLeaveAsync(cancelled.Id, worker.SubjectId!, CancellationToken.None);

        var messages = (await ctx.OutboxMessages.ToListAsync()).OrderBy(x => x.CreatedAt).ToList();
        Assert.Equal(4, messages.Count);
        Assert.Equal(2, messages.Count(x => x.EventType == HrmEventTypes.LeaveRequested));
        Assert.Contains(messages, x => x.EventType == HrmEventTypes.LeaveDecided);
        Assert.Contains(messages, x => x.EventType == HrmEventTypes.LeaveCancelled);
        Assert.All(messages, message =>
        {
            Assert.Equal("keycloak-leave-123", message.SubjectId);
            Assert.DoesNotContain("private manager decision", message.PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain("evidence", message.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("balance", message.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });
        using var payload = JsonDocument.Parse(messages.First(x => x.EventType == HrmEventTypes.LeaveDecided).PayloadJson);
        Assert.Equal("approved", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal("annual", payload.RootElement.GetProperty("leave_type_code").GetString());
    }

    [Fact]
    public async Task LeaveOutboxFailure_RollsBackRequestBalanceAndWorkflow()
    {
        var (service, ctx, worker) = await PrepareLeaveAsync("m26-leave-atomicity", new ThrowingOutboxWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateLeaveAsync(NewLeave(worker.Id), CancellationToken.None));

        ctx.ChangeTracker.Clear();
        Assert.Empty(await ctx.LeaveRequests.ToListAsync());
        Assert.Empty(await ctx.WorkflowRequests.ToListAsync());
        Assert.DoesNotContain(await ctx.LeaveBalanceLedgers.ToListAsync(), x => x.Reason == "request");
    }

    [Fact]
    public async Task GenericApprovalQueue_AlsoPublishesLeaveDecisionEvent()
    {
        var (timeService, ctx, worker) = await PrepareLeaveAsync("m26-generic-leave-decision");
        await timeService.CreateLeaveAsync(NewLeave(worker.Id), CancellationToken.None);
        var workflowRequest = await ctx.WorkflowRequests.SingleAsync();
        var workflowRepository = new WorkflowRepository(ctx);
        var effects = new LeaveEffectApplierImpl(
            new TimeRepository(ctx), new LetterTemplatesImpl(),
            new ExperienceRepository(ctx), new FakeMergeDataProvider(), Writer(ctx));
        var workflowService = new WorkflowServiceImpl(
            workflowRepository, new PermissiveAuthz(), effects, new EfUnitOfWork(ctx));

        await workflowService.DecideAsync(
            workflowRequest.Id, Guid.NewGuid(), new WorkflowDecisionRequest("reject", "private reason"),
            CancellationToken.None);

        var messages = await ctx.OutboxMessages.ToListAsync();
        Assert.Contains(messages, x => x.EventType == HrmEventTypes.LeaveRequested);
        var decided = Assert.Single(messages, x => x.EventType == HrmEventTypes.LeaveDecided);
        Assert.DoesNotContain("private reason", decided.PayloadJson, StringComparison.Ordinal);
        Assert.Equal("rejected", (await ctx.LeaveRequests.SingleAsync()).Status);
        Assert.DoesNotContain(await ctx.LeaveBalanceLedgers.ToListAsync(), x => x.Reason == "request");
    }

    [Fact]
    public async Task DeliveryStatus_ExcludesPayloadAndCanRetryFailedMessage()
    {
        await using var ctx = TestDbContextFactory.Create("m26-delivery-status");
        var row = await Writer(ctx).EnqueueAsync(
            HrmEventTypes.LeaveRequested, "subject-secret",
            new { email = "secret@example.test", private_note = "never expose" }, CancellationToken.None);
        row.Status = "failed";
        row.PublishAttempts = 3;
        row.LastTransport = "nats-jetstream";
        row.LastError = "temporary broker failure\ninternal trace";
        await ctx.SaveChangesAsync();
        var service = new NotificationDeliveryService(ctx, new PermissiveAuthz());

        var result = await service.ListAsync(HrmEventTypes.LeaveRequested, "failed", 50, CancellationToken.None);

        Assert.Equal(1, result.Failed);
        var delivery = Assert.Single(result.Items);
        Assert.Equal(row.PublicId, delivery.PublicId);
        Assert.DoesNotContain("secret@example.test", JsonSerializer.Serialize(delivery), StringComparison.Ordinal);
        Assert.DoesNotContain("never expose", JsonSerializer.Serialize(delivery), StringComparison.Ordinal);
        Assert.DoesNotContain('\n', delivery.LastError!);

        var retried = await service.RetryAsync(row.Id, CancellationToken.None);
        Assert.Equal("pending", retried.Status);
        Assert.Null(retried.LastError);
        Assert.Equal(3, retried.PublishAttempts);
    }
}
