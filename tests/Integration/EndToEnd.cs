using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;

namespace Integration;

public class EndToEnd
{
    private HttpClient? HttpClient { get; set; }

    [Before(Test)]
    public void Setup()
    {
        HttpClient?.Dispose();
        // Get the API service endpoint from the Aspire app
        HttpClient = AspireTestFactory.App!.CreateHttpClient("api");
    }

    [After(Test)]
    public void Cleanup()
    {
        HttpClient?.Dispose();
        HttpClient = null;
    }

    [Test]
    public async Task CompleteUserJourney()
    {
        var jobId = Guid.NewGuid();
        var request = new
        {
            jobId = jobId,
            type = "test",
            imgUrl = "https://memeshapes.com/cdn/shop/articles/rickastley_x1600.png?v=1712534757"
        };

        // Wait for services to be ready
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try 
        {
            Console.WriteLine("Waiting for worker service to be ready...");
            await AspireTestFactory.App!.ResourceNotifications.WaitForResourceHealthyAsync("worker", cts.Token);
            Console.WriteLine("Worker service is healthy and ready");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Worker service health check failed: {ex.Message}");
            // Continue with test anyway to see what happens
        }

        // Step 1: Enqueue job and get 202
        var enqueueResponse = await HttpClient!.PostAsJsonAsync("/results/enqueue", request, cancellationToken: cts.Token);
        await Assert.That(enqueueResponse.StatusCode).IsEqualTo(HttpStatusCode.Accepted);

        // Step 2: Check initial status - should be Queued
        var listResponse = await HttpClient!.GetAsync("/results/list", cts.Token);
        await Assert.That(listResponse.IsSuccessStatusCode).IsTrue();

        var content = await listResponse.Content.ReadAsStringAsync(cts.Token);
        var jobs = JsonSerializer.Deserialize<JsonElement>(content);
        
        // Find our specific job
        var job = jobs.EnumerateArray().FirstOrDefault(j => j.GetProperty("jobId").GetGuid() == jobId);
        await Assert.That(job.ValueKind).IsNotEqualTo(JsonValueKind.Undefined);
        
        await Assert.That(job.GetProperty("jobId").GetGuid()).IsEqualTo(jobId);
        // Status is serialized as a string representation of the enum
        var statusProperty = job.GetProperty("status");
        var statusString = statusProperty.ValueKind == JsonValueKind.String 
            ? statusProperty.GetString()
            : "Unknown";
        await Assert.That(statusString).IsEqualTo("Queued");

        // Step 3: Poll for status changes - should eventually see Processing then Completed/Failed
        string finalStatus = "Unknown";
        var maxAttempts = 10; // 10 attempts with 1.5 second delay = 15 seconds max (Worker has 5s delay + processing time)
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            await Task.Delay(1500, cts.Token); // Wait 1.5 seconds between polls
            attempt++;

            listResponse = await HttpClient!.GetAsync("/results/list");
            await Assert.That(listResponse.IsSuccessStatusCode).IsTrue();

            content = await listResponse.Content.ReadAsStringAsync();
            jobs = JsonSerializer.Deserialize<JsonElement>(content);
            job = jobs.EnumerateArray().FirstOrDefault(j => j.GetProperty("jobId").GetGuid() == jobId);

            var currentStatusProperty = job.GetProperty("status");
            var currentStatusString = currentStatusProperty.ValueKind == JsonValueKind.String 
                ? currentStatusProperty.GetString()
                : "Unknown";
            finalStatus = currentStatusString ?? "Unknown";

            Console.WriteLine($"Attempt {attempt}: Job status is {finalStatus}");

            // Break when we reach a final status
            if (finalStatus is "Completed" or "Failed")
            {
                break;
            }
        }

        // Step 4: Verify final status and timing
        await Assert.That(finalStatus is "Completed" or "Failed").IsTrue();
        
        // Verify timing fields are populated
        await Assert.That(job.GetProperty("createdAt").GetInt64()).IsGreaterThan(0);
        await Assert.That(job.GetProperty("updatedAt").GetInt64()).IsGreaterThan(0);
        
        // If completed/failed, should have duration
        if (finalStatus is "Completed" or "Failed")
        {
            var durationMs = job.GetProperty("durationMs").GetInt64();
            await Assert.That(durationMs).IsGreaterThan(0);
            Console.WriteLine($"Job completed in {durationMs}ms");
        }

        Console.WriteLine($"End-to-end test completed successfully. Final status: {finalStatus}");
    }
}