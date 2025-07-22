using Amazon.S3;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("db");

// Add S3 client for LocalStack
builder.Services.AddSingleton<IAmazonS3>(provider =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = "http://localhost:4566", // LocalStack endpoint
        ForcePathStyle = true
    };
    return new AmazonS3Client("test", "test", config);
});

// Add HttpClient
builder.Services.AddHttpClient();

builder.Services.AddHostedService<JobProcessingService>();

var host = builder.Build();
host.Run();
