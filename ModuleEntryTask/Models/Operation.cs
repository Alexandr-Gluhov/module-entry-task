using System.ComponentModel.DataAnnotations;

namespace ModuleEntryTask.Models;

public class Operation
{
    public string Id { get; set; } = null!;
    public decimal Amount { get; set; }
    public OperationStatus Status { get; set; }
    public Currency Currency { get; set; }
    public string Description { get; set; } = null!;
    public string? ProviderPaymentId { get; set; }
    public SubmitIntent? SubmitIntent { get; set; }
}