using System.Net;
using ModuleEntryTask.DTOs;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Services;

public enum ProviderSubmitResult
{
    Success,
    TransientFailure,
    PermanentFailure,
}

public class ProviderSubmitResponse
{
    public ProviderSubmitResult Result { get; init; }
    public ProviderPaymentResponse? Payment { get; init; }
}

public class ProviderClient(HttpClient httpClient)
{
    public async Task<ProviderSubmitResponse> SubmitPaymentAsync(Operation operation)
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

        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(request);
        }
        catch (Exception)
        {
            return new ProviderSubmitResponse { Result = ProviderSubmitResult.TransientFailure };
        }

        if (response.IsSuccessStatusCode)
        {
            var payment = await response.Content.ReadFromJsonAsync<ProviderPaymentResponse>();
            return new ProviderSubmitResponse
            {
                Result = ProviderSubmitResult.Success,
                Payment = payment,
            };
        }

        var isTransient = response.StatusCode == HttpStatusCode.ServiceUnavailable
                          || response.StatusCode == HttpStatusCode.TooManyRequests
                          || (int)response.StatusCode >= 500;

        return new ProviderSubmitResponse
        {
            Result = isTransient
                ? ProviderSubmitResult.TransientFailure
                : ProviderSubmitResult.PermanentFailure,
        };
    }
}
