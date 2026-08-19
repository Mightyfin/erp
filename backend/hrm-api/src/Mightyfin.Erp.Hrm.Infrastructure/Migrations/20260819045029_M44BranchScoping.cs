using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M44BranchScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "payslips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "payroll_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "leave_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "leave_encashments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "benefit_claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "attendance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "attendance_corrections",
                type: "uuid",
                nullable: true);

            // M44: list pages filter by branch constantly; index for performance.
            foreach (var t in new[] { "payslips", "payroll_runs", "leave_requests", "leave_encashments", "benefit_claims", "attendance_records", "attendance_corrections" })
            {
                migrationBuilder.CreateIndex(
                    name: $"ix_{t}_location_id",
                    schema: "hrm",
                    table: t,
                    column: "location_id");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "payslips");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "payroll_runs");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "leave_encashments");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "benefit_claims");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "attendance_corrections");

            foreach (var t in new[] { "payslips", "payroll_runs", "leave_requests", "leave_encashments", "benefit_claims", "attendance_records", "attendance_corrections" })
            {
                migrationBuilder.DropIndex(
                    name: $"ix_{t}_location_id",
                    schema: "hrm",
                    table: t);
            }
        }
    }
}
