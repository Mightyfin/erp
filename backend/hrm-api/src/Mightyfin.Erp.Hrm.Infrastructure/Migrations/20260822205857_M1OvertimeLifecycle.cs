using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1OvertimeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "overtime_decided_at",
                schema: "hrm",
                table: "attendance_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overtime_decided_by_subject_id",
                schema: "hrm",
                table: "attendance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overtime_decision_reason",
                schema: "hrm",
                table: "attendance_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "overtime_payroll_line_id",
                schema: "hrm",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "overtime_payroll_run_id",
                schema: "hrm",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overtime_status",
                schema: "hrm",
                table: "attendance_records",
                type: "text",
                nullable: false,
                defaultValue: "");


            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_tenant_id_overtime_status_work_date",
                schema: "hrm",
                table: "attendance_records",
                columns: new[] { "tenant_id", "overtime_status", "work_date" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropIndex(
                name: "IX_attendance_records_tenant_id_overtime_status_work_date",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_decided_at",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_decided_by_subject_id",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_decision_reason",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_payroll_line_id",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_payroll_run_id",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_status",
                schema: "hrm",
                table: "attendance_records");
        }
    }
}
