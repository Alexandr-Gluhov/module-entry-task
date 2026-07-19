using System.Net;
using ModuleEntryTask.Tests.Infrastructure;

namespace ModuleEntryTask.Tests;

public class HealthTests(ApiFactory factory) : TestBase(factory)
{
    [Fact]
    public async Task Health_Returns200()
    {
        var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
