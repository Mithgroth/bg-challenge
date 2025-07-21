using Bogus;

namespace Unit.Fakers;

public static class JobFaker
{
    private static readonly Faker _faker = new();

    public static IEnumerable<(Guid JobId, string Type, string ImgUrl)> JobTestData()
    {
        var jobTypes = new[] { "tryon", "fitting", "sizing", "recommendation" };
        var imageExtensions = new[] { "png", "jpg", "jpeg", "webp" };
        
        for (int i = 0; i < 5; i++)
        {
            var jobId = _faker.Random.Guid();
            var type = _faker.PickRandom(jobTypes);
            var filename = $"{_faker.Lorem.Word()}_{_faker.Random.Int(1, 999)}.{_faker.PickRandom(imageExtensions)}";
            var imgUrl = GenerateS3Url(filename);
            
            yield return (jobId, type, imgUrl);
        }
    }

    public static IEnumerable<(string ImgUrl, string ExpectedResultFile)> ResultFileTestData()
    {
        var imageExtensions = new[] { "png", "jpg", "jpeg", "webp" };
        
        for (int i = 0; i < 8; i++)
        {
            var filename = $"{_faker.Lorem.Word()}_{_faker.Random.Int(1, 999)}.{_faker.PickRandom(imageExtensions)}";
            var hasQuery = _faker.Random.Bool(0.75f); // 75% chance of having query params
            
            var imgUrl = hasQuery ? GenerateS3Url(filename) : GenerateSimpleUrl(filename);
            
            yield return (imgUrl, filename);
        }
    }

    public static IEnumerable<string> InvalidUrlTestData()
    {
        yield return "";
        yield return "   ";
        yield return "not-a-url";
        yield return "ftp://invalid-protocol.com/file.png";
        yield return "http://"; // incomplete URL
        yield return _faker.Lorem.Word(); // just random text
    }

    public static string GenerateValidType()
    {
        return _faker.PickRandom(new[] { "tryon", "fitting", "sizing", "recommendation" });
    }

    public static string GenerateValidUrl()
    {
        var filename = $"{_faker.Lorem.Word()}_{_faker.Random.Int(1, 999)}.png";
        return GenerateS3Url(filename);
    }

    public static Guid GenerateValidJobId()
    {
        return _faker.Random.Guid();
    }

    private static string GenerateS3Url(string filename)
    {
        var bucket = _faker.Internet.DomainName();
        var projectId = _faker.Random.Guid();
        var jobId = _faker.Random.Guid();
        
        var queryParams = new Dictionary<string, string>
        {
            ["X-Amz-Expires"] = _faker.Random.Int(3600, 259200).ToString(),
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{_faker.Random.AlphaNumeric(20)}/{_faker.Date.Recent().ToString("yyyyMMdd")}/us-east-1/s3/aws4_request",
            ["X-Amz-Date"] = _faker.Date.Recent().ToString("yyyyMMddTHHmmssZ"),
            ["X-Amz-SignedHeaders"] = "host",
            ["X-Amz-Signature"] = _faker.Random.AlphaNumeric(64)
        };
        
        var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
        return $"https://{bucket}/projects/{projectId}/jobs/{jobId}/results/{filename}?{query}";
    }

    private static string GenerateSimpleUrl(string filename)
    {
        var domain = _faker.Internet.DomainName();
        var path = _faker.Lorem.Slug();
        return $"https://{domain}/{path}/{filename}";
    }
}