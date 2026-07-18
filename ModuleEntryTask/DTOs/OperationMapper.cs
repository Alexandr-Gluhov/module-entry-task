using ModuleEntryTask.Models;

namespace ModuleEntryTask.DTOs;

public static class OperationMapper
{
    public static OperationResponse ToResponse(Operation operation) =>
        new()
        {
            OperationId = operation.Id,
            Amount = operation.Amount.ToString("F2"),
            Currency = operation.Currency.ToString().ToUpper(),
            Description = operation.Description,
            Status = operation.Status.ToString().ToUpper(),
            ProviderPaymentId = operation.ProviderPaymentId,
        };
}
