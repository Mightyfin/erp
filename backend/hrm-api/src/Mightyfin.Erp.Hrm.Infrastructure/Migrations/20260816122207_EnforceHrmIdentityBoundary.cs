using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceHrmIdentityBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_workers_tenant_id_subject_id",
                schema: "hrm",
                table: "workers",
                columns: new[] { "tenant_id", "subject_id" },
                unique: true,
                filter: "subject_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workers_tenant_id_subject_id",
                schema: "hrm",
                table: "workers");
        }
    }
}
