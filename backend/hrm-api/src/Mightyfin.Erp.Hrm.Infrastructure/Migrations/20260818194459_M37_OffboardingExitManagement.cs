using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M37_OffboardingExitManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offboarding_requests",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    additional_notes = table.Column<string>(type: "text", nullable: true),
                    notice_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    last_working_day = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    approved_by = table.Column<string>(type: "text", nullable: true),
                    approver_name = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    is_final_pay_processed = table.Column<bool>(type: "boolean", nullable: false),
                    checklist_items_completed = table.Column<int>(type: "integer", nullable: false),
                    checklist_items_total = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offboarding_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_offboarding_requests_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exit_interviews",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offboarding_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_for_leaving = table.Column<string>(type: "text", nullable: true),
                    reason_details = table.Column<string>(type: "text", nullable: true),
                    what_went_well = table.Column<string>(type: "text", nullable: true),
                    what_could_improve = table.Column<string>(type: "text", nullable: true),
                    would_recommend = table.Column<string>(type: "text", nullable: true),
                    manager_feedback = table.Column<string>(type: "text", nullable: true),
                    hrm_notes = table.Column<string>(type: "text", nullable: true),
                    interviewed_by = table.Column<string>(type: "text", nullable: true),
                    interviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    OffboardingRequestId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exit_interviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_exit_interviews_offboarding_requests_OffboardingRequestId1",
                        column: x => x.OffboardingRequestId1,
                        principalSchema: "hrm",
                        principalTable: "offboarding_requests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_exit_interviews_offboarding_requests_offboarding_request_id",
                        column: x => x.offboarding_request_id,
                        principalSchema: "hrm",
                        principalTable: "offboarding_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exit_interviews_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offboarding_checklist_items",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offboarding_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    owner = table.Column<string>(type: "text", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_by = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offboarding_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_offboarding_checklist_items_offboarding_requests_offboardin~",
                        column: x => x.offboarding_request_id,
                        principalSchema: "hrm",
                        principalTable: "offboarding_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exit_interviews_offboarding_request_id",
                schema: "hrm",
                table: "exit_interviews",
                column: "offboarding_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exit_interviews_OffboardingRequestId1",
                schema: "hrm",
                table: "exit_interviews",
                column: "OffboardingRequestId1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exit_interviews_worker_id",
                schema: "hrm",
                table: "exit_interviews",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_checklist_items_offboarding_request_id",
                schema: "hrm",
                table: "offboarding_checklist_items",
                column: "offboarding_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_offboarding_requests_worker_id",
                schema: "hrm",
                table: "offboarding_requests",
                column: "worker_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exit_interviews",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "offboarding_checklist_items",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "offboarding_requests",
                schema: "hrm");
        }
    }
}
