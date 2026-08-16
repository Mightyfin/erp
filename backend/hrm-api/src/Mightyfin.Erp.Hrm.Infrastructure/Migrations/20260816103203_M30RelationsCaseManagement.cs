using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M30RelationsCaseManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "hrm",
                table: "relations_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confidentiality",
                schema: "hrm",
                table: "relations_cases",
                type: "text",
                nullable: false,
                defaultValue: "restricted");

            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                schema: "hrm",
                table: "relations_cases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "findings",
                schema: "hrm",
                table: "relations_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_subject_id",
                schema: "hrm",
                table: "relations_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "raised_by",
                schema: "hrm",
                table: "relations_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference",
                schema: "hrm",
                table: "relations_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assigned_to_subject_id",
                schema: "hrm",
                table: "protected_disclosures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "hrm",
                table: "protected_disclosures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "protected_disclosure_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    disclosure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_protected_disclosure_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_protected_disclosure_events_protected_disclosures_disclosur~",
                        column: x => x.disclosure_id,
                        principalSchema: "hrm",
                        principalTable: "protected_disclosures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relations_case_access",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relations_case_access", x => x.id);
                    table.ForeignKey(
                        name: "FK_relations_case_access_relations_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "hrm",
                        principalTable: "relations_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relations_case_actions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    owner_subject_id = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relations_case_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_relations_case_actions_relations_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "hrm",
                        principalTable: "relations_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relations_case_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relations_case_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_relations_case_events_relations_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "hrm",
                        principalTable: "relations_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relations_evidence",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    evidence_type = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    classification = table.Column<string>(type: "text", nullable: false),
                    added_by_subject_id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relations_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_relations_evidence_relations_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "hrm",
                        principalTable: "relations_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_relations_cases_tenant_id_reference",
                schema: "hrm",
                table: "relations_cases",
                columns: new[] { "tenant_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_protected_disclosure_events_disclosure_id",
                schema: "hrm",
                table: "protected_disclosure_events",
                column: "disclosure_id");

            migrationBuilder.CreateIndex(
                name: "IX_relations_case_access_case_id",
                schema: "hrm",
                table: "relations_case_access",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_relations_case_access_tenant_id_case_id_actor_subject_id",
                schema: "hrm",
                table: "relations_case_access",
                columns: new[] { "tenant_id", "case_id", "actor_subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_relations_case_actions_case_id",
                schema: "hrm",
                table: "relations_case_actions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_relations_case_events_case_id",
                schema: "hrm",
                table: "relations_case_events",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_relations_evidence_case_id",
                schema: "hrm",
                table: "relations_evidence",
                column: "case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "protected_disclosure_events",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "relations_case_access",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "relations_case_actions",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "relations_case_events",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "relations_evidence",
                schema: "hrm");

            migrationBuilder.DropIndex(
                name: "IX_relations_cases_tenant_id_reference",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "confidentiality",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "due_date",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "findings",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "owner_subject_id",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "raised_by",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "reference",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "assigned_to_subject_id",
                schema: "hrm",
                table: "protected_disclosures");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "hrm",
                table: "protected_disclosures");
        }
    }
}
