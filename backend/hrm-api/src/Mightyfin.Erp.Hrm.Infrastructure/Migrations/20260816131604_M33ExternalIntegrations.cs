using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M33ExternalIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_operations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<string>(type: "text", nullable: false),
                    integration_key = table.Column<string>(type: "text", nullable: false),
                    operation_type = table.Column<string>(type: "text", nullable: false),
                    contract_version = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_reference = table.Column<string>(type: "text", nullable: true),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    external_reference = table.Column<string>(type: "text", nullable: true),
                    reconciliation_outcome = table.Column<string>(type: "text", nullable: true),
                    reconciliation_note = table.Column<string>(type: "text", nullable: true),
                    reconciled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    reconciled_by_subject_id = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_operations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_operations_tenant_id_idempotency_key",
                schema: "hrm",
                table: "integration_operations",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_operations_tenant_id_integration_key_status_cre~",
                schema: "hrm",
                table: "integration_operations",
                columns: new[] { "tenant_id", "integration_key", "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_operations",
                schema: "hrm");
        }
    }
}
