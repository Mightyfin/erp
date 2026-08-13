using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

// ---------------------------------------------------------------------------
// M8 — Documents, data-quality engine, statutory exports and HR reports.
// ---------------------------------------------------------------------------

public sealed class DocumentsTests
{
    private static DocumentsServiceImpl CreateService(HrmDbContext db) =>
        new(new DocumentsRepository(db), new ConfigRepository(db), new PermissiveAuthz());

    private static async Task<Guid> SeedWorker(HrmDbContext db)
    {
        var w = new Worker { EmployeeNo = "DQC", FirstName = "Doc", LastName = "Worker", Status = "active", OrgUnitId = null, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.Workers.Add(w);
        await db.SaveChangesAsync();
        return w.Id;
    }

    [Fact]
    public async Task Upload_InvalidCategory_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var workerId = await SeedWorker(db);
        var svc = CreateService(db);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.UploadDocumentAsync(workerId, "passport", "Title", "f.pdf", "application/pdf", 100, "/tmp/f", CancellationToken.None));
        Assert.Equal("document-invalid-category", ex.Code);
    }

    [Fact]
    public async Task Upload_InvalidContentType_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var workerId = await SeedWorker(db);
        var svc = CreateService(db);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.UploadDocumentAsync(workerId, "contract", "Title", "f.exe", "application/x-msdownload", 100, "/tmp/f", CancellationToken.None));
        Assert.Equal("document-invalid-content-type", ex.Code);
    }

    [Fact]
    public async Task Upload_OversizedFile_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var workerId = await SeedWorker(db);
        var svc = CreateService(db);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.UploadDocumentAsync(workerId, "contract", "Title", "f.pdf", "application/pdf", 26 * 1024 * 1024, "/tmp/f", CancellationToken.None));
        Assert.Equal("document-too-large", ex.Code);
    }

    [Fact]
    public async Task Upload_ValidFile_CreatesDocument()
    {
        using var db = TestDbContextFactory.Create();
        var workerId = await SeedWorker(db);
        var svc = CreateService(db);
        var dto = await svc.UploadDocumentAsync(workerId, "qualification", "Degree", "degree.pdf", "application/pdf", 1024, "/tmp/degree.pdf", CancellationToken.None);
        Assert.Equal("qualification", dto.Category);
        Assert.Equal("degree.pdf", dto.FileName);
        Assert.Equal("internal", dto.Classification);
    }

    [Fact]
    public async Task GetDocumentStream_MissingOnDisk_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var workerId = await SeedWorker(db);
        var svc = CreateService(db);
        var dto = await svc.UploadDocumentAsync(workerId, "id", "NRC", "nrc.pdf", "application/pdf", 100, "/nonexistent/nrc.pdf", CancellationToken.None);
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.GetDocumentStreamAsync(dto.Id, CancellationToken.None));
        Assert.Equal("document-missing", ex.Code);
    }

    [Fact]
    public async Task Report_Headcount_ReturnsActiveWorkerCount()
    {
        using var db = TestDbContextFactory.Create();
        await db.Workers.AddAsync(new Worker { EmployeeNo = "W1", FirstName = "A", LastName = "B", Status = "active", OrgUnitId = null, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.Workers.AddAsync(new Worker { EmployeeNo = "W2", FirstName = "C", LastName = "D", Status = "terminated", OrgUnitId = null, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        var report = await svc.GetReportAsync(new ReportQuery("headcount"), CancellationToken.None);
        Assert.Equal("headcount", report.ReportType);
        Assert.Equal(1, report.Summary["total_active"]);
    }

    [Fact]
    public async Task Report_UnknownType_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);
        await Assert.ThrowsAsync<DomainException>(() => svc.GetReportAsync(new ReportQuery("attendance"), CancellationToken.None));
    }
}

public sealed class DqTests
{
    private static DqServiceImpl CreateService(HrmDbContext db) =>
        new(new ConfigRepository(db), new DocumentsRepository(db), new PermissiveAuthz());

    [Fact]
    public async Task Completeness_FlagsActiveWorkerMissingIdentityFields()
    {
        using var db = TestDbContextFactory.Create();
        var le = new LegalEntity { Code = "ZML", RegisteredName = "Zambian Ltd", CountryCode = "ZM", PacraNumber = "123456", Currency = "ZMW" };
        db.LegalEntities.Add(le);
        var org = new OrgUnit { Code = "FIN", Name = "Finance", LegalEntityId = le.Id, Status = "active" };
        db.OrgUnits.Add(org);
        await db.SaveChangesAsync();
        await db.Workers.AddAsync(new Worker { EmployeeNo = "INC1", FirstName = "X", LastName = "Y", Status = "active", Email = null, Phone = null, Nrc = null, Tpin = null, NapsaNumber = null, NhimaNumber = null, OrgUnitId = null, StartDate = null });
        await db.Workers.AddAsync(new Worker { EmployeeNo = "OK1", FirstName = "Z", LastName = "Q", Status = "active", Email = "z@example.com", Phone = "+260970000001", Nrc = "123456/78/1", Tpin = "1234567890", NapsaNumber = "1234567", NhimaNumber = "NH123", OrgUnitId = org.Id, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        var results = await svc.RunChecksAsync(CancellationToken.None);
        var flags = results.Where(r => r.Rule == "completeness").ToList();
        Assert.Single(flags);
        Assert.Contains("email", flags[0].Detail);
        Assert.Contains("napsa_number", flags[0].Detail);
        // org_unit and start_date are also flagged because the seeded incomplete
        // worker has no assignment and no start date — that is correct behaviour.
        Assert.Contains("org_unit", flags[0].Detail);
        Assert.Contains("start_date", flags[0].Detail);
        // the well-formed worker must never be flagged
        Assert.DoesNotContain(flags, f => f.WorkerId != flags[0].WorkerId);
    }

    [Fact]
    public async Task Duplicates_DetectsSharedEmailAndNrc()
    {
        using var db = TestDbContextFactory.Create();
        await db.Workers.AddAsync(new Worker { EmployeeNo = "D1", FirstName = "A", LastName = "B", Status = "active", Email = "same@example.com", Phone = "+260970000002", Nrc = "111111/11/1", Tpin = null, NapsaNumber = null, NhimaNumber = null, OrgUnitId = null, StartDate = null });
        await db.Workers.AddAsync(new Worker { EmployeeNo = "D2", FirstName = "C", LastName = "D", Status = "active", Email = "same@example.com", Phone = "+260970000003", Nrc = "111111/11/1", Tpin = null, NapsaNumber = null, NhimaNumber = null, OrgUnitId = null, StartDate = null });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        var results = await svc.RunChecksAsync(CancellationToken.None);
        Assert.Equal(4, results.Count(r => r.Rule == "duplicate-email" || r.Rule == "duplicate-nrc"));
        Assert.Equal(2, results.Count(r => r.Rule == "duplicate-email"));
        Assert.Equal(2, results.Count(r => r.Rule == "duplicate-nrc"));
        Assert.All(results.Where(r => r.Rule == "duplicate-email"), r => Assert.Equal("high", r.Severity));
    }

    [Fact]
    public async Task Expiry_FlagsDocumentsExpiringWithin90Days()
    {
        using var db = TestDbContextFactory.Create();
        var soon = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var far = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(400));
        var worker = new Worker { EmployeeNo = "EXP", FirstName = "E", LastName = "X", Status = "active", OrgUnitId = null, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.Workers.Add(worker);
        await db.SaveChangesAsync();
        await db.WorkerDocuments.AddAsync(new WorkerDocument { WorkerId = worker.Id, Category = "medical", Title = "Fitness", FileName = "m.pdf", ContentType = "application/pdf", SizeBytes = 100, StoragePath = "/tmp/m.pdf", ExpiryDate = soon });
        await db.WorkerDocuments.AddAsync(new WorkerDocument { WorkerId = worker.Id, Category = "medical", Title = "Old Fitness", FileName = "o.pdf", ContentType = "application/pdf", SizeBytes = 100, StoragePath = "/tmp/o.pdf", ExpiryDate = far });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        var results = await svc.RunChecksAsync(CancellationToken.None);
        var exp = results.Where(r => r.Rule == "document-expiring").ToList();
        Assert.Single(exp);
        Assert.Equal("low", exp[0].Severity);
    }

    [Fact]
    public async Task TerminatedWorkersAreNotCheckedForCompleteness()
    {
        using var db = TestDbContextFactory.Create();
        await db.Workers.AddAsync(new Worker { EmployeeNo = "ARC", FirstName = "A", LastName = "B", Status = "terminated", Email = null, Phone = null, Nrc = null, Tpin = null, NapsaNumber = null, NhimaNumber = null, OrgUnitId = null, StartDate = null });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        var results = await svc.RunChecksAsync(CancellationToken.None);
        Assert.DoesNotContain(results, r => r.Rule == "completeness");
    }
}

public sealed class StatutoryExportTests
{
    private static Guid _periodId;
    private static Guid PeriodId => _periodId != Guid.Empty ? _periodId : Guid.NewGuid();

    private static async Task<StatutoryExportServiceImpl> CreateService(HrmDbContext db)
    {
        var payGroup = new PayGroup { Code = "M", Name = "Monthly", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 28, IsDefault = true };
        db.PayGroups.Add(payGroup);
        var period = new PayPeriod { Id = _periodId == Guid.Empty ? Guid.NewGuid() : _periodId, PayGroupId = payGroup.Id, PeriodLabel = "2026-08", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3).AddDays(30)), CutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)), PayDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)), Status = "locked" };
        _periodId = period.Id;
        db.PayPeriods.Add(period);
        var worker = new Worker { EmployeeNo = "SMK001", FirstName = "George", LastName = "Mung'amba", Status = "active", Email = "g@example.com", Phone = "+260970000001", Nrc = "123456/78/1", Tpin = "1234567890", NapsaNumber = "1234567", NhimaNumber = "NH123", OrgUnitId = null, StartDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.Workers.Add(worker);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Group/period/worker save failed: " + ex.GetBaseException().Message, ex);
        }

        // EF Core 10 state-propagation change: add children explicitly with FKs set manually.
        var run = new PayrollRun { PayPeriodId = PeriodId, PayGroupId = payGroup.Id, Status = "released", TotalGross = 10000, TotalDeductions = 1200, TotalNet = 8800 };
        db.PayrollRuns.Add(run);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Run save failed: " + ex.GetBaseException().Message, ex);
        }
        var line = new PayrollRunLine { RunId = run.Id, WorkerId = worker.Id, GrossPay = 10000, TotalDeductions = 1200, NetPay = 8800 };
        db.PayrollRunLines.Add(line);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Line save failed: " + ex.GetBaseException().Message, ex);
        }
        line = db.PayrollRunLines.Single(l => l.RunId == run.Id);
        foreach (var comp in new[]
        {
            new PayrollLineComponent { RunLineId = line.Id, ComponentCode = "napsa-ee", ComponentName = "NAPSA Employee", ComponentType = "deduction", Amount = 330.5m, IsStatutory = true },
            new PayrollLineComponent { RunLineId = line.Id, ComponentCode = "napsa-er", ComponentName = "NAPSA Employer", ComponentType = "employer-contribution", Amount = 330.5m, IsStatutory = true },
            new PayrollLineComponent { RunLineId = line.Id, ComponentCode = "nhima-ee", ComponentName = "NHIMA Employee", ComponentType = "deduction", Amount = 100m, IsStatutory = true },
            new PayrollLineComponent { RunLineId = line.Id, ComponentCode = "nhima-er", ComponentName = "NHIMA Employer", ComponentType = "employer-contribution", Amount = 100m, IsStatutory = true },
            new PayrollLineComponent { RunLineId = line.Id, ComponentCode = "paye", ComponentName = "PAYE", ComponentType = "tax", Amount = 670m, IsStatutory = true },
        })
            db.Set<PayrollLineComponent>().Add(comp);
        db.SaveChanges();

        return new StatutoryExportServiceImpl(new PayrollRepository(db), new PermissiveAuthz());
    }

    [Fact]
    public async Task Zra_Export_ContainsPayeAndNetColumns()
    {
        using var db = TestDbContextFactory.Create();
        var svc = await CreateService(db);
        var file = await svc.GenerateAsync("zra", PeriodId, CancellationToken.None);
        var csv = await File.ReadAllTextAsync(file);
        File.Delete(file);
        Assert.Contains("SMK001", csv);
        Assert.Contains("George Mung'amba", csv);
        Assert.Contains("1234567890", csv);
        Assert.Contains("670", csv);
        Assert.Contains("8800", csv);
    }

    [Fact]
    public async Task Napsa_Export_ContainsEeErAmounts()
    {
        using var db = TestDbContextFactory.Create();
        var svc = await CreateService(db);
        var file = await svc.GenerateAsync("napsa", PeriodId, CancellationToken.None);
        var csv = await File.ReadAllTextAsync(file);
        File.Delete(file);
        Assert.Contains("1234567", csv);
        Assert.Contains("330.5", csv);
        Assert.Contains("661", csv);
    }

    [Fact]
    public async Task Nhima_Export_ContainsMemberNumber()
    {
        using var db = TestDbContextFactory.Create();
        var svc = await CreateService(db);
        var file = await svc.GenerateAsync("nhima", PeriodId, CancellationToken.None);
        var csv = await File.ReadAllTextAsync(file);
        File.Delete(file);
        Assert.Contains("NH123", csv);
        Assert.Contains("100", csv);
    }

    [Fact]
    public async Task UnsupportedExportType_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var svc = await CreateService(db);
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("nsfdc", PeriodId, CancellationToken.None));
        Assert.Equal("export-not-found", ex.Code);
    }

    [Fact]
    public async Task EmptyPeriod_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new StatutoryExportServiceImpl(new PayrollRepository(db), new PermissiveAuthz());
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("zra", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("export-no-data", ex.Code);
    }
}
