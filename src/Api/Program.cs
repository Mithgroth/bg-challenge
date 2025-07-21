using Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add Aspire PostgreSQL integration
builder.AddNpgsqlDbContext<AppDbContext>("db");

var app = builder.Build();

app.MapDefaultEndpoints();

// Automatically apply migrations in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
}

app.MapGet("/", () => "Hello World!");

app.Run();
