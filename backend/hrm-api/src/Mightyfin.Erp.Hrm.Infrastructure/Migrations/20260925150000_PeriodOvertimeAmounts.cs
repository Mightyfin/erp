using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;
[DbContext(typeof(HrmDbContext))]
[Migration("20260925150000_PeriodOvertimeAmounts")]
public sealed class PeriodOvertimeAmounts : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE TABLE hrm.period_overtime (
            id uuid PRIMARY KEY, tenant_id text NOT NULL, pay_period_id uuid NOT NULL REFERENCES hrm.pay_periods(id),
            worker_id uuid NOT NULL REFERENCES hrm.workers(id), amount numeric(18,2) NOT NULL CHECK (amount >= 0), note text NOT NULL,
            created_at timestamptz NOT NULL, created_by text NOT NULL, updated_at timestamptz, updated_by text,
            is_archived boolean NOT NULL DEFAULT false);
        CREATE INDEX ix_period_overtime_pay_period_id ON hrm.period_overtime(pay_period_id);
        CREATE INDEX ix_period_overtime_worker_id ON hrm.period_overtime(worker_id);
        CREATE UNIQUE INDEX ix_period_overtime_tenant_id_pay_period_id_worker_id
            ON hrm.period_overtime(tenant_id,pay_period_id,worker_id) WHERE NOT is_archived;
        """);
    protected override void Down(MigrationBuilder m) => m.DropTable("period_overtime", "hrm");
}
