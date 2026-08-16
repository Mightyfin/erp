using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M34SecurityTenancyCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_evidence",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    control_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    evidence_reference = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_evidence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_holds",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    placed_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_by_subject_id = table.Column<string>(type: "text", nullable: true),
                    release_reason = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_holds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "privileged_action_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    actor_roles = table.Column<string>(type: "text", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    request_id = table.Column<string>(type: "text", nullable: false),
                    source_address_hash = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_privileged_action_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_tenant_id_created_at",
                schema: "hrm",
                table: "audit_entries",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_tenant_id_entity_type_entity_id",
                schema: "hrm",
                table: "audit_entries",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evidence_tenant_id_control_key_executed_at",
                schema: "hrm",
                table: "compliance_evidence",
                columns: new[] { "tenant_id", "control_key", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_holds_tenant_id_reference",
                schema: "hrm",
                table: "legal_holds",
                columns: new[] { "tenant_id", "reference" },
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_privileged_action_events_tenant_id_actor_subject_id_created~",
                schema: "hrm",
                table: "privileged_action_events",
                columns: new[] { "tenant_id", "actor_subject_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_privileged_action_events_tenant_id_created_at",
                schema: "hrm",
                table: "privileged_action_events",
                columns: new[] { "tenant_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_evidence",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "legal_holds",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "privileged_action_events",
                schema: "hrm");

            migrationBuilder.DropIndex(
                name: "IX_audit_entries_tenant_id_created_at",
                schema: "hrm",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "IX_audit_entries_tenant_id_entity_type_entity_id",
                schema: "hrm",
                table: "audit_entries");
        }
    }
}
