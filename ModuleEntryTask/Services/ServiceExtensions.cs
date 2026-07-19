namespace ModuleEntryTask.Services;

public static class ServiceExtensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        var providerUrl = builder.Configuration["PROVIDER_URL"]
            ?? throw new InvalidOperationException("PROVIDER_URL is not configured.");

        builder.Services.AddHttpClient<ProviderClient>(client =>
        {
            client.BaseAddress = new Uri(providerUrl);
        });

        builder.Services.AddScoped<OperationService>();
        builder.Services.AddScoped<ReceiptService>();
        builder.Services.AddHostedService<SubmitWorker>();
    }
}
