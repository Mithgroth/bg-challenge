using Domain;
using TUnit.Assertions.AssertConditions.Throws;
using Unit.Fakers;

namespace Unit;

public class Jobs
{
    [Test]
    [MethodDataSource(typeof(JobFaker), nameof(JobFaker.JobTestData))]
    public async Task CanParseFromJson(Guid expectedJobId, string expectedType, string expectedImgUrl)
    {
        var json = $$"""
        {
            "jobId": "{{expectedJobId}}",
            "type": "{{expectedType}}",
            "imgUrl": "{{expectedImgUrl}}"
        }
        """;

        var job = Job.FromJson(json);

        await Assert.That(job.JobId).IsEqualTo(expectedJobId);
        await Assert.That(job.Type).IsEqualTo(expectedType);
        await Assert.That(job.ImgUrl).IsEqualTo(expectedImgUrl);
    }

    [Test]
    public async Task ValidatesJobId()
    {
        var validType = JobFaker.GenerateValidType();
        var validUrl = JobFaker.GenerateValidUrl();

        await Assert.That(() => new Job(Guid.Empty, validType, validUrl))
            .Throws<ArgumentException>();
    }

    [Test]
    [MethodDataSource(typeof(JobFaker), nameof(JobFaker.InvalidUrlTestData))]
    public async Task ValidatesImgUrl(string invalidUrl)
    {
        var validJobId = JobFaker.GenerateValidJobId();
        var validType = JobFaker.GenerateValidType();
        
        await Assert.That(() => new Job(validJobId, validType, invalidUrl))
            .Throws<ArgumentException>();
    }

    [Test]
    [MethodDataSource(typeof(JobFaker), nameof(JobFaker.ObjectPathTestData))]
    public async Task ExtractsObjectPathFromUrl(string imgUrl, string expectedObjectPath)
    {
        var jobId = JobFaker.GenerateValidJobId();
        var type = JobFaker.GenerateValidType();
        
        var job = new Job(jobId, type, imgUrl);

        await Assert.That(job.ObjectPath).IsEqualTo(expectedObjectPath);
    }

    [Test]
    public async Task HasDefaultUnknownStatus()
    {
        var jobId = JobFaker.GenerateValidJobId();
        var type = JobFaker.GenerateValidType();
        var imgUrl = JobFaker.GenerateValidUrl();
        
        var job = new Job(jobId, type, imgUrl);
        
        await Assert.That(job.Status).IsEqualTo(JobStatus.Unknown);
    }

    [Test]
    public async Task TracksJobStatus()
    {
        var jobId = JobFaker.GenerateValidJobId();
        var type = JobFaker.GenerateValidType();
        var imgUrl = JobFaker.GenerateValidUrl();
        var job = new Job(jobId, type, imgUrl);

        await Assert.That(job.Status).IsEqualTo(JobStatus.Unknown);

        job.SetStatus(JobStatus.Queued);
        await Assert.That(job.Status).IsEqualTo(JobStatus.Queued);

        job.SetStatus(JobStatus.Processing);
        await Assert.That(job.Status).IsEqualTo(JobStatus.Processing);

        job.SetStatus(JobStatus.Completed);
        await Assert.That(job.Status).IsEqualTo(JobStatus.Completed);
    }

    [Test]
    public async Task DetectsDuplicatePath()
    {
        var jobId = JobFaker.GenerateValidJobId();
        var type = JobFaker.GenerateValidType();
        var imgUrl1 = "https://example.com/path/file.png?signature=abc123";
        var imgUrl2 = "https://example.com/path/file.png?signature=xyz789";
        
        var job1 = new Job(jobId, type, imgUrl1);
        var job2 = new Job(jobId, type, imgUrl2);
        
        await Assert.That(job1.ObjectPath).IsEqualTo(job2.ObjectPath);
    }
}