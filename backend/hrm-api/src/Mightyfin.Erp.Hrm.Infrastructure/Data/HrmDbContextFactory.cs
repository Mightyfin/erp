using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>Design-time factory so 'dotnet ef migrations add' works without running DI.</summary>
public sealed class HrmDbContextFactory : IDesignTimeDbContextFactory<HrmDbContext>
{
    public HrmDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connStr = config.GetConnectionString("Hrm")
            ?? "Host=localhost;Database=erp;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql(connStr, npgsql => npgsql.MigrationsHistoryTable("__hrm_migrations", "hrm"))
            .Options;
        return new HrmDbContext(options, new DesignTimeTenantAccessor());
    }
}

internal sealed class DesignTimeTenantAccessor : ITenantAccessor
{
    public string GetTenantId() => Guid.Empty.ToString();
}
