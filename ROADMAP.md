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

### **Step 2 – `/results/enqueue`**

1. **Tests** (`Enqueue`)  
   * `CanAccept`  
   * `RejectsArrays`  
   * `IsIdempotent`  
   * Unit (`Job`): `CanExtractResultFile`, `DetectsDuplicatePath`

2. **Implementation**  
   * **Api** – `EnqueueRequest` record; filters `SingleObjectGuard`, `SignHereFilter`.  
   * Use existing `Job` entity with status tracking.  
   * Return **409 Conflict** on duplicate; insert row then `NOTIFY jobs_channel, id`.

---

### **Step 3 – Worker Processing**

1. **Tests** (`DownloadWorker`)  
   * `CanCompleteOnNotify`  
   * `FailsOnInvalidHead`

2. **Implementation**  
   * **Worker**: BackgroundService with dedicated `NpgsqlConnection` → `LISTEN jobs_channel`.  
   * Query rows via `SELECT … FOR UPDATE SKIP LOCKED LIMIT n`.  
   * `HEAD` validation (`200`, `image/*`, ≤ 20 MB).  
   * Stream to LocalStack S3; update status.  
   * Retry × 3 (1 s → 4 s → 8 s).

---

### **Step 4 – `/results/list` + Delta**

1. **Tests** (`List`)  
   * `Returns304WhenUnchanged`  
   * `ReturnsItemsWhenUpdated`

2. **Implementation**  
   * Use Simon Cropp **Delta** to compute ETag (max `updated_at`).  
   * Expose `startedAt`, `endedAt`, `durationMs`.

---

### **Step 5 – `/results/{id}/cancel`**

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
