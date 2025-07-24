using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var db = postgres.AddDatabase("db");

var localstack = builder.AddContainer("localstack", "docker.io/localstack/localstack:latest")
       .WithEnvironment("SERVICES", "s3")
       .WithHttpEndpoint(port: 4566, targetPort: 4566, name: "http");

var api = builder.AddProject<Api>("api")
    .WithReference(db)
    .WaitFor(postgres)
    .WaitFor(localstack);

builder.AddProject<Worker>("worker")
    .WithReference(db)
    .WithEnvironment("LOCALSTACK:URL", localstack.GetEndpoint("http"))
    .WaitFor(postgres)
    .WaitFor(localstack)
    .WaitFor(api);

builder.Build().Run();