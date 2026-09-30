using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;
[DbContext(typeof(HrmDbContext))]
[Migration("20260926193000_PeriodBenefitAssignments")]
public sealed class PeriodBenefitAssignments : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE TABLE hrm.period_benefits (
            id uuid PRIMARY KEY, tenant_id text NOT NULL, pay_period_id uuid NOT NULL REFERENCES hrm.pay_periods(id),
            worker_id uuid NOT NULL REFERENCES hrm.workers(id), benefit_type_id uuid NOT NULL REFERENCES hrm.benefit_types(id),
            amount numeric(18,2) NOT NULL CHECK (amount >= 0), note text NOT NULL,
            created_at timestamptz NOT NULL, created_by text NOT NULL, updated_at timestamptz, updated_by text,
            is_archived boolean NOT NULL DEFAULT false);
        CREATE INDEX ix_period_benefits_pay_period_id ON hrm.period_benefits(pay_period_id);
        CREATE INDEX ix_period_benefits_worker_id ON hrm.period_benefits(worker_id);
        CREATE INDEX ix_period_benefits_benefit_type_id ON hrm.period_benefits(benefit_type_id);
        CREATE UNIQUE INDEX ix_period_benefits_tenant_period_worker_type
            ON hrm.period_benefits(tenant_id,pay_period_id,worker_id,benefit_type_id) WHERE NOT is_archived;
        """);
    protected override void Down(MigrationBuilder m) => m.DropTable("period_benefits", "hrm");
}
