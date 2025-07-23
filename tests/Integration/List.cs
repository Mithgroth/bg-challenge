using System.Text.Json;
using Api.Common;
using Domain;

namespace Integration;

public class List
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
    public async Task ReturnsEmptyArrayWhenNoJobs()
    {
        // Arrange - clear database 
        await using var context = new AppDbContext(WebAppFactory.SharedDbOptions);
        await context.Database.EnsureCreatedAsync();
        
        // Clear any existing data
        context.Jobs.RemoveRange(context.Jobs);
        await context.SaveChangesAsync();

        // Act
        var response = await HttpClient!.GetAsync("/results/list");
        
        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<JsonElement>(content);
        
        await Assert.That(jobs.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(jobs.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task ReturnsJobsWithTimingFields()
    {
        // Arrange - clear database and create jobs
        await using var context = new AppDbContext(WebAppFactory.SharedDbOptions);
        await context.Database.EnsureCreatedAsync();
        
        // Clear any existing data
        context.Jobs.RemoveRange(context.Jobs);
        await context.SaveChangesAsync();
        
        var job1 = new Job(Guid.NewGuid(), "test", "https://ih1.redbubble.net/image.724412595.1147/tst,small,845x845-pad,1000x1000,f8f8f8.u2.jpg");
        var job2 = new Job(Guid.NewGuid(), "test", "https://memeshapes.com/cdn/shop/articles/rickastley_x1600.png?v=1712534757");
        context.Jobs.AddRange(job1, job2);
        await context.SaveChangesAsync();

        // Act
        var response = await HttpClient!.GetAsync("/results/list");
        
        // Assert  
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<JsonElement>(content);
        
        await Assert.That(jobs.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(jobs.GetArrayLength()).IsEqualTo(2);
    }
}