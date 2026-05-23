namespace ProductCatalog.Infrastructure.Entity;

/// <summary>
/// Represents a failed event that could not be processed after all retry attempts.
/// Used as a dead-letter store for manual investigation and replay.
/// </summary>
public class DeadLetterEvent
{
    public Guid Id { get; set; }
    public string EventTypeName { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public string ErrorMessage { get; set; } = default!;
    public string? StackTrace { get; set; }
    public int RetryCount { get; set; }
    public DateTime FailedAt { get; set; }
    public DateTime? ReprocessedAt { get; set; }
    public bool IsReprocessed { get; set; }
}
