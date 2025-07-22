using Api.Features.Enqueue;
using Domain;

namespace Api.Common;

public class ImageFileGuard : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<Request>().FirstOrDefault();
        if (request != null && string.IsNullOrWhiteSpace(request.ImgUrl))
        {
            return Results.BadRequest(new { error = "ImgUrl is required" });
        }

        if (request != null && request.JobId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "JobId cannot be empty" });
        }

        if (request?.ImgUrl != null)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var (isValid, error) = await ImageValidation.ValidateImageAsync(request.ImgUrl, httpClient);
            if (!isValid)
            {
                return Results.BadRequest(new { error });
            }
        }

        return await next(context);
    }
}