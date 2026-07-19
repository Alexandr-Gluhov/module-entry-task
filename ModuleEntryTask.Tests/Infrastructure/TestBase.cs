using System.Net.Http.Json;

namespace ModuleEntryTask.Tests.Infrastructure;

public abstract class TestBase : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected readonly HttpClient Client;
    protected readonly ApiFactory Factory;

    protected TestBase(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = Factory.CreateDbContext();
        db.SubmitIntents.RemoveRange(db.SubmitIntents);
        db.OperationEvents.RemoveRange(db.OperationEvents);
        db.Operations.RemoveRange(db.Operations);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task CreateOperationAsync(string operationId, string amount = "100.00")
    {
        await Client.PostAsJsonAsync("/operations", new
        {
            operationId,
            amount,
            currency = "RUB",
            description = "test"
        });
    }

    protected async Task CreateAndSubmitAsync(string operationId)
    {
        await CreateOperationAsync(operationId);
        await Client.PostAsync($"/operations/{operationId}/submit", null);
    }
}
