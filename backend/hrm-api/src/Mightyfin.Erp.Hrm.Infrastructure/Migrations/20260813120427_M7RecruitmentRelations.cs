using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7RecruitmentRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "outcome",
                schema: "hrm",
                table: "relations_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "stage_changed_at",
                schema: "hrm",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stage_score",
                schema: "hrm",
                table: "candidates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "outcome",
                schema: "hrm",
                table: "relations_cases");

            migrationBuilder.DropColumn(
                name: "stage_changed_at",
                schema: "hrm",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "stage_score",
                schema: "hrm",
                table: "candidates");
        }
    }
}
