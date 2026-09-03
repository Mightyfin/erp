using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

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
