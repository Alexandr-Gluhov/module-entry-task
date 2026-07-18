using ModuleEntryTask.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationDb();

var app = builder.Build();

await app.MigrateDbAsync();

app.Run();
