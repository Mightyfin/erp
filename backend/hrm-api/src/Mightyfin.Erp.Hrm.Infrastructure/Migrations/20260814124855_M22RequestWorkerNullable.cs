using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M22RequestWorkerNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hr_requests_workers_worker_id",
                schema: "hrm",
                table: "hr_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "worker_id",
                schema: "hrm",
                table: "hr_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_hr_requests_workers_worker_id",
                schema: "hrm",
                table: "hr_requests",
                column: "worker_id",
                principalSchema: "hrm",
                principalTable: "workers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hr_requests_workers_worker_id",
                schema: "hrm",
                table: "hr_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "worker_id",
                schema: "hrm",
                table: "hr_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_hr_requests_workers_worker_id",
                schema: "hrm",
                table: "hr_requests",
                column: "worker_id",
                principalSchema: "hrm",
                principalTable: "workers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
