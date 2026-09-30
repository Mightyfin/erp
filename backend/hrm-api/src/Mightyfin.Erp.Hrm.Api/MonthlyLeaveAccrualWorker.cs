using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>Posts configured monthly leave credits, including months missed during downtime.</summary>
public sealed class MonthlyLeaveAccrualWorker(IServiceScopeFactory scopes, ILogger<MonthlyLeaveAccrualWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDuePeriodsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Monthly leave accrual failed; it will retry.");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task RunDuePeriodsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        var starts = await db.LeaveTypes.AsNoTracking()
            .Where(t => t.IsActive && t.AutoAccrueMonthly && t.AccrualStartDate != null)
            .Select(t => t.AccrualStartDate).ToListAsync(ct);
        if (starts.Count == 0) return;

        var first = starts.Min()!.Value;
        var current = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var service = scope.ServiceProvider.GetRequiredService<ITimeService>();
        for (var period = first; period <= current; period = period.AddMonths(1))
        {
            try
            {
                var result = await service.RunScheduledLeaveAccrualAsync(
                    new LeaveAccrualRunRequest(period.ToString("yyyy-MM")), ct);
                logger.LogInformation("Accrued {Days} leave days for {Workers} workers in {Period}.",
                    result.TotalDaysAccrued, result.WorkerCount, result.Period);
            }
            catch (DomainException exception) when (exception.Code is "accrual-period-exists" or "accrual-no-types")
            {
                // A completed period is deliberately immutable; the unique run key also guards races.
            }
            catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
                { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
            {
                logger.LogInformation("Another instance completed leave accrual for {Period}.", period);
            }
        }
    }
}
