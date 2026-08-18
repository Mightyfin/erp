using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M36_PerformanceNullableWorkerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_performance_goals_workers_worker_id",
                schema: "hrm",
                table: "performance_goals");

            migrationBuilder.AlterColumn<Guid>(
                name: "worker_id",
                schema: "hrm",
                table: "performance_goals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_performance_goals_workers_worker_id",
                schema: "hrm",
                table: "performance_goals",
                column: "worker_id",
                principalSchema: "hrm",
                principalTable: "workers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_performance_goals_workers_worker_id",
                schema: "hrm",
                table: "performance_goals");

            migrationBuilder.AlterColumn<Guid>(
                name: "worker_id",
                schema: "hrm",
                table: "performance_goals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_goals_workers_worker_id",
                schema: "hrm",
                table: "performance_goals",
                column: "worker_id",
                principalSchema: "hrm",
                principalTable: "workers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
