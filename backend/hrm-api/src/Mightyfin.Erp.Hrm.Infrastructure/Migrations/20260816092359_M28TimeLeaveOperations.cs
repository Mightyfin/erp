using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M28TimeLeaveOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "import_batch_id",
                schema: "hrm",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "overtime_hours",
                schema: "hrm",
                table: "attendance_records",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "overtime_multiplier",
                schema: "hrm",
                table: "attendance_records",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "regular_hours",
                schema: "hrm",
                table: "attendance_records",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "scheduled_hours",
                schema: "hrm",
                table: "attendance_records",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "shift_id",
                schema: "hrm",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attendance_import_batches",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    imported_count = table.Column<int>(type: "integer", nullable: false),
                    updated_count = table.Column<int>(type: "integer", nullable: false),
                    rejected_count = table.Column<int>(type: "integer", nullable: false),
                    errors_json = table.Column<string>(type: "text", nullable: true),
                    imported_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_accrual_runs",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    worker_count = table.Column<int>(type: "integer", nullable: false),
                    ledger_entry_count = table.Column<int>(type: "integer", nullable: false),
                    total_days_accrued = table.Column<decimal>(type: "numeric", nullable: false),
                    run_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_accrual_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_balance_adjustments",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_code = table.Column<string>(type: "text", nullable: false),
                    days = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    adjusted_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_balance_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_leave_balance_adjustments_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_definitions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    unpaid_break_minutes = table.Column<int>(type: "integer", nullable: false),
                    standard_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    daily_overtime_threshold_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    weekday_overtime_multiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    rest_day_overtime_multiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    holiday_overtime_multiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "worker_shift_assignments",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_shift_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_worker_shift_assignments_shift_definitions_shift_id",
                        column: x => x.shift_id,
                        principalSchema: "hrm",
                        principalTable: "shift_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_worker_shift_assignments_work_calendars_calendar_id",
                        column: x => x.calendar_id,
                        principalSchema: "hrm",
                        principalTable: "work_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_worker_shift_assignments_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_accrual_runs_tenant_id_period",
                schema: "hrm",
                table: "leave_accrual_runs",
                columns: new[] { "tenant_id", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_balance_adjustments_worker_id",
                schema: "hrm",
                table: "leave_balance_adjustments",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_definitions_tenant_id_code",
                schema: "hrm",
                table: "shift_definitions",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_worker_shift_assignments_calendar_id",
                schema: "hrm",
                table: "worker_shift_assignments",
                column: "calendar_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_shift_assignments_shift_id",
                schema: "hrm",
                table: "worker_shift_assignments",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_shift_assignments_worker_id",
                schema: "hrm",
                table: "worker_shift_assignments",
                column: "worker_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_import_batches",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "leave_accrual_runs",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "leave_balance_adjustments",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "worker_shift_assignments",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "shift_definitions",
                schema: "hrm");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_hours",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_multiplier",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "regular_hours",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "scheduled_hours",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "shift_id",
                schema: "hrm",
                table: "attendance_records");
        }
    }
}
