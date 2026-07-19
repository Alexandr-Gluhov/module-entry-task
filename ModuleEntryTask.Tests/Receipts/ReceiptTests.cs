using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Models;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Receipts;

public class ReceiptTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Completed_Returns204_AndOperationCompleted()
    {
        await CreateAndSubmitAsync("op-receipt-1");

        var response = await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-001",
            operationId = "op-receipt-1",
            result = "COMPLETED",
            message = "Payment completed",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-receipt-1");
        Assert.Equal(OperationStatus.Completed, op!.Status);
        Assert.Equal("ppid-001", op.ProviderPaymentId);
    }

    [Fact]
    public async Task Rejected_Returns204_AndOperationRejected()
    {
        await CreateAndSubmitAsync("op-receipt-2");

        var response = await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-002",
            operationId = "op-receipt-2",
            result = "REJECTED",
            message = "Payment rejected",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-receipt-2");
        Assert.Equal(OperationStatus.Rejected, op!.Status);
    }

    [Fact]
    public async Task Duplicate_Returns204_NoNewEvent()
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

        await Client.PostAsJsonAsync("/receipts", receipt);
        var response = await Client.PostAsJsonAsync("/receipts", receipt);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = Factory.CreateDbContext();
        var events = await db.OperationEvents
            .Where(e => e.OperationId == "op-receipt-dup" && e.Type == EventType.Completed)
            .ToListAsync();
        Assert.Single(events);
    }

    [Fact]
    public async Task LateConflicting_Returns204_StatusUnchanged_WritesIgnoredEvent()
    {
        await CreateAndSubmitAsync("op-receipt-late");

        await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-late",
            operationId = "op-receipt-late",
            result = "COMPLETED",
            message = "ok",
            occurredAt = DateTime.UtcNow
        });

        var response = await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-late",
            operationId = "op-receipt-late",
            result = "REJECTED",
            message = "late",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-receipt-late");
        Assert.Equal(OperationStatus.Completed, op!.Status);

        var ignored = await db.OperationEvents
            .Where(e => e.OperationId == "op-receipt-late" && e.Type == EventType.Ignored)
            .ToListAsync();
        Assert.Single(ignored);
    }

    [Fact]
    public async Task MismatchedProviderPaymentId_Returns409()
    {
        await CreateAndSubmitAsync("op-receipt-mismatch");

        await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-original",
            operationId = "op-receipt-mismatch",
            result = "COMPLETED",
            message = "ok",
            occurredAt = DateTime.UtcNow
        });

        var response = await Client.PostAsJsonAsync("/receipts", new
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
    public async Task EarlyCallback_SetsProviderPaymentId()
    {
        await CreateAndSubmitAsync("op-early-cb");

        var response = await Client.PostAsJsonAsync("/receipts", new
        {
            providerPaymentId = "ppid-early",
            operationId = "op-early-cb",
            result = "COMPLETED",
            message = "early callback",
            occurredAt = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var db = Factory.CreateDbContext();
        var op = await db.Operations.FindAsync("op-early-cb");
        Assert.Equal("ppid-early", op!.ProviderPaymentId);
        Assert.Equal(OperationStatus.Completed, op.Status);
    }
}
