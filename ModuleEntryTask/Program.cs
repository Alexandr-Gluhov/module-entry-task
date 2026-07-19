using ModuleEntryTask.Data;
using ModuleEntryTask.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationDb();
builder.AddApplicationServices();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

await app.MigrateDbAsync();

app.MapHealthChecks("/health");
app.MapMetrics();
app.MapControllers();

app.Run();

public partial class Program { }
