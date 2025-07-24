using Amazon.S3;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("db");

builder.Services.AddSingleton<IAmazonS3>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var localstackUrl = configuration["LOCALSTACK:URL"]
                        ?? throw new InvalidOperationException("Missing LOCALSTACK:URL env var");
    
    var config = new AmazonS3Config
    {
        ServiceURL = localstackUrl,
        ForcePathStyle = true
    };
    return new AmazonS3Client("test", "test", config);
});

builder.Services.AddHttpClient();
builder.Services.AddHostedService<JobProcessingService>();

var host = builder.Build();
host.Run();
