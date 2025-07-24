using Api.Common;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Cancel;

public static class Endpoint
{
    public static void MapCancelEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/results/{id:guid}/cancel", Handle)
            .WithName("CancelJob")
            .WithSummary("Cancel a queued background job")
            .WithTags("Results");
    }

    private static async Task<IResult> Handle(
        Guid id,
        AppDbContext context,
        ILogger<object> logger)
    {
        logger.LogInformation("Cancel endpoint called for job ID: {JobId}", id);
        
        var job = await context.Jobs
            .FirstOrDefaultAsync(j => j.JobId == id);

        if (job == null)
        {
            logger.LogWarning("Job not found: {JobId}", id);
            return Results.NotFound(new { error = "Job not found" });
        }

        // If already completed or failed, return current status (idempotent)
        if (job.Status is JobStatus.Completed or JobStatus.Failed)
        {
            return Results.Ok(new { jobId = job.JobId, status = job.Status.ToString() });
        }

        // If queued, immediately cancel
        if (job.Status == JobStatus.Queued)
        {
            job.Cancel();
        }
        else if (job.Status == JobStatus.Processing)
        {
            // Already processing, signal the worker to cancel
            job.RequestCancel();
        }
        else if (job.Status == JobStatus.Canceled)
        {
            // Already canceled, idempotent
            return Results.Ok(new { jobId = job.JobId, status = job.Status.ToString() });
        }

        await context.SaveChangesAsync();

        // Send NOTIFY to signal workers
        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlRawAsync($"NOTIFY cancel_channel, '{id}'");
        }

        logger.LogInformation("Job {JobId} cancel requested, status: {Status}", id, job.Status);
        return Results.Ok(new { 
            jobId = job.JobId,
            status = job.Status.ToString()
        });
    }
}