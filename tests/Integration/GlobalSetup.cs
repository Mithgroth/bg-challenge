using System.Diagnostics.CodeAnalysis;

[assembly: Retry(3)]
[assembly: ExcludeFromCodeCoverage]

namespace Integration;

public static class TestContext
{
    public static AspireTestFactory? AspireTestFactory { get; set; }
}

public class GlobalHooks
{
    [Before(TestSession)]
    public static async Task SetUp()
    {
        Console.WriteLine(@"Starting Aspire application for integration tests...");
        TestContext.AspireTestFactory = new AspireTestFactory();
        await TestContext.AspireTestFactory.InitializeAsync();
    }

    [After(TestSession)]
    public static async Task CleanUp()
    {
        Console.WriteLine(@"Stopping Aspire application...");
        await AspireTestFactory.DisposeAsync();
    }
}