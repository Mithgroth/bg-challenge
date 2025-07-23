using Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.List;

public static class Endpoint
{
    public static void MapListEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/results/list", Handle)
            .WithName("ListJobs")
            .WithSummary("List all jobs with statuses")
            .WithTags("Results");
    }

    private static async Task<IResult> Handle(AppDbContext context)
    {
        var jobs = await context.Jobs
            .OrderBy(j => j.CreatedAt)
            .ToListAsync();

        var jobListItems = jobs.Select(job => new JobResponse(
            job.JobId,
            job.Type,
            job.ImgUrl,
            job.Status,
            job.ResultFile,
            job.CreatedAt,
            job.UpdatedAt,
            job.GetDurationMs()
        )).ToList();

        return Results.Ok(jobListItems);
    }
}