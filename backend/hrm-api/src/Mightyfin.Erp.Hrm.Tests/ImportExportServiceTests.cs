// M31 — shared import/export engine tests: schema listing, CSV parse, preview
// (client-mapped rows → per-row status), apply (create path) and the round-
// trip export. SQLite in-memory per factory, isolated tenant.
using Xunit;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public class ImportExportServiceTests
{
    [Fact]
    public void ParseCsvLine_HonorsQuotedCommasAndEscapedQuotes()
    {
        var cells = ImportRowParser.ParseCsvLine("\"Kabanga, Mary\", \"\"Big Mary\"\", 123");
        // Double-quoted cells are unquoted (RFC 4180), including escaped inner quotes.
        Assert.Equal(["Kabanga, Mary", " Big Mary", " 123"], cells);
    }

    [Fact]
    public async Task Preview_Insert_NewRowIsCreate()
    {
        var svc = BuildServiceWithOrgUnit("Human Resources");
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "Grace", ["lastName"] = "Mulenga", ["orgUnitName"] = "Human Resources" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillCreate);
    }

    [Fact]
    public async Task Preview_Update_ExistingEmployeeNoBecomesUpdate()
    {
        // Seed the worker into the SAME db the service uses — each service holds
        // its own in-memory context, so the seed happens inside the helper.
        var svc = BuildSeedService("EMP-99");

        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["firstName"] = "A-updated", ["lastName"] = "B" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "update", rows, CancellationToken.None);
        Assert.Equal("update", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillUpdate);
    }

    [Fact]
    public async Task Preview_MissingNamesIsError()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "", ["lastName"] = "Z" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillError);
    }

    [Fact]
    public async Task Preview_UnknownOrgUnitIsError()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "X", ["lastName"] = "Y", ["orgUnitName"] = "No Such Department" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
    }

    [Fact]
    public async Task Apply_InvalidPreviewIdThrows()
    {
        var svc = BuildService();
        await Assert.ThrowsAsync<Application.DomainException>(
            () => svc.ApplyAsync(Guid.NewGuid(), [0], CancellationToken.None));
    }

    [Fact]
    public async Task Export_CsvRoundTripsWorkerRows()
    {
        var svc = BuildService();
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert",
            [new() { ["firstName"] = "E", ["lastName"] = "F", ["orgUnitName"] = "Finance" }],
            CancellationToken.None);
        var bytes = await svc.ExportAsync("workers", null, CancellationToken.None);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("firstName", text);
        Assert.Contains("orgUnitName", text);
    }

    [Fact]
    public void ListSchemas_IncludesWorkers()
    {
        var svc = BuildService();
        var schemas = svc.ListSchemas();
        Assert.Contains(schemas, s => s.TypeKey == "workers" && s.Fields.Count > 10);
    }

    private static ImportExportServiceImpl BuildService()
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz());
        return new ImportExportServiceImpl(new[] { schema });
    }

    private static ImportExportServiceImpl BuildSeedService(string employeeNo)
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        db.Workers.Add(new Domain.Entities.Worker
        {
            Id = Guid.NewGuid(), EmployeeNo = employeeNo, FirstName = "A", LastName = "B",
            Status = "active", WorkerType = "employee",
        });
        db.SaveChanges();
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz());
        return new ImportExportServiceImpl(new[] { schema });
    }

    private static ImportExportServiceImpl BuildServiceWithOrgUnit(string unitName)
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        var entity = new Domain.Entities.LegalEntity
        {
            Id = Guid.NewGuid(), Code = "TST", RegisteredName = "Test Ltd",
            TradingName = "Test", CountryCode = "ZM",
        };
        db.LegalEntities.Add(entity);
        db.OrgUnits.Add(new Domain.Entities.OrgUnit
        {
            Id = Guid.NewGuid(), Code = "TST", Name = unitName, LegalEntityId = entity.Id,
            UnitType = "department", Status = "active", EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.SaveChanges();
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz());
        return new ImportExportServiceImpl(new[] { schema });
    }
}
