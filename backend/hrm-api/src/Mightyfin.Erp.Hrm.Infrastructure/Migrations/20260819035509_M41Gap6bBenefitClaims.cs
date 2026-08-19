using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M41Gap6bBenefitClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "benefit_types",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    annual_cap = table.Column<decimal>(type: "numeric", nullable: false),
                    requires_evidence = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefit_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "benefit_allowances",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    benefit_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    annual_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefit_allowances", x => x.id);
                    table.ForeignKey(
                        name: "FK_benefit_allowances_benefit_types_benefit_type_id",
                        column: x => x.benefit_type_id,
                        principalSchema: "hrm",
                        principalTable: "benefit_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_benefit_allowances_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "benefit_claims",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    benefit_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_claimed = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    evidence_attached = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    approved_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    created_by_subject_id = table.Column<string>(type: "text", nullable: true),
                    decided_by_subject_id = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_by_subject_id = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefit_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_benefit_claims_benefit_types_benefit_type_id",
                        column: x => x.benefit_type_id,
                        principalSchema: "hrm",
                        principalTable: "benefit_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_benefit_claims_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_benefit_allowances_benefit_type_id",
                schema: "hrm",
                table: "benefit_allowances",
                column: "benefit_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_allowances_tenant_id_worker_id_benefit_type_id_year",
                schema: "hrm",
                table: "benefit_allowances",
                columns: new[] { "tenant_id", "worker_id", "benefit_type_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_benefit_allowances_worker_id",
                schema: "hrm",
                table: "benefit_allowances",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_claims_benefit_type_id",
                schema: "hrm",
                table: "benefit_claims",
                column: "benefit_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_claims_tenant_id_worker_id_status",
                schema: "hrm",
                table: "benefit_claims",
                columns: new[] { "tenant_id", "worker_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_benefit_claims_worker_id",
                schema: "hrm",
                table: "benefit_claims",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_benefit_types_tenant_id_code",
                schema: "hrm",
                table: "benefit_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "benefit_allowances",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "benefit_claims",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "benefit_types",
                schema: "hrm");
        }
    }
}
