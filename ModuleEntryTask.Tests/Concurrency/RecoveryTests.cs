using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModuleEntryTask.Models;
using ModuleEntryTask.Services;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Concurrency;

public class RecoveryTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task AfterRestart_WorkerPicksUpPendingIntent()
    {
        await CreateOperationAsync("op-recovery");

        using var db = Factory.CreateDbContext();
        var operation = await db.Operations.FindAsync("op-recovery");
        operation!.Status = OperationStatus.Processing;

        db.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-recovery",
            AttemptCount = 0,
            RetryAfter = null,
        });

        await db.SaveChangesAsync();

        using var scope = factory.Services.CreateScope();
        var submitWorker = scope.ServiceProvider.GetRequiredService<SubmitWorker>();
        await submitWorker.ProcessOnceAsync(CancellationToken.None);

        using var db2 = Factory.CreateDbContext();
        var intent = await db2.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == "op-recovery");

        Assert.True(intent == null || intent.RetryAfter != null,
            "Worker должен либо удалить intent при успехе, либо запланировать retry при ошибке");
    }

    [Fact]
    public async Task PendingIntentSurvivesRestart_StillInDb()
    {
        await CreateAndSubmitAsync("op-survives");

        using var db = Factory.CreateDbContext();
        var intent = await db.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == "op-survives");
        Assert.NotNull(intent);

        // после получения финального callback intent должен быть удалён
        await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-survives",
            operationId = "op-survives",
            result = "COMPLETED",
            message = "done",
            occurredAt = DateTime.UtcNow
        });

        using var db2 = Factory.CreateDbContext();
        var intentAfter = await db2.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == "op-survives");
        Assert.Null(intentAfter);
    }
}
