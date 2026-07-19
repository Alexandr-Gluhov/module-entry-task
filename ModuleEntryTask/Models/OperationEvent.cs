namespace ModuleEntryTask.Models;

public class OperationEvent
{
    public int Id { get; set; }
    public EventType Type { get; set; }
    public OperationStatus? FromStatus { get; set; }
    public OperationStatus? ToStatus { get; set; }
    public string OperationId { get; set; } = null!;
    public Operation Operation { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
}