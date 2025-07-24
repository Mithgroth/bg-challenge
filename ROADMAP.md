# Step-by-Step Road-map

*(each step follows **1. Write failing TUnit tests → 2. Implementation**)*

---

## Steps

### **Step 0 – Project Structure Init** ✅

1. **Tests** – _none_ (scaffolding only)

2. **Implementation**  
   * Create solution:

     ```bash
     dotnet new aspire -n BgChallenge         # generates AppHost + defaults
     ```

   * Restructure folders/projects:

     ```text
     /aspire/
       ├─ AppHost/            (generated host stays here)
       └─ ServiceDefaults/    (generated)
     /src/
       ├─ Domain/             (new class-lib:   dotnet new classlib -n Domain)
       ├─ Api/                (rename generated Service1 → Api)
       └─ Worker/             (new worker-svc:  dotnet new worker    -n Worker)
     /tests/
       ├─ Unit/               (dotnet new tunit -n Unit)
       └─ Integration/        (dotnet new tunit -n Integration)
     ```

   * Update solution refs:  

     ```bash
     dotnet sln BgChallenge.sln add src/Domain/Domain.csproj
     dotnet sln BgChallenge.sln add src/Api/Api.csproj
     dotnet sln BgChallenge.sln add src/Worker/Worker.csproj
     dotnet sln BgChallenge.sln add tests/Unit/Unit.csproj
     dotnet sln BgChallenge.sln add tests/Integration/Integration.csproj
     ```

   * Add `Directory.Build.props` for common nullable/implicit-usin​​gs.  
   * Ensure `dotnet build` succeeds.

---

### **Step 1 – Domain Objects & Database** ✅

1. **Tests**  
   * Unit (`Job`): `CanParseFromJson`, `ValidatesJobId`, `ValidatesImgUrl`
   * Unit (`Job`): `ExtractsResultFileFromUrl`, `HandlesUrlWithoutQueryString`, `HandlesComplexQueryParams`
   * Unit (`Job`): `HasDefaultUnknownStatus`, `TracksJobStatus`, `DetectsDuplicatePath`

2. **Implementation**  
   * **Domain** – `Job` entity (rich domain model) with `JobId` (Guid), `Type`, `ImgUrl`, `Status`, `ResultFile` properties.  
   * **Domain** – `Job.ResultFile` property that automatically extracts path from `ImgUrl` (strips query string at `?`).  
   * **Domain** – `JobStatus` enum with status tracking (`Unknown`, `Queued`, `Processing`, `Completed`, `Failed`, `Canceled`).  
   * **Api** – Add EF Core with Aspire PostgreSQL integration, create `AppDbContext` with `Jobs` DbSet.  
   * EF migration for `jobs` table with `UNIQUE(job_id, result_file)` constraint.  
   * **CRITICAL**: `Job.ResultFile` must internally handle URL parsing to extract clean path (e.g., `results_2.png` from `https://...results_2.png?X-Amz-Expires=...`) to avoid signature mismatches.  
   * **Development**: Auto-migration on startup for seamless F5 developer experience.  
   * Reference Job structure:

     ```json
     {
       "jobId": "0197718c-2355-725e-a8e3-7f8dd78c7ff0", 
       "type": "tryon",
       "imgUrl": "https://example.com/path/results_2.png?X-Amz-Expires=..."
     }
     ```

---

### **Step 2 – `/results/enqueue`** ✅

1. **Tests** (`Enqueue`)  
   * `CanAccept`  
   * `IsIdempotent`  
   * Unit (`Job`): `CanExtractResultFile`, `DetectsDuplicatePath`

2. **Implementation**  
   * **Api** – `EnqueueRequest` record; filters `ImageFileGuard`.  
   * Use existing `Job` entity with status tracking.  
   * Return **409 Conflict** on duplicate; insert row then `NOTIFY jobs_channel, id`.

---

### **Step 3 – Worker Processing + Advisory Lock** ✅

1. **Tests** (`DownloadWorker`)
   * `CanCompleteOnNotify`
   * `FailsOnInvalidHead`
   * `RescuesOrphanedProcessingJob`   ← new
   * `LeavesLiveProcessingJobAlone`   ← new

2. **Implementation**

   * **Schema**
     * Add nullable `LockKey bigint` column to `Jobs` via EF migration.
     * Keep PascalCase in the model (`LockKey`).

   * **Worker startup**
     * Generate `workerSalt` once (random 32‑bit hex).
     * **Rescue pass**

       ```sql
       SELECT "JobId","LockKey"
       FROM "Jobs"
       WHERE "Status" = 'Processing';
       ```

       For each row run `pg_try_advisory_lock(:LockKey)`  
         `true`  ⇒ job is orphaned → `UPDATE` back to `Queued`, then `pg_advisory_unlock`  
         `false` ⇒ another worker owns it → leave untouched

   * **Claim query** (inside one transaction)

     ```sql
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
         "UpdatedAt" = extract(epoch from now())::bigint
     FROM next
     WHERE "Jobs"."JobId" = next."JobId"
     RETURNING *;
     ```

     * Commit, then on the same Npgsql connection  
       `SELECT pg_advisory_lock(:LockKey);`

   * **Processing loop**
     * Validate via `HEAD`, stream to S3, retry × 3 (1s → 4s → 8s).

   * **Finish job**

     ```sql
     BEGIN;
       UPDATE "Jobs"
       SET "Status" = 'Completed',
           "UpdatedAt" = extract(epoch from now())::bigint,
           "LockKey" = NULL;
       SELECT pg_advisory_unlock(:LockKey);
     COMMIT;
     ```

   * **Crash scenario**  
     Connection drops → advisory lock released, `LockKey` stays in row → next worker rescues with the startup pass.

   * **LISTEN / NOTIFY**
     * Worker keeps its connection open with `LISTEN jobs_channel`.
     * On `NOTIFY` run the rescue pass first, then the claim query.

---

### **Step 4 – `/results/list` + Delta** ✅

1. **Tests** (`List`)  
   * `Returns304WhenUnchanged`  
   * `ReturnsItemsWhenUpdated`

2. **Implementation**  
   * Use Simon Cropp **Delta** to compute ETag (max `updated_at`).  
   * Expose `startedAt`, `endedAt`, `durationMs`.

---

### **Step 5 – `/results/{id}/cancel`** ✅

1. **Tests** (`Cancel`)  
   * `CanCancelQueuedItem`  
   * `IsIdempotentOnNonQueued`

2. **Implementation**  
   * Endpoint flips status to **Canceled**, sets `canceled_at`; worker skips non-Queued.

---

### **Step 6 – Metrics Fields**

1. **Tests** (`Metrics`)  
   * `MetricsPopulated`

2. **Implementation**  
   * Populate timing columns; map to list response.

---

## Notes & Limits

* **Domain remains pure** – all business logic is in `/src/Domain`.  
* **Memory footprint** – constant via streaming.  
* **Scale path** – swap `IJobQueue` for SQS/Rabbit when sustained load > 500 msg/s.
