using System.Text.Json;
using Api.Common;
using Api.Features.Cancel;
using Api.Features.Enqueue;
using Api.Features.List;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AppDbContext>("db");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
app.MapDefaultEndpoints();

// Automatically apply migrations in Development for convenience
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
}

app.MapEnqueueEndpoint();
app.MapListEndpoint();
app.MapCancelEndpoint();

app.Run();

public partial class Program { }
