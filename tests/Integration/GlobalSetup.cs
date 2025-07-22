using System.Diagnostics.CodeAnalysis;
using Api.Data;

[assembly: Retry(3)]
[assembly: ExcludeFromCodeCoverage]

namespace Integration;

public static class TestContext
{
    public static WebAppFactory? WebAppFactory { get; set; }
}

public class GlobalHooks
{
    [Before(TestSession)]
    public static async Task SetUp()
    {
        Console.WriteLine(@"Or you can define methods that do stuff before...");
        TestContext.WebAppFactory = new WebAppFactory();
        await TestContext.WebAppFactory.InitializeAsync();
    }

    [Before(Test)]
    public async Task EnsureDatabaseCreated()
    {
        // Ensure database is created for each test
        // Create context directly using the shared options
        await using var context = new AppDbContext(WebAppFactory.SharedDbOptions);
        await context.Database.EnsureCreatedAsync();
    }

    [After(TestSession)]
    public static async Task CleanUp()
    {
        Console.WriteLine(@"...and after!");
        if (TestContext.WebAppFactory != null)
        {
            await TestContext.WebAppFactory.DisposeAsync();
        }
    }
}