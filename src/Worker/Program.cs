using Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BackgroundWorkerService>();

var host = builder.Build();
host.Run();
