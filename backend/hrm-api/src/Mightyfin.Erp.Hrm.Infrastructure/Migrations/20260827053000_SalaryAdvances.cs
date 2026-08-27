using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260827053000_SalaryAdvances")]
public partial class SalaryAdvances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "salary_advances",
            schema: "hrm",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric", nullable: false),
                installment_amount = table.Column<decimal>(type: "numeric", nullable: false),
                currency = table.Column<string>(type: "text", nullable: false),
                issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                deduction_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                deduct_from_payslip = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                reason = table.Column<string>(type: "text", nullable: true),
                reference = table.Column<string>(type: "text", nullable: true),
                created_by_subject_id = table.Column<string>(type: "text", nullable: true),
                cancelled_by_subject_id = table.Column<string>(type: "text", nullable: true),
                cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                cancellation_reason = table.Column<string>(type: "text", nullable: true),
                tenant_id = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "text", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_salary_advances", x => x.id);
                table.ForeignKey(
                    name: "fk_salary_advances_workers_worker_id",
                    column: x => x.worker_id,
                    principalSchema: "hrm",
                    principalTable: "workers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_salary_advances_worker_id",
            schema: "hrm",
            table: "salary_advances",
            column: "worker_id");

        migrationBuilder.CreateIndex(
            name: "ix_salary_advances_tenant_id_worker_id_status",
            schema: "hrm",
            table: "salary_advances",
            columns: new[] { "tenant_id", "worker_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_salary_advances_tenant_deduct_start",
            schema: "hrm",
            table: "salary_advances",
            columns: new[] { "tenant_id", "deduct_from_payslip", "deduction_start_date" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "salary_advances",
            schema: "hrm");
    }
}
