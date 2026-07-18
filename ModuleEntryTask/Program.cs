using ModuleEntryTask.Data;
using ModuleEntryTask.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationDb();
builder.AddApplicationServices();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

var app = builder.Build();

await app.MigrateDbAsync();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
