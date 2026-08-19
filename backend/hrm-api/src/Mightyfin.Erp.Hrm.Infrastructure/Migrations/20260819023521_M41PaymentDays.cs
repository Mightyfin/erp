using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M41PaymentDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "payment_days",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "proration_note",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "working_days",
                schema: "hrm",
                table: "payroll_run_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_days",
                schema: "hrm",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "proration_note",
                schema: "hrm",
                table: "payroll_run_lines");

            migrationBuilder.DropColumn(
                name: "working_days",
                schema: "hrm",
                table: "payroll_run_lines");
        }
    }
}
