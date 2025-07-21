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

### **Step 1 – Bootstrap**

1. **Tests**  
   * `Health.CanRespond` (integration)

2. **Implementation**  
   * In **Api** add minimal `/health` endpoint.  
   * Configure containers in **AppHost**:

     ```csharp
     builder.AddPostgres("db", "postgres:latest")
            .WithVolumeMount("pgdata", "/var/lib/postgresql/data");

     builder.AddContainer("localstack", "localstack/localstack:latest")
            .WithEnvironment("SERVICES", "s3")
            .WithPortBinding(4566, 4566);
     ```

   * Create `TestHostFixture` in `tests/Integration` to spin up AppHost once per test collection; make test green.

---

### **Step 2 – `/results/enqueue`**

1. **Tests** (`Enqueue`)  
   * `CanAccept`  
   * `RejectsArrays`  
   * `IsIdempotent`  
   * Unit (`Result`): `CanExtractObjectPath`, `DetectsDuplicatePath`

2. **Implementation**  
   * **Domain** – `Result` entity + factory stripping query-string.  
   * **Api** – `EnqueueRequest` record; filters `SingleObjectGuard`, `SignHereFilter`.  
   * EF migration for `results` table with `UNIQUE(job_id, object_path)`.  
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
