using System.ComponentModel.DataAnnotations;

namespace ModuleEntryTask.DTOs;

public class CreateOperationRequest
{
    [Required]
    public string OperationId { get; set; } = null!;

    [Required]
    [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Amount must be a positive decimal with up to 2 decimal places.")]
    public string Amount { get; set; } = null!;

    [Required]
    public string Currency { get; set; } = null!;

    [Required]
    public string Description { get; set; } = null!;
}
