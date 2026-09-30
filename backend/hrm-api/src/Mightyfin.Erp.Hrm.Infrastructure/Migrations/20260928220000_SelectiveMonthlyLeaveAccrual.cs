using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260928220000_SelectiveMonthlyLeaveAccrual")]
public sealed class SelectiveMonthlyLeaveAccrual : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>(name: "auto_accrue_monthly", schema: "hrm", table: "leave_types", type: "boolean", nullable: false, defaultValue: false);
        m.AddColumn<DateOnly>(name: "accrual_start_date", schema: "hrm", table: "leave_types", type: "date", nullable: true);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn(name: "auto_accrue_monthly", schema: "hrm", table: "leave_types");
        m.DropColumn(name: "accrual_start_date", schema: "hrm", table: "leave_types");
    }
}
