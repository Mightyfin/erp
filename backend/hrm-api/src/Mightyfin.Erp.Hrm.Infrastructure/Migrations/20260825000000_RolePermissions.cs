using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260825000000_RolePermissions")]
public partial class RolePermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "permissions_csv",
            schema: "hrm",
            table: "tenant_role_assignments",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE hrm.tenant_role_assignments
            SET permissions_csv = role_key
            WHERE permissions_csv = ''
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "permissions_csv",
            schema: "hrm",
            table: "tenant_role_assignments");
    }
}
