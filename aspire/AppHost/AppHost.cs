using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var db = postgres.AddDatabase("db");

var localstack = builder.AddContainer("localstack", "docker.io/localstack/localstack:latest")
       .WithEnvironment("SERVICES", "s3")
       .WithHttpEndpoint(port: 4566, targetPort: 4566);

builder.AddProject<Projects.Api>("api")
    .WithReference(db)
    .WaitFor(postgres)
    .WaitFor(localstack);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(db)
    .WaitFor(postgres)
    .WaitFor(localstack);

builder.Build().Run();