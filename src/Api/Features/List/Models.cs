using Domain;

namespace Api.Features.List;

public record JobResponse(
    Guid JobId,
    string Type,
    string ImgUrl,
    JobStatus Status,
    string ResultFile,
    long CreatedAt,
    long UpdatedAt,
    long? DurationMs
);