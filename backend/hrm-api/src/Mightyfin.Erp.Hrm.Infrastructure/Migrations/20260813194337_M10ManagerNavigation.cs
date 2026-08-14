using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10ManagerNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_workers_manager_id",
                schema: "hrm",
                table: "workers",
                column: "manager_id");

            migrationBuilder.AddForeignKey(
                name: "FK_workers_workers_manager_id",
                schema: "hrm",
                table: "workers",
                column: "manager_id",
                principalSchema: "hrm",
                principalTable: "workers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workers_workers_manager_id",
                schema: "hrm",
                table: "workers");

            migrationBuilder.DropIndex(
                name: "IX_workers_manager_id",
                schema: "hrm",
                table: "workers");
        }
    }
}
