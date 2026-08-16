using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M29RecruitmentOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "approved_at",
                schema: "hrm",
                table: "offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "expires_on",
                schema: "hrm",
                table: "offers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "issued_at",
                schema: "hrm",
                table: "offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "responded_at",
                schema: "hrm",
                table: "offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "worker_id",
                schema: "hrm",
                table: "candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "candidate_documents",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_documents_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hrm",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_interviews",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    interview_type = table.Column<string>(type: "text", nullable: false),
                    interviewer_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    overall_score = table.Column<int>(type: "integer", nullable: true),
                    recommendation = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_candidate_interviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_interviews_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hrm",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_stage_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_stage = table.Column<string>(type: "text", nullable: false),
                    to_stage = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_candidate_stage_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_stage_events_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hrm",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preboarding_cases",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preboarding_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_preboarding_cases_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hrm",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_preboarding_cases_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "preboarding_tasks",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    preboarding_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    owner = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_preboarding_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_preboarding_tasks_preboarding_cases_preboarding_case_id",
                        column: x => x.preboarding_case_id,
                        principalSchema: "hrm",
                        principalTable: "preboarding_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_documents_candidate_id",
                schema: "hrm",
                table: "candidate_documents",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_interviews_candidate_id",
                schema: "hrm",
                table: "candidate_interviews",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_stage_events_candidate_id",
                schema: "hrm",
                table: "candidate_stage_events",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_preboarding_cases_candidate_id",
                schema: "hrm",
                table: "preboarding_cases",
                column: "candidate_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preboarding_cases_tenant_id_candidate_id",
                schema: "hrm",
                table: "preboarding_cases",
                columns: new[] { "tenant_id", "candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preboarding_cases_worker_id",
                schema: "hrm",
                table: "preboarding_cases",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_preboarding_tasks_preboarding_case_id",
                schema: "hrm",
                table: "preboarding_tasks",
                column: "preboarding_case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_documents",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "candidate_interviews",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "candidate_stage_events",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "preboarding_tasks",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "preboarding_cases",
                schema: "hrm");

            migrationBuilder.DropColumn(
                name: "approved_at",
                schema: "hrm",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "expires_on",
                schema: "hrm",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "issued_at",
                schema: "hrm",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "responded_at",
                schema: "hrm",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "worker_id",
                schema: "hrm",
                table: "candidates");
        }
    }
}
