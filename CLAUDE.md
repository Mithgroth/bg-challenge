# BG Backend Challenge — Execution Plan

*(Read top-to-bottom · finish each step before moving on · **never `git commit` automatically**)*

---

## Ground Rules

| Rule | Detail |
|------|--------|
| **Step-wise workflow** | Complete **Step N** fully before starting the next. |
| **TDD first** | Every step has **1️⃣ Failing tests → 2️⃣ Implementation**. |
| **Manual Git** | Human decides when/what to commit; Claude never runs git commands. |

---

## Tech Baseline (latest images & tooling)

| Item            | Version / Choice |
|-----------------|------------------|
| .NET SDK        | 9.0 · Aspire 9.3 |
| Orchestration   | Aspire **AppHost** (`/aspire`) |
| DB container    | `postgres:latest` |
| Blob mock       | `localstack/localstack:latest` &nbsp;*(S3 service only)* |
| Queue pattern   | Postgres **LISTEN / NOTIFY** |
| Test framework  | **TUnit** |
| Libraries       | SignHere · SimonCropp/Delta |

---

## Solution / Folder Layout

```text
/
├─ aspire/
│  ├─ AppHost/           (Aspire host)
│  └─ ServiceDefaults/
├─ src/
│  ├─ Domain/            (pure business logic / entities)
│  ├─ Api/               (Minimal-API project)
│  └─ Worker/            (BackgroundService project)
└─ tests/
   ├─ Unit/              (TUnit tests targeting Domain only)
   └─ Integration/       (TUnit tests spinning up Api + Worker)
```

## TUnit Conventions

* **Test class names are concise user-journey nouns** (e.g. `Health`, `Enqueue`, `DownloadWorker`)—no “*Tests” suffix.
* **Method names are short camel-cased boolean phrases:** `CanRespond`, `RejectsArrays`, `IsIdempotent`.
* **Global integration-test setup** lives in `tests/Integration/TestHostFixture.cs` (starts Aspire host once per test collection).

---

## How to proceed

Claude must execute the steps **exactly as laid out in [ROADMAP.md](./ROADMAP.md)**, starting with **Step 0 – Project Structure Init** (note: that step has no tests).

---

## Naming Guidelines

* Solution = **BgChallenge**  
* Project names = **Api**, **Worker**, **Domain** (no “BgChallenge.Api”).  
* Request / response records must **not** carry a “Dto” suffix  
  *(e.g. `EnqueueRequest`, not `EnqueueDto`)*.
