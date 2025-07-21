using System.Text.Json;

namespace Domain;

public class Job
{
    public Guid JobId { get; private set; }
    public string Type { get; private set; }
    public string ImgUrl { get; private set; }
    public JobStatus Status { get; private set; }

    public string ObjectPath => ExtractObjectPathFromUrl(ImgUrl);

    // Parameterless constructor for EF
    private Job()
    {
        Type = string.Empty;
        ImgUrl = string.Empty;
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
        Status = status;
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
    }

    private static string ExtractObjectPathFromUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Split('/');
        return segments[^1]; // Get the last segment (filename)
    }
}