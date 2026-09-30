using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;
[DbContext(typeof(HrmDbContext))]
[Migration("20260926203000_PayrollBenefitClaims")]
public sealed class PayrollBenefitClaims : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        ALTER TABLE hrm.benefit_claims ADD COLUMN pay_period_id uuid REFERENCES hrm.pay_periods(id) ON DELETE RESTRICT;
        CREATE INDEX "IX_benefit_claims_pay_period_id" ON hrm.benefit_claims(pay_period_id);
        """);
    protected override void Down(MigrationBuilder m) => m.DropColumn("pay_period_id", "benefit_claims", "hrm");
}
