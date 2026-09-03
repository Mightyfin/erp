using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260903095000_HistoricalPayrollRuns")]
public partial class HistoricalPayrollRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "is_historical", schema: "hrm", table: "pay_periods", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "historical_reason", schema: "hrm", table: "pay_periods", type: "text", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "is_historical", schema: "hrm", table: "payroll_runs", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "historical_reason", schema: "hrm", table: "payroll_runs", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "is_historical", schema: "hrm", table: "pay_periods");
        migrationBuilder.DropColumn(name: "historical_reason", schema: "hrm", table: "pay_periods");
        migrationBuilder.DropColumn(name: "is_historical", schema: "hrm", table: "payroll_runs");
        migrationBuilder.DropColumn(name: "historical_reason", schema: "hrm", table: "payroll_runs");
    }
}
