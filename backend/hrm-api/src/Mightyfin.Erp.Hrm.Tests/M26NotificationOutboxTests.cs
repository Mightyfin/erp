using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Payroll;
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
}
