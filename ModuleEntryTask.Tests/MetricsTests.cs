using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ModuleEntryTask.Models;
using ModuleEntryTask.Services;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests;

public class MetricsTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Metrics_Returns200()
    {
        var response = await Client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_ContainsPaymentMetrics_AfterWorkerRun()
    {
        await CreateOperationAsync("op-metrics");

        await using var setupDb = Factory.CreateDbContext();
        var op = await setupDb.Operations.FindAsync("op-metrics");
        op!.Status = OperationStatus.Processing;
        setupDb.SubmitIntents.Add(new SubmitIntent
        {
            OperationId = "op-metrics",
            AttemptCount = 0,
            RetryAfter = null,
        });
        await setupDb.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var worker = scope.ServiceProvider.GetRequiredService<SubmitWorker>();
        await worker.ProcessOnceAsync(CancellationToken.None);

        var response = await Client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("payment_pending_operations", body);
        Assert.Contains("payment_retry_attempts_total", body);
    }
}
