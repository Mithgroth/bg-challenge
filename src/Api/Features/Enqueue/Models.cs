namespace Api.Features.Enqueue;

public record Request(Guid JobId, string Type, string ImgUrl);