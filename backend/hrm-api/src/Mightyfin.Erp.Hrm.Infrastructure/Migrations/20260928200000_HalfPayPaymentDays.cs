using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;
[DbContext(typeof(HrmDbContext))]
[Migration("20260928200000_HalfPayPaymentDays")]
public sealed class HalfPayPaymentDays : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("ALTER TABLE hrm.payroll_run_lines ALTER COLUMN payment_days TYPE numeric(8,2) USING payment_days::numeric(8,2);");
    protected override void Down(MigrationBuilder m) => m.Sql("ALTER TABLE hrm.payroll_run_lines ALTER COLUMN payment_days TYPE integer USING round(payment_days)::integer;");
}
