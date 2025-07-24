using Aspire.Hosting;
using Aspire.Hosting.Testing;
using TUnit.Core.Interfaces;

namespace Integration;

public class AspireTestFactory : IAsyncInitializer
{
    public static DistributedApplication? App { get; private set; }

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AppHost>();

        App = await appHost.BuildAsync();
        await App.StartAsync();
    }

    public static async Task DisposeAsync()
    {
        if (App != null)
        {
            await App.DisposeAsync();
            App = null;
        }
    }
}