using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M38_RequisitionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "closing_date",
                schema: "hrm",
                table: "vacancies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "hrm",
                table: "vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requisition_id",
                schema: "hrm",
                table: "vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "requisitions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requisition_no = table.Column<string>(type: "text", nullable: false),
                    job_title = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    replacement_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    headcount = table.Column<int>(type: "integer", nullable: false),
                    grade = table.Column<string>(type: "text", nullable: true),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hiring_manager_name = table.Column<string>(type: "text", nullable: true),
                    budget_annual = table.Column<decimal>(type: "numeric", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    business_case = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    approvers_subject_id = table.Column<string>(type: "text", nullable: true),
                    approver_name = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    returned_reason = table.Column<string>(type: "text", nullable: true),
                    raised_by_subject_id = table.Column<string>(type: "text", nullable: true),
                    raised_by_name = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requisitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_requisitions_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "hrm",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requisition_events",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_requisition_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_requisition_events_requisitions_requisition_id",
                        column: x => x.requisition_id,
                        principalSchema: "hrm",
                        principalTable: "requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vacancies_requisition_id",
                schema: "hrm",
                table: "vacancies",
                column: "requisition_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacancies_tenant_id_requisition_id",
                schema: "hrm",
                table: "vacancies",
                columns: new[] { "tenant_id", "requisition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_requisition_events_requisition_id",
                schema: "hrm",
                table: "requisition_events",
                column: "requisition_id");

            migrationBuilder.CreateIndex(
                name: "IX_requisitions_org_unit_id",
                schema: "hrm",
                table: "requisitions",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_requisitions_tenant_id_requisition_no",
                schema: "hrm",
                table: "requisitions",
                columns: new[] { "tenant_id", "requisition_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_requisitions_tenant_id_status",
                schema: "hrm",
                table: "requisitions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_vacancies_requisitions_requisition_id",
                schema: "hrm",
                table: "vacancies",
                column: "requisition_id",
                principalSchema: "hrm",
                principalTable: "requisitions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vacancies_requisitions_requisition_id",
                schema: "hrm",
                table: "vacancies");

            migrationBuilder.DropTable(
                name: "requisition_events",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "requisitions",
                schema: "hrm");

            migrationBuilder.DropIndex(
                name: "IX_vacancies_requisition_id",
                schema: "hrm",
                table: "vacancies");

            migrationBuilder.DropIndex(
                name: "IX_vacancies_tenant_id_requisition_id",
                schema: "hrm",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "closing_date",
                schema: "hrm",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "hrm",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "requisition_id",
                schema: "hrm",
                table: "vacancies");
        }
    }
}
