using System.ComponentModel.DataAnnotations;

namespace ModuleEntryTask.DTOs;

public class ReceiptRequest
{
    [Required]
    public string ProviderPaymentId { get; set; } = null!;

    [Required]
    public string OperationId { get; set; } = null!;

    [Required]
    public string Result { get; set; } = null!;

    [Required]
    public string Message { get; set; } = null!;

    [Required]
    public DateTime OccurredAt { get; set; }
}
