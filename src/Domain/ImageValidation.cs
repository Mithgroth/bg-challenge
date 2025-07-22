using System.Net.Http.Headers;
using SignHere;

namespace Domain;

public static class ImageValidation
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB

    /// <summary>
    /// Validates image URL by performing HEAD request and validating response
    /// </summary>
    public static async Task<(bool IsValid, string? Error)> ValidateImageAsync(string? imgUrl, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imgUrl))
        {
            return (false, "Image URL cannot be empty");
        }

        if (!Uri.TryCreate(imgUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return (false, "Image URL must be a valid HTTP or HTTPS URL");
        }

        try
        {
            // Perform HTTP HEAD request
            using var request = new HttpRequestMessage(HttpMethod.Head, imgUrl);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Unable to access the image URL (status: {response.StatusCode})");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType))
            {
                return (false, "Content type is missing");
            }

            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Content type must be an image");
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (!contentLength.HasValue)
            {
                return (false, "Content length is not available");
            }

            if (contentLength.Value > MaxFileSizeBytes)
            {
                return (false, $"File size ({contentLength.Value} bytes) exceeds maximum allowed size ({MaxFileSizeBytes} bytes)");
            }

            if (contentLength.Value <= 0)
            {
                return (false, "File size must be greater than 0");
            }

            // Perform partial GET request to validate magic bytes
            var getRequest = new HttpRequestMessage(HttpMethod.Get, imgUrl);
            getRequest.Headers.Range = new RangeHeaderValue(0, 8192); // First 8KB
            using var getResponse = await httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!getResponse.IsSuccessStatusCode)
            {
                return (false, "Unable to download image bytes for validation");
            }

            // Check magic bytes to verify it's actually an image
            await using var rawStream = await getResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            await rawStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            if (!memoryStream.Is(Category.Image))
            {
                return (false, "URL does not point to a valid image file");
            }

            return (true, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Unable to validate the image URL: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (false, "Request timeout while validating image URL");
        }
        catch (Exception ex)
        {
            return (false, $"Image validation failed: {ex.Message}");
        }
    }
}