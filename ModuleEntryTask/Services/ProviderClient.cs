using ModuleEntryTask.DTOs;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public class ProviderClient(HttpClient httpClient)
{
    public async Task<ProviderPaymentResponse?> SubmitPaymentAsync(Operation operation)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = JsonContent.Create(new
            {
                operationId = operation.Id,
                amount = operation.Amount.ToString("F2"),
                currency = operation.Currency.ToString().ToUpper(),
            })
        };

        request.Headers.Add("Idempotency-Key", operation.Id);
        request.Headers.Add("X-Correlation-ID", operation.Id);

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<ProviderPaymentResponse>();
    }
}
