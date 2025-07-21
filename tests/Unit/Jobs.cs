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
}