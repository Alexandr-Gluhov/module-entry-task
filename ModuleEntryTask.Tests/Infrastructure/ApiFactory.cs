using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModuleEntryTask.Data;
using ModuleEntryTask.Services;
using Testcontainers.PostgreSql;

namespace ModuleEntryTask.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .Build();

    static ApiFactory()
    {
        Environment.SetEnvironmentVariable("PROVIDER_URL", "http://localhost:1");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PROVIDER_URL"] = "http://localhost:1"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // заменяем DbContext на тестовый с контейнером
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddNpgsql<ApplicationDbContext>(_postgres.GetConnectionString());

            // отключаем воркер — в тестах управляем отправкой вручную
            services.RemoveAll<IHostedService>();
        });

        builder.UseEnvironment("Testing");
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
