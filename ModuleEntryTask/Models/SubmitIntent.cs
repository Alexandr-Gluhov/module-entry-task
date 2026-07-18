namespace ModuleEntryTask.Models;

public class SubmitIntent
{
    public int Id { get; set; }
    public string OperationId { get; set; } = null!;
    public Operation Operation { get; set; } = null!;
    public int AttemptCount { get; set; }
    public DateTime? RetryAfter { get; set; }
}
