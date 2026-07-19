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

        await using var db = Factory.CreateDbContext();
        var operation = await db.Operations.FindAsync("op-recovery");
        operation!.Status = OperationStatus.Processing;

        db.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-recovery",
            AttemptCount = 0,
            RetryAfter = null,
        });

        await db.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var submitWorker = scope.ServiceProvider.GetRequiredService<SubmitWorker>();
        await submitWorker.ProcessOnceAsync(CancellationToken.None);

        await using var db2 = Factory.CreateDbContext();
        var intent = await db2.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == "op-recovery");

        Assert.True(
            intent == null || intent.RetryAfter != null,
            "Worker должен либо удалить intent при успехе, либо запланировать retry при ошибке");
    }

    [Fact]
    public async Task Receipt_DeletesIntent_SoWorkerWontRetry()
    {
        await CreateAndSubmitAsync("op-receipt-deletes-intent");

        await using var db = Factory.CreateDbContext();
        var intent = await db.SubmitIntents.FirstOrDefaultAsync(
            i => i.OperationId == "op-receipt-deletes-intent");
        Assert.NotNull(intent);

        await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-survives",
            operationId = "op-receipt-deletes-intent",
            result = "COMPLETED",
            message = "done",
            occurredAt = DateTime.UtcNow
        });

        await using var db2 = Factory.CreateDbContext();
        var intentAfter = await db2.SubmitIntents.FirstOrDefaultAsync(
            i => i.OperationId == "op-receipt-deletes-intent");
        Assert.Null(intentAfter);
    }

    [Fact]
    public async Task Late202_DoesNotOverwriteCompletedStatus()
    {
        await CreateOperationAsync("op-late-202");

        await using var setupDb = Factory.CreateDbContext();
        var operation = await setupDb.Operations.FindAsync("op-late-202");
        operation!.Status = OperationStatus.Completed;
        operation.ProviderPaymentId = "ppid-already-set";
        await setupDb.SaveChangesAsync();

        await using var intentDb = Factory.CreateDbContext();
        intentDb.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-late-202",
            AttemptCount = 0,
            RetryAfter = null,
        });
        await intentDb.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var submitWorker = scope.ServiceProvider.GetRequiredService<SubmitWorker>();
        await submitWorker.ProcessOnceAsync(CancellationToken.None);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-late-202");

        Assert.Equal(OperationStatus.Completed, op!.Status);
        Assert.Equal("ppid-already-set", op.ProviderPaymentId);

        var intent = await db.SubmitIntents.FirstOrDefaultAsync(
            i => i.OperationId == "op-late-202");
        Assert.Null(intent);
    }

    [Fact]
    public async Task WorkerDoesNotOverwriteExistingProviderPaymentId()
    {
        await CreateOperationAsync("op-no-overwrite");

        await using var setupDb = Factory.CreateDbContext();
        var operation = await setupDb.Operations.FindAsync("op-no-overwrite");
        operation!.Status = OperationStatus.Processing;
        operation.ProviderPaymentId = "ppid-existing";

        setupDb.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-no-overwrite",
            AttemptCount = 0,
            RetryAfter = null,
        });
        await setupDb.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var submitWorker = scope.ServiceProvider.GetRequiredService<SubmitWorker>();
        await submitWorker.ProcessOnceAsync(CancellationToken.None);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-no-overwrite");

        Assert.Equal("ppid-existing", op!.ProviderPaymentId);
    }
}
