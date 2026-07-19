using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Data;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Models;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests;

public class OperationsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var db = factory.CreateDbContext();
        db.SubmitIntents.RemoveRange(db.SubmitIntents);
        db.OperationEvents.RemoveRange(db.OperationEvents);
        db.Operations.RemoveRange(db.Operations);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ──────────────────────────────────────────────────────────
    // POST /operations
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOperation_Returns201_WithCreatedStatus()
    {
        var response = await _client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-001",
            amount = "100.00",
            currency = "RUB",
            description = "Test payment"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OperationResponse>();
        Assert.NotNull(body);
        Assert.Equal("op-001", body.OperationId);
        Assert.Equal("CREATED", body.Status);
        Assert.Equal("100.00", body.Amount);
        Assert.Null(body.ProviderPaymentId);
    }

    [Fact]
    public async Task CreateOperation_DuplicateId_Returns409()
    {
        var payload = new { operationId = "op-dup", amount = "50.00", currency = "RUB", description = "x" };

        await _client.PostAsJsonAsync("/operations", payload);
        var response = await _client.PostAsJsonAsync("/operations", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateOperation_InvalidCurrency_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-bad-currency",
            amount = "100.00",
            currency = "USD",
            description = "x"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOperation_MissingFields_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/operations", new { operationId = "op-missing" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // POST /operations/{id}/submit
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_FirstTime_Returns202_AndCreatesIntent()
    {
        await _client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-submit-1",
            amount = "200.00",
            currency = "RUB",
            description = "x"
        });

        var response = await _client.PostAsync("/operations/op-submit-1/submit", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OperationResponse>();
        Assert.Equal("PROCESSING", body!.Status);

        using var db = factory.CreateDbContext();
        var intent = await db.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == "op-submit-1");
        Assert.NotNull(intent);
    }

    [Fact]
    public async Task Submit_AlreadyProcessing_Returns200_NoNewIntent()
    {
        await _client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-submit-2",
            amount = "200.00",
            currency = "RUB",
            description = "x"
        });
        await _client.PostAsync("/operations/op-submit-2/submit", null);

        var response = await _client.PostAsync("/operations/op-submit-2/submit", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = factory.CreateDbContext();
        var intents = await db.SubmitIntents.Where(i => i.OperationId == "op-submit-2").ToListAsync();
        Assert.Single(intents);
    }

    [Fact]
    public async Task Submit_NotFound_Returns404()
    {
        var response = await _client.PostAsync("/operations/nonexistent/submit", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // POST /receipts
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Receipt_Completed_Returns204_AndOperationCompleted()
    {
        await CreateAndSubmitAsync("op-receipt-1");

        var response = await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-001",
            operationId = "op-receipt-1",
            result = "COMPLETED",
            message = "Payment completed",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var db = factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-receipt-1");
        Assert.Equal(OperationStatus.Completed, op!.Status);
        Assert.Equal("ppid-001", op.ProviderPaymentId);
    }

    [Fact]
    public async Task Receipt_Rejected_Returns204_AndOperationRejected()
    {
        await CreateAndSubmitAsync("op-receipt-2");

        var response = await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-002",
            operationId = "op-receipt-2",
            result = "REJECTED",
            message = "Payment rejected",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var db = factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-receipt-2");
        Assert.Equal(OperationStatus.Rejected, op!.Status);
    }

    [Fact]
    public async Task Receipt_Duplicate_Returns204_NoNewEvent()
    {
        await CreateAndSubmitAsync("op-receipt-dup");

        var receipt = new
        {
            providerPaymentId = "ppid-dup",
            operationId = "op-receipt-dup",
            result = "COMPLETED",
            message = "ok",
            occurredAt = DateTime.UtcNow
        };

        await _client.PostAsJsonAsync("/receipts", receipt);
        var response = await _client.PostAsJsonAsync("/receipts", receipt);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var db = factory.CreateDbContext();
        var events = await db.OperationEvents
            .Where(e => e.OperationId == "op-receipt-dup" && e.Type == EventType.Completed)
            .ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task Receipt_LateConflicting_Returns204_StatusUnchanged()
    {
        await CreateAndSubmitAsync("op-receipt-late");

        await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-late",
            operationId = "op-receipt-late",
            result = "COMPLETED",
            message = "ok",
            occurredAt = DateTime.UtcNow
        });

        var response = await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-late",
            operationId = "op-receipt-late",
            result = "REJECTED",
            message = "late",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var db = factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-receipt-late");
        Assert.Equal(OperationStatus.Completed, op!.Status);

        var ignored = await db.OperationEvents
            .Where(e => e.OperationId == "op-receipt-late" && e.Type == EventType.Ignored)
            .ToListAsync();
        Assert.Single(ignored);
    }

    [Fact]
    public async Task Receipt_MismatchedProviderPaymentId_Returns409()
    {
        await CreateAndSubmitAsync("op-receipt-mismatch");

        await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-original",
            operationId = "op-receipt-mismatch",
            result = "COMPLETED",
            message = "ok",
            occurredAt = DateTime.UtcNow
        });

        var response = await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-different",
            operationId = "op-receipt-mismatch",
            result = "COMPLETED",
            message = "ok",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_EarlyCallback_SetsProviderPaymentId()
    {
        // callback приходит до того как воркер сохранил providerPaymentId
        await _client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-early-cb",
            amount = "100.00",
            currency = "RUB",
            description = "x"
        });
        await _client.PostAsync("/operations/op-early-cb/submit", null);

        // providerPaymentId ещё не установлен — симулируем раннюю квитанцию
        var response = await _client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-early",
            operationId = "op-early-cb",
            result = "COMPLETED",
            message = "early callback",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var db = factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-early-cb");
        Assert.Equal("ppid-early", op!.ProviderPaymentId);
        Assert.Equal(OperationStatus.Completed, op.Status);
    }

    // ──────────────────────────────────────────────────────────
    // GET /operations/{id} и GET /operations/{id}/events
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOperation_Returns200_WithCorrectState()
    {
        await _client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-get",
            amount = "300.00",
            currency = "RUB",
            description = "Get test"
        });

        var response = await _client.GetAsync("/operations/op-get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OperationResponse>();
        Assert.Equal("op-get", body!.OperationId);
        Assert.Equal("CREATED", body.Status);
    }

    [Fact]
    public async Task GetOperation_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/operations/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsInOrder_WithMonotonicEventId()
    {
        await CreateAndSubmitAsync("op-events");

        var response = await _client.GetAsync("/operations/op-events/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<OperationEventResponse>>();
        Assert.NotNull(events);
        Assert.Equal(2, events.Count);
        Assert.Equal(1, events[0].EventId);
        Assert.Equal(2, events[1].EventId);
        Assert.Equal("CREATED", events[0].Type);
        Assert.Equal("PROCESSING", events[1].Type);
    }

    [Fact]
    public async Task GetEvents_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/operations/nonexistent/events");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // GET /health
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // helpers
    // ──────────────────────────────────────────────────────────

    private async Task CreateAndSubmitAsync(string operationId)
    {
        await _client.PostAsJsonAsync("/operations", new
        {
            operationId,
            amount = "100.00",
            currency = "RUB",
            description = "test"
        });
        await _client.PostAsync($"/operations/{operationId}/submit", null);
    }
}
