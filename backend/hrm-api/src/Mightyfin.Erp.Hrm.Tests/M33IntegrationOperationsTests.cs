using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M33IntegrationOperationsTests
{
    [Fact]
    public async Task FinancePosting_IsBalancedAuditableAndIdempotent()
    {
        await using var db = TestDbContextFactory.Create("m33-finance");
        var run = await SeedRunAsync(db, "released");
        var service = Service(db);

        var first = await service.CreateFinancePostingAsync(run.Id, "payroll-user", CancellationToken.None);
        var second = await service.CreateFinancePostingAsync(run.Id, "payroll-user", CancellationToken.None);
        var file = await service.DownloadAsync(first.Id, CancellationToken.None);
        using var payload = JsonDocument.Parse(file.Payload);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("ready", first.Status);
        Assert.Equal(1450m, payload.RootElement.GetProperty("controlTotals").GetProperty("debit").GetDecimal());
        Assert.Equal(1450m, payload.RootElement.GetProperty("controlTotals").GetProperty("credit").GetDecimal());
        Assert.Single(await db.IntegrationOperations.ToListAsync());
        Assert.Single(await db.OutboxMessages.Where(x => x.EventType == HrmEventTypes.IntegrationReady).ToListAsync());
    }

    [Fact]
    public async Task FinancePosting_RejectsUnreleasedPayroll()
    {
        await using var db = TestDbContextFactory.Create("m33-finance-gate");
        var run = await SeedRunAsync(db, "approved");
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            Service(db).CreateFinancePostingAsync(run.Id, "payroll-user", CancellationToken.None));
        Assert.Equal("integration-source-not-ready", error.Code);
    }

    [Fact]
    public async Task PaymentHandoff_RequiresReleasedPaymentAndPrimaryBankDetails()
    {
        await using var db = TestDbContextFactory.Create("m33-payment");
        var run = await SeedRunAsync(db, "released");
        run.PaymentStatus = "released";
        run.PaymentFileReference = "PAY-2026-06";
        await db.SaveChangesAsync();

        var missing = await Assert.ThrowsAsync<DomainException>(() =>
            Service(db).CreatePaymentHandoffAsync(run.Id, "treasury", CancellationToken.None));
        Assert.Equal("payment-bank-details-missing", missing.Code);

        var workerId = await db.PayrollRunLines.Select(x => x.WorkerId).SingleAsync();
        db.WorkerBankDetails.Add(new WorkerBankDetail
        {
            WorkerId = workerId, BankName = "Test Bank", BranchCode = "001",
            AccountName = "Test Worker", AccountNumber = "123456789", IsPrimary = true,
        });
        await db.SaveChangesAsync();
        var operation = await Service(db).CreatePaymentHandoffAsync(run.Id, "treasury", CancellationToken.None);
        var file = await Service(db).DownloadAsync(operation.Id, CancellationToken.None);
        Assert.Equal("text/csv", file.ContentType);
        Assert.Contains("123456789", file.Payload);
        Assert.Contains("1200.00", file.Payload);
    }

    [Fact]
    public async Task ReconciliationFailure_CanBeRetriedWithSameIdempotencyKey()
    {
        await using var db = TestDbContextFactory.Create("m33-reconcile");
        var worker = new Worker
        {
            EmployeeNo = "ID-001", FirstName = "Identity", LastName = "Worker",
            Status = "active", WorkerType = "employee", SubjectId = "subject-1",
        };
        db.Workers.Add(worker);
        await db.SaveChangesAsync();
        var service = Service(db);
        var operation = await service.CreateIdentitySyncAsync(new IdentitySyncRequest("full"), "identity-admin", CancellationToken.None);

        var failed = await service.ReconcileAsync(operation.Id,
            new IntegrationReconciliationRequest("failed", "IDP-BATCH-1", "Provider timeout"),
            "identity-admin", CancellationToken.None);
        Assert.Equal("failed", failed.Status);
        var retried = await service.RetryAsync(operation.Id, "identity-admin", CancellationToken.None);
        Assert.Equal("ready", retried.Status);
        Assert.Equal(operation.IdempotencyKey, retried.IdempotencyKey);
        Assert.Equal(1, retried.AttemptCount);
        Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.EventType == HrmEventTypes.IntegrationReady));
    }

    [Fact]
    public async Task NapsaHandoff_CarriesEmployerAndWorkerIdentityDetail()
    {
        await using var db = TestDbContextFactory.Create("m33-napsa");
        var run = await SeedRunAsync(db, "released");
        var worker = await db.Workers.SingleAsync();
        worker.Nrc = "123456/78/9";
        worker.NapsaNumber = "NAPSA-WORKER-1";
        db.LegalEntities.Add(new LegalEntity
        {
            Code = "TEST", RegisteredName = "Test Employer", IsDefault = true,
            NapsaEmployerRef = "NAPSA-EMPLOYER-1",
        });
        var line = await db.PayrollRunLines.SingleAsync();
        db.PayrollLineComponents.AddRange(
            new PayrollLineComponent
            {
                RunLineId = line.Id, ComponentCode = "NAPSA-EE", ComponentName = "NAPSA employee",
                ComponentType = "deduction", Amount = 70m, IsStatutory = true,
            },
            new PayrollLineComponent
            {
                RunLineId = line.Id, ComponentCode = "NAPSA-ER", ComponentName = "NAPSA employer",
                ComponentType = "employer-contribution", Amount = 70m, IsStatutory = true,
            });
        await db.SaveChangesAsync();

        var operation = await Service(db).CreateStatutoryHandoffAsync(
            new StatutoryHandoffRequest("napsa", run.PayPeriodId), "payroll-user", CancellationToken.None);
        var file = await Service(db).DownloadAsync(operation.Id, CancellationToken.None);

        Assert.Contains("NAPSA-EMPLOYER-1", file.Payload);
        Assert.Contains("123456/78/9", file.Payload);
        Assert.Contains("NAPSA-WORKER-1", file.Payload);
        Assert.Contains("70.00,70.00,140.00", file.Payload);
    }

    private static IntegrationOperationsService Service(HrmDbContext db)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HRM:DocumentStorage:Provider"] = "test-object-storage",
        }).Build();
        return new IntegrationOperationsService(db, new PermissiveAuthz(), new TestOutboxWriter(db), new EfUnitOfWork(db), config);
    }

    private static async Task<PayrollRun> SeedRunAsync(HrmDbContext db, string status)
    {
        var worker = new Worker { EmployeeNo = "PAY-001", FirstName = "Test", LastName = "Worker", WorkerType = "employee", Status = "active" };
        var group = new PayGroup { Code = "MONTHLY", Name = "Monthly", Currency = "ZMW" };
        var period = new PayPeriod
        {
            PayGroupId = group.Id, PayGroup = group, PeriodLabel = "June 2026",
            StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 30),
            CutoffDate = new DateOnly(2026, 6, 25), PayDate = new DateOnly(2026, 6, 30),
        };
        var run = new PayrollRun
        {
            PayGroupId = group.Id, PayPeriodId = period.Id, PayPeriod = period, Status = status,
            EmployeeCount = 1, TotalGross = 1400m, TotalDeductions = 200m,
            TotalNet = 1200m, TotalEmployerCost = 1450m,
        };
        var line = new PayrollRunLine
        {
            RunId = run.Id, Run = run, WorkerId = worker.Id, Worker = worker,
            GrossPay = 1400m, TotalDeductions = 200m, NetPay = 1200m, EmployerCost = 1450m,
        };
        db.AddRange(worker, group, period, run, line);
        await db.SaveChangesAsync();
        return run;
    }

    private sealed class TestOutboxWriter(HrmDbContext db) : IOutboxWriter
    {
        public async Task<OutboxMessage> EnqueueAsync(string eventType, string subjectId, object payload, CancellationToken ct)
        {
            var row = new OutboxMessage
            {
                PublicId = $"evt_{Guid.NewGuid():N}", EventType = eventType, SubjectId = subjectId,
                CorrelationId = $"test_{Guid.NewGuid():N}", PayloadJson = JsonSerializer.Serialize(payload),
            };
            db.OutboxMessages.Add(row);
            await db.SaveChangesAsync(ct);
            return row;
        }
    }
}
