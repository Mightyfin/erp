using Mightyfin.Erp.Hrm.Api.Routing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging: structured JSON in production, console otherwise ----------
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        o.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
    });
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Services.AddControllers();
builder.Services.AddOpenApi("hrm");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// ---------- Postgres ----------
var connStr = builder.Configuration.GetConnectionString("Hrm");
if (string.IsNullOrEmpty(connStr))
    throw new InvalidOperationException("ConnectionStrings:Hrm is not configured.");
// Npgsql does not accept libpq URI query parameters (e.g. ?sslmode=disable), so normalize
// the string to the keyword/value format it understands.
if (connStr.StartsWith("postgresql", StringComparison.OrdinalIgnoreCase) && connStr.Contains('?'))
    connStr = NpgsqlConnectionStringNormalizer.Normalize(connStr);
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddDbContext<HrmDbContext>((services, options) =>
{
    options.UseNpgsql(connStr, npgsql => npgsql.MigrationsHistoryTable("__hrm_migrations", "hrm"));
    options.AddInterceptors(services.GetRequiredService<AuditInterceptor>());
});

// ---------- Tenant / auth principal ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantAccessor, PrincipalTenantAccessor>();
builder.Services.AddScoped<IAuthzService, AuthzServiceImpl>();
builder.Services.AddSingleton<IIdProvider, IdProvider>();

// ---------- Services ----------
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();
builder.Services.AddScoped<IWorkerService, WorkerServiceImpl>();
builder.Services.AddScoped<IWorkerResolver, WorkerResolver>();
builder.Services.AddScoped<IWorkerLifecycleService, WorkerLifecycleServiceImpl>();
builder.Services.AddScoped<ITimeRepository, TimeRepository>();
builder.Services.AddScoped<ITimeService, TimeServiceImpl>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddSingleton<ILetterTemplates, LetterTemplatesImpl>();
builder.Services.AddScoped<IMergeDataProvider, MergeDataProviderImpl>();
builder.Services.AddScoped<ILeaveEffectApplier, LeaveEffectApplierImpl>();
builder.Services.AddScoped<IWorkflowService, WorkflowServiceImpl>();
builder.Services.AddScoped<IExperienceRepository, ExperienceRepository>();
builder.Services.AddScoped<IExperienceService, ExperienceServiceImpl>();
builder.Services.AddScoped<IPayrollRepository, PayrollRepository>();
builder.Services.AddScoped<IPayrollService, PayrollServiceImpl>();
builder.Services.AddScoped<IPayslipDocumentService, PayslipDocumentServiceImpl>();
builder.Services.AddScoped<IConfigRepository, ConfigRepository>();
builder.Services.AddScoped<IConfigService, ConfigServiceImpl>();
builder.Services.AddScoped<IConfigAdminService, ConfigAdminServiceImpl>();
builder.Services.AddScoped<IRecruitmentRepository, RecruitmentRepository>();
builder.Services.AddScoped<IRecruitmentService, RecruitmentServiceImpl>();
builder.Services.AddScoped<IRelationsRepository, RelationsRepository>();
builder.Services.AddScoped<IRelationsService, RelationsServiceImpl>();
builder.Services.AddScoped<IDocumentsRepository, DocumentsRepository>();
builder.Services.AddScoped<IDocumentsService, DocumentsServiceImpl>();
builder.Services.AddScoped<IDqService, DqServiceImpl>();
builder.Services.AddScoped<IStatutoryExportService, StatutoryExportServiceImpl>();

// ---------- AuthN: OIDC via Keycloak, with developer-fallback mode ----------
var authMode = builder.Configuration["ERP:AuthMode"] ?? builder.Configuration["HRM:AuthMode"] ?? "oidc";
if (authMode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthentication("dev")
        .AddScheme<DeveloperAuthOptions, DeveloperAuthHandler>("dev", _ => { });
}
else
{
    var authority = builder.Configuration["ERP:OidcAuthority"] ?? builder.Configuration["HRM:OidcAuthority"]
        ?? "http://127.0.0.1.nip.io:18081/realms/mightyfin-sandbox";
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = authority;
            o.RequireHttpsMetadata = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, // workforce realm tokens are introspected by scope claims
                NameClaimType = "preferred_username",
                RoleClaimType = "realm_access.roles",
            };
        });
}
builder.Services.AddAuthorization();

// ---------- Health: readiness probes the database ----------
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString: connStr ?? throw new InvalidOperationException("ConnectionStrings:Hrm is not configured."));

// ---------- API versioning + CORS for the React frontend ----------
// The React HRM UI (TanStack Start) is served from a separate origin; CORS is
// enabled only for the configured origin list so the API refuses stray origins.
var allowedOrigins = (builder.Configuration["HRM:AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()
     .WithExposedHeaders("X-Request-Id", "X-Correlation-Id")));

// Map every route under /api/v{version}/hrm (current = v1) while keeping the
// legacy /api/hrm/* routes available for existing clients. Version resolution
// uses the URL path when present; handlers shared between versions.
builder.Services.AddSingleton(new ApiVersioning { CurrentVersion = 1 });

var app = builder.Build();

// ---------- Cross-cutting middleware ----------
app.UseCors();

// Assign a per-request id (client-supplied X-Request-Id preferred) and log
// every request with method/path/status/duration for observability.
app.Use(async (ctx, next) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? System.Guid.NewGuid().ToString("N")[..12];
    var sw = System.Diagnostics.Stopwatch.StartNew();
    ctx.Response.Headers["X-Request-Id"] = requestId;
    var logger = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["RequestId"] = requestId,
        ["Method"] = ctx.Request.Method,
        ["Path"] = ctx.Request.Path.Value,
    });
    await next(ctx);
    sw.Stop();
    logger.LogInformation("{Method} {Path} -> {Status} in {ElapsedMs}ms",
        ctx.Request.Method, ctx.Request.Path.Value, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/live");

// Readiness probes the database so orchestrators can delay routing traffic
// until migrations have run and Postgres is reachable.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        var status = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "healthy" : "degraded";
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { status, checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), duration = e.Value.Duration.TotalMilliseconds }) });
    },
});

// Root welcome endpoint: confirms a successful deployment and lists supported versions.
app.MapGet("/", () => Results.Ok(new
{
    service = "mightyfin-erp-hrm-api",
    versions = new[] { "v1" },
    documentation = app.Environment.IsDevelopment() ? "/openapi/hrm.json" : null,
}));

// Global error handler: DomainException -> structured ApiError
app.Use(async (ctx, next) =>
{
    try
    {
        await next(ctx);
    }
    catch (DomainException ex)
    {
            var code = ex.Code switch
        {
            "forbidden" => StatusCodes.Status403Forbidden,
            "unauthorized" => StatusCodes.Status401Unauthorized,
            "not-found" or "worker-not-found" or "payroll-run-not-found" or "candidate-not-found" or "pay-period-not-found" or "letter-not-found" or "hr-request-not-found" => StatusCodes.Status404NotFound,
            "conflict" or "employee-no-exists" or "run-already-exists" or "movement-not-allowed" or "offboarding-blocked" => StatusCodes.Status409Conflict,
            "legal-entity-code-taken" or "location-code-taken" or "org-unit-code-taken" or "leave-type-code-taken"
                or "unit-close-backdated" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ApiError(ex.Code, ex.Message, []));
    }
    catch (Exception)
    {
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ApiError("internal-error", "An unexpected error occurred.", []));
    }
});

app.UseAuthentication();
app.UseAuthorization();

// ---------- Route registrations ----------
// URL-based API versioning: the current version is served at /api/v{n}/hrm
// while the legacy /api/hrm prefix stays available for existing clients.
// Both surfaces register the same handlers (shared service methods).
var versioning = app.Services.GetRequiredService<ApiVersioning>();
Routes.HrmPrefix = $"/api/v{versioning.CurrentVersion}/hrm";
Routes.RegisterAll(app);
Routes.HrmPrefix = "/api/hrm"; // legacy prefix, kept for existing clients
Routes.RegisterAll(app);

app.Run();

/// <summary>In-process JWT verification bypass for local development only:
/// mirrors the Go skeleton's ERP_AUTH_MODE=disabled behaviour — never enable in production.</summary>
internal sealed class DeveloperAuthOptions : Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;

internal sealed class DeveloperAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<DeveloperAuthOptions>
{
    public DeveloperAuthHandler(Microsoft.Extensions.Options.IOptionsMonitor<DeveloperAuthOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-user-001"),
                new System.Security.Claims.Claim("preferred_username", "developer"),
                new System.Security.Claims.Claim("tenant", Context.RequestServices.GetRequiredService<ITenantAccessor>().GetTenantId()),
                new System.Security.Claims.Claim("realm_access.roles", "hr_admin"),
                new System.Security.Claims.Claim("worker_id", Context.RequestServices.GetRequiredService<IWorkerResolver>().ResolveDev()),
            ], "dev"));
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(
            new Microsoft.AspNetCore.Authentication.AuthenticationTicket(claims, "dev")));
    }
}
