using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260826000100_BenefitPayrollToggle")]
public partial class BenefitPayrollToggle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "include_in_payroll",
            schema: "hrm",
            table: "benefit_types",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "include_in_payroll",
            schema: "hrm",
            table: "benefit_types");
    }
}
