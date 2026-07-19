using System.Net;
using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Concurrency;

public class ConcurrentSubmitTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task ConcurrentSubmits_CreateExactlyOneIntent()
    {
        await CreateOperationAsync("op-concurrent");

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Client.PostAsync("/operations/op-concurrent/submit", null))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        var accepted = responses.Count(r => r.StatusCode == HttpStatusCode.Accepted);
        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        Assert.Equal(1, accepted);
        Assert.Equal(9, ok);

        using var db = Factory.CreateDbContext();
        var intents = await db.SubmitIntents
            .Where(i => i.OperationId == "op-concurrent")
            .ToListAsync();
        Assert.Single(intents);
    }

    [Fact]
    public async Task ConcurrentSubmits_OperationInProcessingState()
    {
        await CreateOperationAsync("op-concurrent-status");

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Client.PostAsync("/operations/op-concurrent-status/submit", null))
            .ToList();

        await Task.WhenAll(tasks);

        var response = await Client.GetAsync("/operations/op-concurrent-status");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("PROCESSING", body);
    }

    [Fact]
    public async Task ConcurrentSubmits_ExactlyOneSubmitEventRecorded()
    {
        await CreateOperationAsync("op-concurrent-events");

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Client.PostAsync("/operations/op-concurrent-events/submit", null))
            .ToList();

        await Task.WhenAll(tasks);

        using var db = Factory.CreateDbContext();
        var processingEvents = await db.OperationEvents
            .Where(e => e.OperationId == "op-concurrent-events"
                        && e.Type == ModuleEntryTask.Models.EventType.Processing)
            .ToListAsync();

        Assert.Single(processingEvents);
    }
}
