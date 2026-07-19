using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Data;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public class SubmitWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubmitWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingIntentsAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingIntentsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var providerClient = scope.ServiceProvider.GetRequiredService<ProviderClient>();

        var now = DateTime.UtcNow;

        var intents = await db.SubmitIntents
            .Include(i => i.Operation)
            .Where(i => i.RetryAfter == null || i.RetryAfter <= now)
            .ToListAsync(stoppingToken);

        foreach (var intent in intents.TakeWhile(_ => !stoppingToken.IsCancellationRequested))
        {
            await ProcessIntentAsync(db, providerClient, intent, stoppingToken);
        }
    }

    private async Task ProcessIntentAsync(
        ApplicationDbContext db,
        ProviderClient providerClient,
        SubmitIntent intent,
        CancellationToken stoppingToken)
    {
        var operation = intent.Operation;

        if (operation.Status is OperationStatus.Completed or OperationStatus.Rejected)
        {
            db.SubmitIntents.Remove(intent);
            await db.SaveChangesAsync(stoppingToken);
            return;
        }

        try
        {
            var result = await providerClient.SubmitPaymentAsync(operation);

            if (result != null)
            {
                if (operation.ProviderPaymentId == null)
                    operation.ProviderPaymentId = result.ProviderPaymentId;

                db.SubmitIntents.Remove(intent);
                await db.SaveChangesAsync(stoppingToken);

                logger.LogInformation(
                    "Payment submitted for operation {OperationId}, providerPaymentId: {ProviderPaymentId}",
                    operation.Id, result.ProviderPaymentId);
            }
            else
            {
                await ScheduleRetryAsync(db, intent, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to submit payment for operation {OperationId}, attempt {AttemptCount}",
                operation.Id, intent.AttemptCount + 1);

            await ScheduleRetryAsync(db, intent, stoppingToken);
        }
    }

    private static async Task ScheduleRetryAsync(
        ApplicationDbContext db,
        SubmitIntent intent,
        CancellationToken stoppingToken)
    {
        intent.AttemptCount++;

        var delaySeconds = Math.Min(5 * Math.Pow(2, intent.AttemptCount - 1), 300);
        intent.RetryAfter = DateTime.UtcNow.AddSeconds(delaySeconds);

        await db.SaveChangesAsync(stoppingToken);
    }
}
