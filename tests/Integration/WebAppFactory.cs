using Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core.Interfaces;

namespace Integration;

public class WebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    public Task InitializeAsync()
    {
        // You can also override certain services here to mock things out

        // Grab a reference to the server
        // This forces it to initialize.
        // By doing it within this method, it's thread safe.
        // And avoids multiple initialisations from different tests if parallelisation is switched on
        _ = Server;

        return Task.CompletedTask;
    }
    
    // Static shared database options to ensure all contexts use the same database
    public static readonly DbContextOptions<AppDbContext> SharedDbOptions = 
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AppIntegrationTestDb")
            .EnableSensitiveDataLogging()
            .Options;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related services more comprehensively
            var servicesToRemove = services.Where(d =>
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>) ||
                d.ServiceType.FullName?.Contains("Npgsql") == true ||
                d.ImplementationType?.FullName?.Contains("Npgsql") == true ||
                d.ImplementationFactory?.Method.DeclaringType?.FullName?.Contains("Npgsql") == true ||
                d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                d.ImplementationType?.FullName?.Contains("EntityFramework") == true
            ).ToList();
            
            foreach (var service in servicesToRemove)
            {
                services.Remove(service);
            }
            
            services.AddScoped<AppDbContext>(_ => new AppDbContext(SharedDbOptions));
        });
        
        // Override the configuration to prevent AddDbContext from running
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear any existing configuration that might trigger PostgreSQL
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["Environment"] = "Testing",
            });
        });
    }
}