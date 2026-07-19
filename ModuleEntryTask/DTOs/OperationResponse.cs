namespace ModuleEntryTask.DTOs;

public class OperationResponse
{
    public string OperationId { get; set; } = null!;
    public string Amount { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ProviderPaymentId { get; set; }
}
