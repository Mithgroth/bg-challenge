using System.Net.Http.Headers;
using Api.Features.Enqueue;
using SignHere;

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
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Perform HEAD request to validate file without downloading
                using var response = await httpClient.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, request.ImgUrl));
                if (!response.IsSuccessStatusCode)
                {
                    return Results.BadRequest(new { error = "Unable to access the image URL" });
                }

                // Check if content type indicates an image
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { error = "URL must point to an image file" });
                }

                // Partial GET request (first 64KB by default)
                var getRequest = new HttpRequestMessage(HttpMethod.Get, request.ImgUrl);
                getRequest.Headers.Range = new RangeHeaderValue(0, 8192); // 8KB should be enough
                using var getResponse = await httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                if (!getResponse.IsSuccessStatusCode)
                {
                    return Results.BadRequest(new { error = "Unable to download image bytes" });
                }

                // Is this really an image? Check magic bytes of the file.
                await using var rawStream = await getResponse.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await rawStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                if (!memoryStream.Is(Category.Image))
                {
                    return Results.BadRequest(new { error = "URL does not point to a valid image file" });
                }

                // Validate file size (max 20MB as per roadmap)
                if (response.Content.Headers.ContentLength > 20 * 1024 * 1024)
                {
                    return Results.BadRequest(new { error = "Image file size must not exceed 20MB" });
                }
            }
            catch (HttpRequestException)
            {
                return Results.BadRequest(new { error = "Unable to validate the image URL" });
            }
            catch (TaskCanceledException)
            {
                return Results.BadRequest(new { error = "Request timeout while validating image URL" });
            }
            catch (Exception ex)
            {
                // Catch all other exceptions to prevent 500 errors during testing
                return Results.BadRequest(new { error = $"Image validation failed: {ex.Message}" });
            }
        }

        return await next(context);
    }
}