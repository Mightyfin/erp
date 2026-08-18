using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M36_PerformanceManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "performance_cycles",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    period_type = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    goal_template = table.Column<string>(type: "text", nullable: true),
                    self_assessment_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    manager_assessment_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    review_meeting_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_cycles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "performance_assessments",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    self_rating = table.Column<string>(type: "text", nullable: true),
                    self_comments = table.Column<string>(type: "text", nullable: true),
                    self_submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    manager_rating = table.Column<string>(type: "text", nullable: true),
                    manager_comments = table.Column<string>(type: "text", nullable: true),
                    manager_submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    manager_name = table.Column<string>(type: "text", nullable: true),
                    final_rating = table.Column<string>(type: "text", nullable: true),
                    final_comments = table.Column<string>(type: "text", nullable: true),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    development_notes = table.Column<string>(type: "text", nullable: true),
                    next_cycle_goals = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_performance_assessments_performance_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "hrm",
                        principalTable: "performance_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_performance_assessments_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performance_goals",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    weight = table.Column<decimal>(type: "numeric", nullable: true),
                    measurement_type = table.Column<string>(type: "text", nullable: false),
                    target_value = table.Column<string>(type: "text", nullable: true),
                    actual_value = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_performance_goals", x => x.id);
                    table.ForeignKey(
                        name: "FK_performance_goals_performance_cycles_cycle_id",
                        column: x => x.cycle_id,
                        principalSchema: "hrm",
                        principalTable: "performance_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_performance_goals_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_performance_assessments_cycle_id",
                schema: "hrm",
                table: "performance_assessments",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_assessments_worker_id",
                schema: "hrm",
                table: "performance_assessments",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_goals_cycle_id",
                schema: "hrm",
                table: "performance_goals",
                column: "cycle_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_goals_worker_id",
                schema: "hrm",
                table: "performance_goals",
                column: "worker_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "performance_assessments",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "performance_goals",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "performance_cycles",
                schema: "hrm");
        }
    }
}
