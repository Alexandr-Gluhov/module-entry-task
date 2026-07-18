using ModuleEntryTask.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationDb();
builder.Services.AddHealthChecks();

var app = builder.Build();

await app.MigrateDbAsync();

app.MapHealthChecks("/health");

app.Run();
