using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HrmDbContext))]
    [Migration("20260826183000_M51OvertimeCompliancePolicy")]
    public partial class M51OvertimeCompliancePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "monthly_overtime_divisor",
                schema: "hrm",
                table: "worker_payroll_profiles",
                type: "numeric",
                nullable: false,
                defaultValue: 208m);

            migrationBuilder.AddColumn<string>(
                name: "overtime_category",
                schema: "hrm",
                table: "worker_payroll_profiles",
                type: "text",
                nullable: false,
                defaultValue: "ordinary");

            migrationBuilder.AddColumn<decimal>(
                name: "weekly_overtime_threshold_hours",
                schema: "hrm",
                table: "worker_payroll_profiles",
                type: "numeric",
                nullable: false,
                defaultValue: 48m);

            migrationBuilder.AddColumn<decimal>(
                name: "overtime_hourly_divisor",
                schema: "hrm",
                table: "attendance_records",
                type: "numeric",
                nullable: false,
                defaultValue: 208m);

            migrationBuilder.AddColumn<string>(
                name: "overtime_rule_code",
                schema: "hrm",
                table: "attendance_records",
                type: "text",
                nullable: false,
                defaultValue: "ordinary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "monthly_overtime_divisor",
                schema: "hrm",
                table: "worker_payroll_profiles");

            migrationBuilder.DropColumn(
                name: "overtime_category",
                schema: "hrm",
                table: "worker_payroll_profiles");

            migrationBuilder.DropColumn(
                name: "weekly_overtime_threshold_hours",
                schema: "hrm",
                table: "worker_payroll_profiles");

            migrationBuilder.DropColumn(
                name: "overtime_hourly_divisor",
                schema: "hrm",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "overtime_rule_code",
                schema: "hrm",
                table: "attendance_records");
        }
    }
}
