using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Data;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public class SubmitWorker(
    IServiceScopeFactory scopeFactory,
    PaymentMetrics metrics,
    ILogger<SubmitWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("SubmitWorker stopped gracefully.");
    }

    public async Task ProcessOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var providerClient = scope.ServiceProvider.GetRequiredService<ProviderClient>();

        var now = DateTime.UtcNow;

        var intents = await db.SubmitIntents
            .Include(i => i.Operation)
            .Where(i => i.RetryAfter == null || i.RetryAfter <= now)
            .ToListAsync(stoppingToken);

        metrics.SetPendingOperations(intents.Count);

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
            var response = await providerClient.SubmitPaymentAsync(operation);

            switch (response.Result)
            {
                case ProviderSubmitResult.Success:
                    if (operation.ProviderPaymentId == null)
                        operation.ProviderPaymentId = response.Payment!.ProviderPaymentId;

                    db.SubmitIntents.Remove(intent);
                    await db.SaveChangesAsync(stoppingToken);

                    metrics.IncrementSubmitted();
                    logger.LogInformation(
                        "Payment submitted. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, Attempt: {AttemptCount}",
                        operation.Id, response.Payment!.ProviderPaymentId, intent.AttemptCount + 1);
                    break;

                case ProviderSubmitResult.TransientFailure:
                    metrics.IncrementRetryAttempts();
                    logger.LogWarning(
                        "Transient failure. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, Attempt: {AttemptCount}. Will retry.",
                        operation.Id, operation.ProviderPaymentId, intent.AttemptCount + 1);
                    await ScheduleRetryAsync(db, intent, stoppingToken);
                    break;

                case ProviderSubmitResult.PermanentFailure:
                    metrics.IncrementFailed();
                    logger.LogError(
                        "Permanent failure. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, Attempt: {AttemptCount}.",
                        operation.Id, operation.ProviderPaymentId, intent.AttemptCount + 1);
                    await ScheduleRetryAsync(db, intent, stoppingToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unexpected error. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}, Attempt: {AttemptCount}",
                operation.Id, operation.ProviderPaymentId, intent.AttemptCount + 1);

            await ScheduleRetryAsync(db, intent, stoppingToken);
        }
    }

    private static async Task ScheduleRetryAsync(
        ApplicationDbContext db,
        SubmitIntent intent,
        CancellationToken stoppingToken)
    {
        intent.AttemptCount++;

        var maxDelay = Math.Min(5 * Math.Pow(2, intent.AttemptCount - 1), 300);
        var delaySeconds = Random.Shared.NextDouble() * maxDelay;
        intent.RetryAfter = DateTime.UtcNow.AddSeconds(delaySeconds);

        await db.SaveChangesAsync(stoppingToken);
    }
}
