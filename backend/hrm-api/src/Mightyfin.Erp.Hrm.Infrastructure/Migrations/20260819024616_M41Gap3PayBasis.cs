using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M41Gap3PayBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pay_basis",
                schema: "hrm",
                table: "worker_payroll_profiles",
                type: "text",
                nullable: false,
                defaultValue: "salary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pay_basis",
                schema: "hrm",
                table: "worker_payroll_profiles");
        }
    }
}
