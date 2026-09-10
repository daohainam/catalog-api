namespace ProductCatalog.Infrastructure.Entity;
public class LogTailingOutboxMessage
{
    public Guid Id { get; set; }
    public DateTime CreationDate { get; set; }
    public string Payload { get; set; } = default!;
    public string PayloadType { get; set; } = default!;

    /// <summary>
    /// Set once the event has been published successfully. Rows with a null value
    /// are picked up again by the outbox sweep, which is what makes the pipeline
    /// recoverable after a publisher or broker outage.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}
