using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;
[DbContext(typeof(HrmDbContext))]
[Migration("20260926080000_UniqueAttendanceDay")]
public sealed class UniqueAttendanceDay : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.DropIndex("IX_attendance_records_tenant_id_worker_id_work_date", "attendance_records", "hrm");
        m.CreateIndex("IX_attendance_records_tenant_id_worker_id_work_date", "attendance_records",
            new[] { "tenant_id", "worker_id", "work_date" }, "hrm", unique: true, filter: "NOT is_archived");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex("IX_attendance_records_tenant_id_worker_id_work_date", "attendance_records", "hrm");
        m.CreateIndex("IX_attendance_records_tenant_id_worker_id_work_date", "attendance_records",
            new[] { "tenant_id", "worker_id", "work_date" }, "hrm");
    }
}
