using System.Net.Http.Json;
using Api.Features.List;
using Aspire.Hosting.Testing;

namespace Integration;

public class List
{
    private HttpClient? HttpClient { get; set; }

    [Before(Test)]
    public void Setup()
    {
        HttpClient?.Dispose();
        HttpClient = AspireTestFactory.App!.CreateHttpClient("api");
    }

    [After(Test)]
    public void Cleanup()
    {
        HttpClient?.Dispose();
        HttpClient = null;
    }

    [Test]
    public async Task ReturnsArrayFormat()
    {
        // Act
        var response = await HttpClient!.GetAsync("/results/list");
        
        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        
        var jobs = await response.Content.ReadFromJsonAsync<List<JobResponse>>();
        
        await Assert.That(jobs).IsNotNull();
        await Assert.That(jobs).IsTypeOf<List<JobResponse>>();
    }

    [Test]
    public async Task ReturnsJobsWithCorrectFields()
    {
        // Arrange - enqueue a job first
        var jobId = Guid.NewGuid();
        var enqueueRequest = new
        {
            jobId = jobId,
            type = "test",
            imgUrl = "https://memeshapes.com/cdn/shop/articles/rickastley_x1600.png?v=1712534757"
        };

        await HttpClient!.PostAsJsonAsync("/results/enqueue", enqueueRequest);

        // Act
        await Task.Delay(10000);
        var response = await HttpClient!.GetAsync("/results/list");
        
        // Assert  
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        
        var jobs = await response.Content.ReadFromJsonAsync<List<JobResponse>>();
        
        await Assert.That(jobs).IsNotNull();
        await Assert.That(jobs).IsTypeOf<List<JobResponse>>();
        
        // Find our job
        var job = jobs!.FirstOrDefault(j => j.JobId == jobId);
        await Assert.That(job).IsNotNull();
        
        // Verify required fields
        await Assert.That(job!.JobId).IsEqualTo(jobId);
        await Assert.That(job.Type).IsNotNull();
        await Assert.That(job.ImgUrl).IsNotNull();
        await Assert.That(job.Status).IsNotNull();
        await Assert.That(job.ResultFile).IsNotNull();
        await Assert.That(job.CreatedAt).IsGreaterThan(0L);
        await Assert.That(job.UpdatedAt).IsGreaterThan(0L);
        await Assert.That(job.DurationMs).IsNotNull();
        
        // Verify status is string
        await Assert.That(job.Status).IsTypeOf<string>();
    }
}