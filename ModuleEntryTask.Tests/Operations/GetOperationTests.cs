using System.Net;
using System.Net.Http.Json;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests.Operations;

public class GetOperationTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task GetById_Returns200_WithCorrectState()
    {
        await CreateOperationAsync("op-get");

        var response = await Client.GetAsync("/operations/op-get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OperationResponse>();
        Assert.Equal("op-get", body!.OperationId);
        Assert.Equal("CREATED", body.Status);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var response = await Client.GetAsync("/operations/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsInOrder_WithMonotonicEventId()
    {
        await CreateAndSubmitAsync("op-events");

        var response = await Client.GetAsync("/operations/op-events/events");

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
        var response = await Client.GetAsync("/operations/nonexistent/events");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
