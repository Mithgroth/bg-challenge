namespace Domain;

public enum JobStatus
{
    Unknown,
    Queued,
    Processing,
    Completed,
    Failed,
    Canceled
}