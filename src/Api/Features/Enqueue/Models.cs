namespace Api.Features.Enqueue;

public record Request(Guid JobId, string Type, string ImgUrl);

public record Response(Guid JobId, string Type, string ImgUrl, string Status, long CreatedAt);