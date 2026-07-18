namespace ModuleEntryTask.Services;

public static class ServiceExtensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<OperationService>();
    }
}
