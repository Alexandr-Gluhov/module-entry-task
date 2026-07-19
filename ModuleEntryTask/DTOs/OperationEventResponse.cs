namespace ModuleEntryTask.DTOs;

public class OperationEventResponse
{
    public int EventId { get; set; }
    public string Type { get; set; } = null!;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string Message { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
}
