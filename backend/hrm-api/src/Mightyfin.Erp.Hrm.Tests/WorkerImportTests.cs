using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public class WorkerImportTests
{
    private static WorkerImportService Build(HrmDbContext ctx)
    {
        var repo = new WorkerRepository(ctx);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        return new WorkerImportService(repo, workers, new PermissiveAuthz());
    }

    private static readonly Guid SeedLegalEntityId = Guid.NewGuid();

    private static OrgUnit NewOrgUnit(string name) => new()
    {
        Id = Guid.NewGuid(),
        Code = name.ToUpperInvariant() + "-CODE",
        Name = name,
        LegalEntityId = SeedLegalEntityId,
    };

    [Fact]
    public async Task ImportCsv_ValidRows_CreateWorkers()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.LegalEntities.Add(new LegalEntity { Id = SeedLegalEntityId, RegisteredName = "Mighty Finance", Code = "MF", CountryCode = "ZM" });
        var unit = NewOrgUnit("Finance");
        ctx.OrgUnits.Add(unit);
        await ctx.SaveChangesAsync();

        var svc = Build(ctx);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,email,orgUnitName,workerType\n" +
            "Mary,Bwalya,mary@example.com,Finance,employee\n" +
            "John,Phiri,john@example.com,Finance,contingent\n"));

        var result = await svc.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);
        Assert.Equal(2, await ctx.Workers.CountAsync());
    }

    [Fact]
    public async Task ImportCsv_InvalidRows_ReportedPerRow_NeverThrown()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = Build(ctx);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,email,orgUnitName\n" +
            "Mary,Bwalya,mary@example.com,\n" +
            ",,bad@example.com,\n" +
            "John,Phiri,not-an-email,\n" +
            "Grace,Kunda,grace@example.com,NonexistentUnit\n"));

        var result = await svc.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Single(await ctx.Workers.ToListAsync());
        Assert.Equal(1, result.Created);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Row == 3);
        Assert.Contains(result.Errors, e => e.Row == 4);
        Assert.Contains(result.Errors, e => e.Row == 5);
    }

    [Fact]
    public async Task ImportCsv_UnknownOrgUnitRowIsReported()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = Build(ctx);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,orgUnitName\n" +
            "Mary,Bwalya,UnknownDept\n"));

        var result = await svc.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Empty(await ctx.Workers.ToListAsync());
        Assert.Single(result.Errors);
        Assert.Contains("UnknownDept", result.Errors[0].Detail);
    }

    [Fact]
    public async Task ImportCsv_EmptyFile_ReturnsHeaderError()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = Build(ctx);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(""));

        var result = await svc.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Single(result.Errors);
        Assert.Equal(0, result.Created);
    }

    [Fact]
    public async Task ImportCsv_QuotedCommaInsideCell_Parsed()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = Build(ctx);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,jobTitle\n" +
            "\"Mary Jane\",Bwalya,\"Director, Finance\"\n"));

        var result = await svc.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Empty(result.Errors);
        var worker = await ctx.Workers.FirstAsync();
        Assert.Equal("Mary Jane", worker.FirstName);
        Assert.Equal("Director, Finance", worker.JobTitle);
    }

    [Fact]
    public async Task ImportCsv_DuplicateEmailAgainstExisting_IsSkipped()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.LegalEntities.Add(new LegalEntity { Id = SeedLegalEntityId, RegisteredName = "Mighty Finance", Code = "MF", CountryCode = "ZM" });
        await ctx.SaveChangesAsync();

        var svc = Build(ctx);
        // A worker already in the tenant.
        var first = await svc.ImportCsvAsync(new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,email\nMary,Bwalya,mary@example.com\n")), CancellationToken.None);
        Assert.Equal(1, first.Created);
        Assert.Single(await ctx.Workers.ToListAsync());

        // The same email must be skipped on re-import, not duplicated.
        var result = await svc.ImportCsvAsync(new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,email\nMary,Bwalya,mary@example.com\n")), CancellationToken.None);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Contains("already in use", result.Errors[0].Detail);
        Assert.Single(await ctx.Workers.ToListAsync());
    }

    [Fact]
    public async Task ImportCsv_DuplicateEmailWithinFile_IsSkipped()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.LegalEntities.Add(new LegalEntity { Id = SeedLegalEntityId, RegisteredName = "Mighty Finance", Code = "MF", CountryCode = "ZM" });
        await ctx.SaveChangesAsync();

        var svc = Build(ctx);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "firstName,lastName,email,napsaNumber\n" +
            "Mary,Bwalya,mary@example.com,N1234567890\n" +
            "Mary,Bwalya,mary@example.com,N9876543210\n"));

        var result = await svc.ImportCsvAsync(csv, CancellationToken.None);
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal(3, result.Errors[0].Row);
    }
}
