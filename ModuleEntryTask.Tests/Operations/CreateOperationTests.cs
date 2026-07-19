using System.Net;
using System.Net.Http.Json;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Operations;

public class CreateOperationTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Returns201_WithCreatedStatus()
    {
        var response = await Client.PostAsJsonAsync("/operations", new
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
    public async Task DuplicateId_Returns409()
    {
        var payload = new { operationId = "op-dup", amount = "50.00", currency = "RUB", description = "x" };

        await Client.PostAsJsonAsync("/operations", payload);
        var response = await Client.PostAsJsonAsync("/operations", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCurrency_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/operations", new
        {
            operationId = "op-bad-currency",
            amount = "100.00",
            currency = "USD",
            description = "x"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingFields_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/operations", new { operationId = "op-missing" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
