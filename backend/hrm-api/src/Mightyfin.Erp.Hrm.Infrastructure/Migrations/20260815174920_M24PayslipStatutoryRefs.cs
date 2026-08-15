using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M24PayslipStatutoryRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "worker_napsa_number",
                schema: "hrm",
                table: "payslips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "worker_nhima_number",
                schema: "hrm",
                table: "payslips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "worker_nrc",
                schema: "hrm",
                table: "payslips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "worker_tpin",
                schema: "hrm",
                table: "payslips",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "worker_napsa_number",
                schema: "hrm",
                table: "payslips");

            migrationBuilder.DropColumn(
                name: "worker_nhima_number",
                schema: "hrm",
                table: "payslips");

            migrationBuilder.DropColumn(
                name: "worker_nrc",
                schema: "hrm",
                table: "payslips");

            migrationBuilder.DropColumn(
                name: "worker_tpin",
                schema: "hrm",
                table: "payslips");
        }
    }
}
