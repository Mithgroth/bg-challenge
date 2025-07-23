using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Dapper;
using Domain;
using Npgsql;

namespace Worker;

public class JobProcessingService(
    NpgsqlDataSource dataSource,
    IAmazonS3 s3Client,
    ILogger<JobProcessingService> logger,
    HttpClient httpClient)
    : BackgroundService
{
    private readonly string _workerSalt = Random.Shared.Next().ToString("x8");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting JobProcessingService with worker salt: {WorkerSalt}", _workerSalt);

        await EnsureBucketExistsAsync(stoppingToken);
        await RescueOrphanedJobs(stoppingToken);
        await ProcessAvailableJobs(stoppingToken);

        await using var connection = await dataSource.OpenConnectionAsync(stoppingToken);
        await using var listenCommand = new NpgsqlCommand("LISTEN jobs_channel", connection);
        await listenCommand.ExecuteNonQueryAsync(stoppingToken);

        connection.Notification += async (sender, args) =>
        {
            if (args.Channel == "jobs_channel")
            {
                logger.LogInformation("Received job notification for job ID: {JobId}", args.Payload);
                await RescueOrphanedJobs(stoppingToken);
                await ProcessAvailableJobs(stoppingToken);
            }
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            await connection.WaitAsync(stoppingToken);
        }
    }

    private async Task RescueOrphanedJobs(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            const string sql = """
                               SELECT "JobId", "LockKey" 
                               FROM "Jobs"
                               WHERE "Status" = 'Processing' AND "LockKey" IS NOT NULL
                               """;

            var processingJobs = await connection.QueryAsync<(Guid JobId, long LockKey)>(sql);
            foreach (var (jobId, lockKey) in processingJobs)
            {
                // Test if we can acquire the advisory lock - if yes, it's orphaned
                var tryLockSql = "SELECT pg_try_advisory_lock(@lockKey)";
                var canLock = await connection.QuerySingleAsync<bool>(tryLockSql, new { lockKey });

                if (canLock)
                {
                    logger.LogInformation("Rescuing orphaned job {JobId} with lock key {LockKey}", jobId, lockKey);

                    // Job is orphaned - reset it to Queued and release the lock
                    const string rescueSql = """
                                             UPDATE "Jobs"
                                             SET "Status" = 'Queued', "LockKey" = NULL, "UpdatedAt" = @updatedAt
                                             WHERE "JobId" = @jobId
                                             """;

                    await connection.ExecuteAsync(rescueSql, new { jobId, updatedAt = Stopwatch.GetTimestamp() });

                    // Release the advisory lock we just acquired
                    var unlockSql = "SELECT pg_advisory_unlock(@lockKey)";
                    await connection.ExecuteAsync(unlockSql, new { lockKey });
                }
                else
                {
                    logger.LogDebug("Job {JobId} is still being processed by another worker", jobId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during orphaned job rescue pass");
        }
    }

    private async Task ProcessAvailableJobs(CancellationToken cancellationToken)
    {
        while (true)
        {
            var job = await ClaimNextJob(cancellationToken);
            if (job == null)
            {
                break;
            }

            await ProcessJob(job, cancellationToken);
        }
    }

    private async Task<Job?> ClaimNextJob(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string claimSql = """
                                    WITH next AS (
                                      SELECT "JobId","ResultFile"
                                      FROM "Jobs"
                                      WHERE "Status" = 'Queued'
                                      ORDER BY "CreatedAt"
                                      LIMIT 1
                                      FOR UPDATE SKIP LOCKED
                                    )
                                    UPDATE "Jobs"
                                    SET "Status"  = 'Processing',
                                        "LockKey" = hashtextextended(
                                            (next."JobId"::text || next."ResultFile" || @salt), 0
                                          )::bigint,
                                        "UpdatedAt" = @updatedAt
                                    FROM next
                                    WHERE "Jobs"."JobId" = next."JobId"
                                    RETURNING "Jobs"."JobId", "Jobs"."Type", "Jobs"."ImgUrl", "Jobs"."Status", "Jobs"."ResultFile", "Jobs"."LockKey", "Jobs"."CreatedAt", "Jobs"."UpdatedAt"
                                    """;

            var job = await connection.QuerySingleOrDefaultAsync<Job>(claimSql, new { salt = _workerSalt, updatedAt = Stopwatch.GetTimestamp() },
                transaction);

            if (job == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await transaction.CommitAsync(cancellationToken);

            if (job.LockKey.HasValue)
            {
                var lockSql = "SELECT pg_advisory_lock(@lockKey)";
                await connection.ExecuteAsync(lockSql, new { lockKey = job.LockKey.Value });
                logger.LogInformation("Claimed job {JobId} with lock key {LockKey}", job.JobId, job.LockKey);
            }

            return job;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error claiming next job");
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
    }

    private async Task ProcessJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing job {JobId}", job.JobId);

            // Add delay to simulate processing time
            await Task.Delay(5000, cancellationToken);

            var (isValid, error) = await ImageValidation.ValidateImageAsync(job.ImgUrl, httpClient, cancellationToken);
            if (!isValid)
            {
                logger.LogWarning("Image validation failed for job {JobId}: {Error}", job.JobId, error);
                await CompleteJob(job, JobStatus.Failed, cancellationToken);
                return;
            }

            await DownloadAndUploadToS3(job, cancellationToken);
            await CompleteJob(job, JobStatus.Completed, cancellationToken);

            logger.LogInformation("Successfully processed job {JobId}", job.JobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing job {JobId}", job.JobId);
            await CompleteJob(job, JobStatus.Failed, cancellationToken);
        }
    }

    private async Task CompleteJob(Job job, JobStatus finalStatus, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            const string completeSql = """
                                       UPDATE "Jobs"
                                       SET "Status" = @status,
                                           "UpdatedAt" = @updatedAt,
                                           "LockKey" = NULL
                                       WHERE "JobId" = @jobId AND "ResultFile" = @resultFile
                                       """;

            await connection.ExecuteAsync(completeSql, new
            {
                status = finalStatus.ToString(),
                jobId = job.JobId,
                resultFile = job.ResultFile,
                updatedAt = Stopwatch.GetTimestamp()
            }, transaction);

            // Release advisory lock if we have one
            if (job.LockKey.HasValue)
            {
                var unlockSql = "SELECT pg_advisory_unlock(@lockKey)";
                await connection.ExecuteAsync(unlockSql, new { lockKey = job.LockKey.Value }, transaction);
                logger.LogInformation("Released advisory lock {LockKey} for job {JobId}", job.LockKey, job.JobId);
            }

            await transaction.CommitAsync(cancellationToken);

            job.SetStatus(finalStatus);
            job.SetLockKey(null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing job {JobId} with status {Status}", job.JobId, finalStatus);
            throw;
        }
    }

    private async Task DownloadAndUploadToS3(Job job, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(job.ImgUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await using var imageStream = new MemoryStream(imageBytes);

        var request = new PutObjectRequest
        {
            BucketName = "results",
            Key = job.ResultFile,
            InputStream = imageStream,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
        };

        await s3Client.PutObjectAsync(request, cancellationToken);
        logger.LogInformation("Uploaded {FileName} to S3 for job {JobId}", job.ResultFile, job.JobId);
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bucketName = "results";
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName))
            {
                await s3Client.PutBucketAsync(bucketName, cancellationToken);
                logger.LogInformation("Created S3 bucket: {BucketName}", bucketName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure S3 bucket exists");
            throw;
        }
    }
}