using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public class ConfigAdminServiceTests
{
    private static (ConfigAdminServiceImpl svc, HrmDbContext ctx) Build(string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new ConfigRepository(ctx);
        var svc = new ConfigAdminServiceImpl(repo, new PermissiveAuthz());
        return (svc, ctx);
    }

    [Fact]
    public async Task CreateLegalEntity_CreatesAndLists()
    {
        var (svc, ctx) = Build();
        var created = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "ZML", RegisteredName: "Zambia Mining Ltd"), default);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("ZML", created.Code);
        Assert.Equal("ZMW", created.Currency);
        Assert.Equal("ZM", created.CountryCode);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        var list = await svc2.ListLegalEntitiesAsync(default);
        Assert.Single(list.Items);
        Assert.Equal("ZML", list.Items[0].Code);
    }

    [Fact]
    public async Task LegalEntity_DuplicateCode_ReturnsConflict()
    {
        var (svc, _) = Build();
        await svc.CreateLegalEntityAsync(new LegalEntityCreateRequest(Code: "DUP", RegisteredName: "One"), default);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateLegalEntityAsync(new LegalEntityCreateRequest(Code: "dup", RegisteredName: "Two"), default));
    }

    [Fact]
    public async Task OrgUnit_FutureClose_Succeeds_And_NowExcludedFromTree()
    {
        var (svc, ctx) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ1", RegisteredName: "Headquarters One"), default);
        var unit = await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "FIN", Name: "Finance", LegalEntityId: entity.Id), default);

        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        await svc.CloseOrgUnitAsync(unit.Id, new OrgUnitCloseRequest(EffectiveDate: tomorrow), default);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        var list = await svc2.ListOrgUnitsAsync(default);
        Assert.Contains(list, u => u.Id == unit.Id && u.Status == "closed");
        var tree = await svc2.GetOrgUnitTreeAsync(default);
        Assert.DoesNotContain(tree, n => n.Id == unit.Id);
    }

    [Fact]
    public async Task OrgUnit_BackdatedClose_Rejected()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ2", RegisteredName: "Headquarters Two"), default);
        var unit = await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "OPS", Name: "Operations", LegalEntityId: entity.Id), default);
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CloseOrgUnitAsync(unit.Id, new OrgUnitCloseRequest(EffectiveDate: yesterday), default));
    }

    [Fact]
    public async Task WorkCalendar_HolidayLifecycle()
    {
        var (svc, ctx) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ3", RegisteredName: "Headquarters Three"), default);
        var calendar = await svc.CreateCalendarAsync(new WorkCalendarCreateRequest(
            Name: "Zambia 2026", LegalEntityId: entity.Id), default);

        var holiday = await svc.AddHolidayAsync(new PublicHolidayCreateRequest(
            CalendarId: calendar.Id, Name: "Independence Day", HolidayDate: "2026-10-24"), default);

        var updated = await svc.UpdateHolidayAsync(holiday.Id, new PublicHolidayUpdateRequest(Description: "Kenneth Kaunda Day"), default);
        Assert.Equal("Kenneth Kaunda Day", updated.Description);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        await svc2.DeleteHolidayAsync(holiday.Id, default);
        var calendars = await svc2.ListCalendarsAsync(default);
        Assert.Single(calendars.Items);
        Assert.Empty(calendars.Items[0].Holidays);
    }

    [Fact]
    public async Task WeekendDays_Normalized()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ4", RegisteredName: "Headquarters Four"), default);
        var calendar = await svc.CreateCalendarAsync(new WorkCalendarCreateRequest(
            Name: "Mining Shifts", LegalEntityId: entity.Id, WeekendDays: "SUN, fri ,garbage"), default);
        Assert.Equal("sun,fri", calendar.WeekendDays);
    }

    [Fact]
    public async Task LeaveType_Validation_ClampsAndCodes()
    {
        var (svc, _) = Build();
        var lt = await svc.CreateLeaveTypeAsync(new LeaveTypeCreateRequest(
            Code: "AnnualLeave", Name: "Annual Leave", DefaultDaysPerYear: -5, CarryForwardDays: 10,
            MinNoticeDays: -2), default);
        Assert.Equal(0, lt.DefaultDaysPerYear);
        Assert.Equal(10, lt.CarryForwardDays);
        Assert.Equal(0, lt.MinNoticeDays);
        Assert.Equal("annualleave", lt.Code);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateLeaveTypeAsync(new LeaveTypeCreateRequest(
                Code: "annualleave", Name: "Dup"), default));
    }
}
