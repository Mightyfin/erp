using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M32MasterDataTests
{
    [Fact]
    public async Task Import_PreviewsAppliesAndRollsBackAsOneBatch()
    {
        await using var db = TestDbContextFactory.Create("m32-import");
        var existing = Worker("IMP-001", "Old", "Name");
        db.Workers.Add(existing);
        await db.SaveChangesAsync();
        var service = Service(db);

        var preview = await service.PreviewImportAsync(new WorkerImportPreviewRequest("workers.csv",
        [
            new("IMP-001", "Updated", "Name", Email: "updated@example.test"),
            new(null, "New", "Worker", Email: "new@example.test", Nrc: "111111/11/1"),
        ]), "hr-admin", CancellationToken.None);

        Assert.Equal(2, preview.ReadyCount);
        Assert.Equal(0, preview.ErrorCount);
        var applied = await service.ApplyAsync(preview.Id, "hr-admin", CancellationToken.None);
        Assert.Equal("applied", applied.Status);
        Assert.Equal("Updated", (await db.Workers.SingleAsync(x => x.EmployeeNo == "IMP-001")).FirstName);
        Assert.Equal(2, await db.Workers.CountAsync(x => !x.IsArchived));

        var rolledBack = await service.RollbackAsync(preview.Id, "hr-admin", CancellationToken.None);
        Assert.Equal("rolled-back", rolledBack.Status);
        Assert.Equal("Old", (await db.Workers.SingleAsync(x => x.EmployeeNo == "IMP-001")).FirstName);
        var imported = await db.Workers.SingleAsync(x => x.EmployeeNo != "IMP-001");
        Assert.True(imported.IsArchived);
        Assert.Equal("archived", imported.Status);
    }

    [Fact]
    public async Task Import_BlocksDuplicateIdentityAndArchivedOverwrite()
    {
        await using var db = TestDbContextFactory.Create("m32-import-errors");
        var active = Worker("IMP-010", "Active", "Worker");
        active.Email = "duplicate@example.test";
        var archived = Worker("IMP-011", "Archived", "Worker");
        archived.IsArchived = true;
        archived.Status = "archived";
        db.Workers.AddRange(active, archived);
        await db.SaveChangesAsync();
        var service = Service(db);

        var preview = await service.PreviewImportAsync(new WorkerImportPreviewRequest("unsafe.csv",
        [
            new("IMP-011", "Overwrite", "Archived"),
            new("IMP-012", "Duplicate", "Identity", Email: "duplicate@example.test"),
        ]), "hr-admin", CancellationToken.None);

        Assert.Equal(2, preview.ErrorCount);
        await Assert.ThrowsAsync<DomainException>(() =>
            service.ApplyAsync(preview.Id, "hr-admin", CancellationToken.None));
        Assert.Equal(2, await db.Workers.CountAsync());
    }

    [Fact]
    public async Task FutureBulkOrganisationChange_CreatesMovementWithoutRewritingCurrentWorker()
    {
        await using var db = TestDbContextFactory.Create("m32-future");
        var (oldUnit, newUnit, _, worker) = await SeedOrganisationAsync(db);
        worker.OrgUnitId = oldUnit.Id;
        await db.SaveChangesAsync();
        var service = Service(db);
        var effective = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var preview = await service.PreviewBulkAsync(new WorkerBulkPreviewRequest(effective.ToString("yyyy-MM-dd"),
            [new(worker.EmployeeNo, OrgUnitCode: newUnit.Code, Grade: "M2", JobTitle: "Manager")]),
            "hr-admin", CancellationToken.None);
        await service.ApplyAsync(preview.Id, "hr-admin", CancellationToken.None);

        db.ChangeTracker.Clear();
        Assert.Equal(oldUnit.Id, (await db.Workers.SingleAsync(x => x.Id == worker.Id)).OrgUnitId);
        var movement = await db.Movements.SingleAsync();
        Assert.Equal("approved", movement.Status);
        Assert.Equal(newUnit.Id, movement.ToOrgUnitId);
        await service.RollbackAsync(preview.Id, "hr-admin", CancellationToken.None);
        Assert.Equal("cancelled", (await db.Movements.SingleAsync()).Status);
    }

    [Fact]
    public async Task ImmediateStatutoryBulkUpdate_IsReversible()
    {
        await using var db = TestDbContextFactory.Create("m32-statutory");
        var worker = Worker("STAT-001", "Statutory", "Worker");
        worker.Tpin = "old-tpin";
        db.Workers.Add(worker);
        await db.SaveChangesAsync();
        var service = Service(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var preview = await service.PreviewBulkAsync(new WorkerBulkPreviewRequest(today,
            [new(worker.EmployeeNo, Tpin: "new-tpin", NapsaNumber: "NAPSA-001", NhimaNumber: "NHIMA-001")]),
            "hr-admin", CancellationToken.None);
        await service.ApplyAsync(preview.Id, "hr-admin", CancellationToken.None);
        Assert.Equal("new-tpin", (await db.Workers.SingleAsync()).Tpin);

        await service.RollbackAsync(preview.Id, "hr-admin", CancellationToken.None);
        var restored = await db.Workers.SingleAsync();
        Assert.Equal("old-tpin", restored.Tpin);
        Assert.Null(restored.NapsaNumber);
        Assert.Null(restored.NhimaNumber);
    }

    [Fact]
    public async Task Reactivation_RequiresReasonAndCreatesRecoverableHistory()
    {
        await using var db = TestDbContextFactory.Create("m32-reactivate");
        var worker = Worker("ARC-001", "Archived", "Worker");
        worker.IsArchived = true;
        worker.Status = "archived";
        worker.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        db.Workers.Add(worker);
        await db.SaveChangesAsync();
        var service = Service(db);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ReactivateAsync(worker.Id, new WorkerReactivateRequest(""), "hr-admin", CancellationToken.None));
        var batch = await service.ReactivateAsync(worker.Id, new WorkerReactivateRequest("Returning employee"), "hr-admin", CancellationToken.None);

        Assert.Equal("reactivation", batch.BatchType);
        Assert.Equal("active", (await db.Workers.SingleAsync()).Status);
        await service.RollbackAsync(batch.Id, "hr-admin", CancellationToken.None);
        Assert.True((await db.Workers.SingleAsync()).IsArchived);
    }

    private static MasterDataService Service(HrmDbContext db) =>
        new(db, new PermissiveAuthz(), new EfUnitOfWork(db));

    private static Worker Worker(string employeeNo, string firstName, string lastName) => new()
    {
        EmployeeNo = employeeNo, FirstName = firstName, LastName = lastName,
        WorkerType = "employee", Status = "active",
    };

    private static async Task<(OrgUnit OldUnit, OrgUnit NewUnit, WorkLocation Location, Worker Worker)> SeedOrganisationAsync(HrmDbContext db)
    {
        var entity = new LegalEntity { Code = "MFZ", RegisteredName = "MightyFin Zambia" };
        var oldUnit = new OrgUnit { Code = "OPS", Name = "Operations", LegalEntityId = entity.Id, EffectiveFrom = new DateOnly(2020, 1, 1) };
        var newUnit = new OrgUnit { Code = "FIN", Name = "Finance", LegalEntityId = entity.Id, EffectiveFrom = new DateOnly(2020, 1, 1) };
        var location = new WorkLocation { Code = "LHQ", Name = "Lusaka HQ", LegalEntityId = entity.Id };
        var worker = Worker("BULK-001", "Bulk", "Worker");
        db.AddRange(entity, oldUnit, newUnit, location, worker);
        await db.SaveChangesAsync();
        return (oldUnit, newUnit, location, worker);
    }
}
