using System.Diagnostics;
using System.Text.Json;

namespace Domain;

public class Job
{
    public Guid JobId { get; private set; }
    public string Type { get; private set; }
    public string ImgUrl { get; private set; }
    public JobStatus Status { get; private set; }
    public string ResultFile { get; private set; }
    public long CreatedAt { get; private set; }
    public long UpdatedAt { get; private set; }
    public long? LockKey { get; private set; }
    public long? CanceledAt { get; private set; }

    // Parameterless constructor for EF
    private Job()
    {
        Type = string.Empty;
        ImgUrl = string.Empty;
        ResultFile = string.Empty;
        Status = JobStatus.Unknown;
    }

    public Job(Guid jobId, string type, string imgUrl, JobStatus status = JobStatus.Unknown)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("JobId cannot be empty", nameof(jobId));
        }

        if (string.IsNullOrWhiteSpace(imgUrl))
        {
            throw new ArgumentException("ImgUrl cannot be empty", nameof(imgUrl));
        }

        if (!Uri.TryCreate(imgUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("ImgUrl must be a valid HTTP or HTTPS URL", nameof(imgUrl));
        }

        JobId = jobId;
        Type = type;
        ImgUrl = imgUrl;
        ResultFile = ExtractResultFileFromUrl(imgUrl);
        Status = status;
        CreatedAt = Stopwatch.GetTimestamp();
        UpdatedAt = Stopwatch.GetTimestamp();
    }

    public static Job FromJson(string json)
    {
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        var jobId = Guid.Parse(root.GetProperty("jobId").GetString()!);
        var type = root.GetProperty("type").GetString()!;
        var imgUrl = root.GetProperty("imgUrl").GetString()!;

        return new Job(jobId, type, imgUrl);
    }

    public void SetStatus(JobStatus status)
    {
        Status = status;
        UpdatedAt = Stopwatch.GetTimestamp();
    }

    public void SetLockKey(long? lockKey)
    {
        LockKey = lockKey;
        UpdatedAt = Stopwatch.GetTimestamp();
    }

    public void RequestCancel()
    {
        CanceledAt = Stopwatch.GetTimestamp();
        UpdatedAt = Stopwatch.GetTimestamp();
    }

    public void Cancel()
    {
        Status = JobStatus.Canceled;
        CanceledAt = Stopwatch.GetTimestamp();
        UpdatedAt = Stopwatch.GetTimestamp();
    }

    public bool IsCancelRequested => CanceledAt.HasValue;

    public long? GetDurationMs()
    {
        if (Status == JobStatus.Completed || Status == JobStatus.Failed || Status == JobStatus.Canceled)
        {
            var duration = UpdatedAt - CreatedAt;
            return (long)(duration / (double)Stopwatch.Frequency * 1000);
        }
        
        return null;
    }

    private static string ExtractResultFileFromUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Split('/');
        return segments[^1]; // Get the last segment (filename)
    }
}