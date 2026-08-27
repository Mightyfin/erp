using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260827095700_AccountCredentialLinks")]
public partial class AccountCredentialLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "local_credential_links",
            schema: "hrm",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                tenant_id = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "text", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_local_credential_links", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_local_credential_links_token_hash", schema: "hrm", table: "local_credential_links", column: "token_hash", unique: true);
        migrationBuilder.CreateIndex(name: "ix_local_credential_links_tenant_id_local_user_id_expires_at", schema: "hrm", table: "local_credential_links", columns: new[] { "tenant_id", "local_user_id", "expires_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "local_credential_links", schema: "hrm");
}
