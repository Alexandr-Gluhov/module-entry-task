using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Operations;

public class SubmitOperationTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task FirstTime_Returns202_AndCreatesIntent()
    {
        await CreateOperationAsync("op-submit-1", "200.00");

        var response = await Client.PostAsync("/operations/op-submit-1/submit", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OperationResponse>();
        Assert.Equal("PROCESSING", body!.Status);

        using var db = Factory.CreateDbContext();
        var intent = await db.SubmitIntents.FirstOrDefaultAsync(i => i.OperationId == "op-submit-1");
        Assert.NotNull(intent);
    }

    [Fact]
    public async Task AlreadyProcessing_Returns200_NoNewIntent()
    {
        await CreateAndSubmitAsync("op-submit-2");

        var response = await Client.PostAsync("/operations/op-submit-2/submit", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = Factory.CreateDbContext();
        var intents = await db.SubmitIntents.Where(i => i.OperationId == "op-submit-2").ToListAsync();
        Assert.Single(intents);
    }

    [Fact]
    public async Task NotFound_Returns404()
    {
        var response = await Client.PostAsync("/operations/nonexistent/submit", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
