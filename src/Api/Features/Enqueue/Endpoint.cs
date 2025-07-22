using Api.Common;
using Api.Data;
using Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Features.Enqueue;

public static class Endpoint
{
    public static void MapEnqueueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/results/enqueue", Handle)
            .AddEndpointFilter<ImageFileGuard>()
            .WithName("EnqueueJob")
            .WithSummary("Enqueue a background job for image processing")
            .WithTags("Results");
    }

    private static async Task<IResult> Handle(
        Request request,
        AppDbContext context)
    {
        try
        {
            var job = new Job(request.JobId, request.Type, request.ImgUrl, JobStatus.Queued);

            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            return Results.Accepted(
                $"/results/{job.JobId}",
                job);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Results.Conflict(new { error = "Job with this ID and result file already exists" });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("An item with the same key has already been added."))
        {
            return Results.Conflict(new { error = "Job with this ID and result file already exists" });
        }
    }
}