using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M27PayrollOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "approved_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculated_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locked_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_approved_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_file_generated_at",
                schema: "hrm",
                table: "payroll_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_file_generated_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_file_reference",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_released_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: false,
                defaultValue: "not-created");

            migrationBuilder.AddColumn<string>(
                name: "prepared_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reconciled_amount",
                schema: "hrm",
                table: "payroll_runs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reconciled_at",
                schema: "hrm",
                table: "payroll_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reconciled_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reconciliation_reference",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "released_by_subject_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "exception_decided_at",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exception_decided_by_subject_id",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exception_decision_reason",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exception_status",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "text",
                nullable: false,
                defaultValue: "open");

            migrationBuilder.AddColumn<bool>(
                name: "is_excluded",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "payroll_run_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    details_json = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_run_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_run_events_payroll_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "hrm",
                        principalTable: "payroll_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_events_run_id",
                schema: "hrm",
                table: "payroll_run_events",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_events_tenant_id_run_id_created_at",
                schema: "hrm",
                table: "payroll_run_events",
                columns: new[] { "tenant_id", "run_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payroll_run_events",
                schema: "hrm");

            migrationBuilder.DropColumn(
                name: "approved_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "calculated_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "locked_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "payment_approved_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "payment_file_generated_at",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "payment_file_generated_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "payment_file_reference",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "payment_released_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "payment_status",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "prepared_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "reconciled_amount",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "reconciled_at",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "reconciled_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "reconciliation_reference",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "released_by_subject_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "exception_decided_at",
                schema: "hrm",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "exception_decided_by_subject_id",
                schema: "hrm",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "exception_decision_reason",
                schema: "hrm",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "exception_status",
                schema: "hrm",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "is_excluded",
                schema: "hrm",
                table: "payroll_run_lines");
        }
    }
}
