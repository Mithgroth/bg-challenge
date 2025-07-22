using System.Net;
using System.Net.Http.Json;

namespace Integration;

public class Enqueue
{
    private HttpClient? HttpClient { get; set; }

    [Before(Test)]
    public void Setup()
    {
        HttpClient?.Dispose();
        HttpClient = TestContext.WebAppFactory!.CreateClient();
    }

    [After(Test)]
    public void Cleanup()
    {
        HttpClient?.Dispose();
        HttpClient = null;
    }

    [Test]
    public async Task CanAccept()
    {
        var request = new
        {
            jobId = Guid.NewGuid(),
            type = "tryon",
            imgUrl = "https://memeshapes.com/cdn/shop/articles/rickastley_x1600.png?v=1712534757"
        };

        var response = await HttpClient!.PostAsJsonAsync("/results/enqueue", request);
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"UNEXPECTED RESPONSE - Status: {response.StatusCode}");
            Console.WriteLine($"Content: {errorContent}");
            Console.WriteLine($"Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
        }

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Accepted);
        
        var content = await response.Content.ReadFromJsonAsync<object>();
        await Assert.That(content).IsNotNull();
    }


    [Test]
    public async Task IsIdempotent()
    {
        var jobId = Guid.NewGuid();
        var request = new
        {
            jobId = jobId,
            type = "tryon",
            imgUrl = "https://memeshapes.com/cdn/shop/articles/rickastley_x1600.png?v=1712534757&signature=abc123"
        };

        // First request
        var response1 = await HttpClient!.PostAsJsonAsync("/results/enqueue", request);
        await Assert.That(response1.StatusCode).IsEqualTo(HttpStatusCode.Accepted);

        // Second request with same jobId but different query params should return conflict
        var request2 = new
        {
            jobId = jobId,
            type = "tryon",
            imgUrl = "https://memeshapes.com/cdn/shop/articles/rickastley_x1600.png?v=1712534757&signature=xyz789"
        };

        var response2 = await HttpClient!.PostAsJsonAsync("/results/enqueue", request2);
        await Assert.That(response2.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

}