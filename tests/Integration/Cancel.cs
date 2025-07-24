using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;

namespace Integration;

/// <summary>
/// These tests need to be run individually
/// </summary>

public class Cancel
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
    public async Task CanCancelQueuedItem()
    {
        // Arrange - enqueue a job first
        var jobId = Guid.NewGuid();
        var enqueueResponse = await HttpClient!.PostAsJsonAsync("/results/enqueue", new
        {
            jobId = jobId,
            type = "tryon",
            imgUrl = "https://httpbin.org/image/png?queued_job=true"
        });
        
        enqueueResponse.EnsureSuccessStatusCode();

        // Act - cancel the job
        var response = await HttpClient!.PostAsync($"/results/{jobId}/cancel", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        
        // Verify job status is now Canceled
        var listResponse = await HttpClient!.GetAsync("/results/list");
        var listContent = await listResponse.Content.ReadAsStringAsync();
        await Assert.That(listContent).Contains("Canceled");
    }

    [Test]
    public async Task IsIdempotentOnNonQueued()
    {
        // Arrange - enqueue and let it get processed or complete
        var jobId = Guid.NewGuid();
        var enqueueResponse = await HttpClient!.PostAsJsonAsync("/results/enqueue", new
        {
            jobId = jobId,
            type = "tryon", 
            imgUrl = "https://httpbin.org/image/png?non_queued=true"
        });
        
        enqueueResponse.EnsureSuccessStatusCode();
        
        // Wait a bit to let it potentially get processed
        await Task.Delay(100);

        // Act - try to cancel (should be idempotent regardless of current status)
        var response1 = await HttpClient!.PostAsync($"/results/{jobId}/cancel", null);
        var response2 = await HttpClient!.PostAsync($"/results/{jobId}/cancel", null);

        // Assert - both calls should succeed (idempotent)
        await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}