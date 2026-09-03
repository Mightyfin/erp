using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260903084500_TaxablePayrollBenefits")]
/// <summary>Separates payroll inclusion from PAYE treatment. A payroll benefit
/// can be a taxable cash allowance or a genuine exempt non-cash benefit.</summary>
public partial class TaxablePayrollBenefits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_taxable",
            schema: "hrm",
            table: "benefit_types",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_taxable",
            schema: "hrm",
            table: "benefit_types");
    }
}
