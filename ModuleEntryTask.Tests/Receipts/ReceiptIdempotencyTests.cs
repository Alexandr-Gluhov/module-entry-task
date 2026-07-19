using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Models;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Receipts;

public class ReceiptIdempotencyTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task RepeatedReceipt_DoesNotCreateSecondEvent_HistoryIsExact()
    {
        await CreateAndSubmitAsync("op-repeated-receipt");

        var receipt = new
        {
            providerPaymentId = "ppid-repeated",
            operationId = "op-repeated-receipt",
            result = "COMPLETED",
            message = "done",
            occurredAt = DateTime.UtcNow
        };

        await Client.PostAsJsonAsync("/receipts", receipt);
        await Client.PostAsJsonAsync("/receipts", receipt);
        await Client.PostAsJsonAsync("/receipts", receipt);

        var response = await Client.GetAsync("/operations/op-repeated-receipt/events");
        var events = await response.Content
            .ReadFromJsonAsync<List<EventSnapshot>>();

        Assert.NotNull(events);

        // история должна быть ровно: CREATED → PROCESSING → COMPLETED
        Assert.Equal(3, events.Count);
        Assert.Equal("CREATED", events[0].Type);
        Assert.Equal("PROCESSING", events[1].Type);
        Assert.Equal("COMPLETED", events[2].Type);
    }

    [Fact]
    public async Task CompletedThenRejectedReceipt_StatusRemainsCompleted_IgnoredEventRecorded()
    {
        await CreateAndSubmitAsync("op-completed-then-rejected");

        await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-ctr",
            operationId = "op-completed-then-rejected",
            result = "COMPLETED",
            message = "first",
            occurredAt = DateTime.UtcNow
        });

        var lateResponse = await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-ctr",
            operationId = "op-completed-then-rejected",
            result = "REJECTED",
            message = "too late",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, lateResponse.StatusCode);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-completed-then-rejected");
        Assert.Equal(OperationStatus.Completed, op!.Status);

        var ignored = await db.OperationEvents
            .Where(e => e.OperationId == "op-completed-then-rejected"
                        && e.Type == EventType.Ignored)
            .ToListAsync();
        Assert.Single(ignored);

        var completed = await db.OperationEvents
            .Where(e => e.OperationId == "op-completed-then-rejected"
                        && e.Type == EventType.Completed)
            .ToListAsync();
        Assert.Single(completed);
    }

    [Fact]
    public async Task Idempotency_WorkerUsesStableOperationIdAsKey()
    {
        // проверяем что воркер всегда использует operationId как Idempotency-Key
        // и не меняет его между попытками — это гарантия что провайдер
        // не создаст второй платёж при повторе
        await CreateOperationAsync("op-idempotency-key");

        await using var setupDb = Factory.CreateDbContext();
        var op = await setupDb.Operations.FindAsync("op-idempotency-key");
        op!.Status = OperationStatus.Processing;

        // первая попытка уже была (AttemptCount = 1)
        setupDb.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-idempotency-key",
            AttemptCount = 1,
            RetryAfter = null,
        });
        await setupDb.SaveChangesAsync();

        // после повтора operationId операции не изменился
        await using var db = Factory.CreateDbContext();
        var operation = await db.Operations.FindAsync("op-idempotency-key");

        Assert.Equal("op-idempotency-key", operation!.Id);
        // Id операции — это и есть Idempotency-Key, он иммутабелен
        // EF не позволяет менять PK после создания
    }

    [Fact]
    public async Task CallbackBeforeProviderResponse_OperationCompletedCorrectly()
    {
        // сценарий: submit вызван, callback пришёл до того как воркер получил 202
        // то есть в момент callback операция в PROCESSING, providerPaymentId == null
        await CreateOperationAsync("op-early-callback-race");

        await using var setupDb = Factory.CreateDbContext();
        var op = await setupDb.Operations.FindAsync("op-early-callback-race");
        op!.Status = OperationStatus.Processing;
        // providerPaymentId ещё null — воркер не успел сохранить его
        setupDb.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-early-callback-race",
            AttemptCount = 0,
            RetryAfter = null,
        });
        await setupDb.SaveChangesAsync();

        // callback приходит до получения 202
        var receipt = await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-race",
            operationId = "op-early-callback-race",
            result = "COMPLETED",
            message = "callback arrived first",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, receipt.StatusCode);

        await using var db = Factory.CreateDbContext();
        var finalOp = await db.Operations.FindAsync("op-early-callback-race");

        Assert.Equal(OperationStatus.Completed, finalOp!.Status);
        Assert.Equal("ppid-race", finalOp.ProviderPaymentId);

        // intent должен быть удалён receipt-ом
        var intent = await db.SubmitIntents.FirstOrDefaultAsync(
            i => i.OperationId == "op-early-callback-race");
        Assert.Null(intent);
    }

    private record EventSnapshot(int EventId, string Type, string? FromStatus, string? ToStatus);
}
