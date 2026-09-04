using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260821000000_LocalIdentity")]
public partial class LocalIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "local_users",
            schema: "hrm",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "text", nullable: false),
                normalized_email = table.Column<string>(type: "text", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                roles_csv = table.Column<string>(type: "text", nullable: false),
                worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                failed_login_count = table.Column<int>(type: "integer", nullable: false),
                locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                tenant_id = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "text", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_local_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "local_sessions",
            schema: "hrm",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                user_agent = table.Column<string>(type: "text", nullable: true),
                tenant_id = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "text", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_local_sessions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_local_users_tenant_id_normalized_email",
            schema: "hrm",
            table: "local_users",
            columns: new[] { "tenant_id", "normalized_email" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_local_users_tenant_id_worker_id",
            schema: "hrm",
            table: "local_users",
            columns: new[] { "tenant_id", "worker_id" },
            filter: "worker_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_local_sessions_token_hash",
            schema: "hrm",
            table: "local_sessions",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_local_sessions_tenant_id_local_user_id_expires_at",
            schema: "hrm",
            table: "local_sessions",
            columns: new[] { "tenant_id", "local_user_id", "expires_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "local_sessions", schema: "hrm");
        migrationBuilder.DropTable(name: "local_users", schema: "hrm");
    }
}
