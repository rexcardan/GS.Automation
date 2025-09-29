# Selection-To-Worklist Plan

This plan converts user selections (series and instances) into a DICOMAnon.API `WorklistItem`, submits it to the DICOMAnon server, and tracks job status. It aligns with `selectionFeaturePlan.md` by consuming its `SelectionResult` output.

## Goals

- Map selection results to a valid `WorklistItem` with `WorklistInstance` children.
- Include patient context (PatientId) when available.
- Submit the worklist to DICOMAnon via `DAClientSyncService` and poll progress.
- Support optional identity mapping (ID/name remapping) per settings and user choice.
- Provide CLI commands to build-only, export, and monitor jobs.

## Inputs & Outputs

- Input: `SelectionResult`
  - `SeriesUids: HashSet<string>`
  - `Instances: HashSet<(string SeriesUid, string InstanceUid)>`
- Optional Input: `PatientId` (from ESAPI context) and `IdentityMapping` (from settings/UI).
- Output: `WorklistItem` (DICOMAnon.API) and submitted job ID with progress reporting.

## Components

- WorklistBuilder
  - Responsibility: Convert `SelectionResult` (+ patient context) into `WorklistItem`.
  - Rules:
    - For each series UID → `new WorklistInstance { SeriesInstanceUID = seriesUid }`.
    - For each (series, instance) → `new WorklistInstance { SeriesInstanceUID = seriesUid, SOPInstanceUID = instanceUid }`.
    - Set `WorklistItem.PatientId` when known.
    - Ensure the list is de-duplicated and stable.
- DAClientSyncService
  - Responsibility: Wrap `DAClient` lifecycle (settings, add to worklist, run, poll status).
  - Identity Mapping: Conditionally apply `MapToId`, `MapToName`, and update anonymization settings.
  - Methods:
    - `Task<int> Export(WorklistItem wlItem, IdentityMapping idMap, bool isIdentityMappingAllowed)`.
    - `Task<WorklistItemProgress> GetJobStatus(int jobId)`.
  - Settings Consumption:
    - Construct `DAClient` using `IAppSettingsService` values: IP, API key, and port (do not hard-code the port).
- Settings Service
  - Provide API host, API key, and identity mapping configuration.

## Workflow

1) Build Worklist
   - From `SelectionResult`, produce `WorklistItem` with instances.
   - Attach `PatientId` from the ESAPI patient used during selection, if available.
2) Submit and Run
   - Initialize `DAClientSyncService` with app settings.
   - Clear remote worklist, optionally apply identity mapping, push item, update settings (disable modification, clear post-processors), run.
   - Capture returned job ID.
3) Poll Status
   - Query `GetJobStatus(jobId)` until completion or timeout.
   - Surface progress and completion status to the console.

## Error Handling & Edge Cases

- Empty selection → refuse to build or export; show message.
- Mixed selections leading to duplicates → de-duplicate at `SelectionResult` and `WorklistItem` levels.
- Identity mapping disabled or partially filled → ignore mapping fields; never send incomplete mappings.
- Transient API failures → retry `GetJobStatus` with backoff; surface clear errors on `Export` failures.
- Settings changes → recreate client when settings change (per sketch).

## CLI Integration

- Command: `export worklist`
  - Args: `--patient-id <id>`, `--submit` (default true), `--poll`, `--timeout <sec>`, `--identity-mapping on|off`.
  - Options:
    - `--dry-run` to output the `WorklistItem` (JSON) without submission.
    - `--output <path>` to write the JSON.
  - Flow:
    - Read latest `SelectionResult` (or run selection flow if not provided).
    - Build `WorklistItem`.
    - If `--dry-run`, emit and exit.
    - Else submit via `DAClientSyncService.Export`.
    - If `--poll`, call `GetJobStatus` until done.

## Implementation Steps

1) Models & Contracts
   - Define `SelectionResult` (if not already): `SeriesUids`, `Instances`.
   - Add `IWorklistBuilder` with `WorklistItem Build(SelectionResult selection, string patientId)`.
2) WorklistBuilder Implementation
   - Implement rules to create `WorklistItem` and `WorklistInstance`s.
   - Add de-duplication safeguards.
3) DAClientSyncService
   - Integrate code from the sketch; ensure exception handling and logging.
   - Implement identity mapping application and settings update logic.
4) CLI Command(s)
   - Wire arguments, dry-run, submission, and polling.
   - Provide concise console output and error messages.
5) Validation
   - Unit tests: WorklistBuilder conversion logic and de-duplication.
   - Manual validation: end-to-end against a test DICOMAnon instance.

## Pseudo-code

```csharp
// Build worklist from selections
var wl = new WorklistItem { Instances = new List<WorklistInstance>(), PatientId = patientId };
foreach (var s in selection.SeriesUids)
    wl.Instances.Add(new WorklistInstance { SeriesInstanceUID = s });
foreach (var (series, instance) in selection.Instances)
    wl.Instances.Add(new WorklistInstance { SeriesInstanceUID = series, SOPInstanceUID = instance });
wl.Instances = wl.Instances.DistinctBy(x => (x.SeriesInstanceUID, x.SOPInstanceUID)).ToList();

// Submit and poll
var jobId = await client.Export(wl, identityMap, isIdentityMappingAllowed);
if (poll)
{
    WorklistItemProgress p;
    do
    {
        await Task.Delay(1000);
        p = await client.GetJobStatus(jobId);
        AnsiConsole.MarkupLine($"[grey]Status:[/] {p.State} {p.PercentComplete}%");
    } while (!p.IsComplete && !p.IsFailed);
}
```

## Alignment with Selection Plan

- Selection output is standardized as `SelectionResult`.
- The selection plan adds a "Selection-To-Worklist Mapping" phase to produce `WorklistItem`.
- CLI flows can chain: `select` → `export worklist --poll`.
- Shared settings (API host, API key, port, identity mapping) are provided via the same settings service.

## Milestones

1) Implement WorklistBuilder + unit tests.
2) Implement DAClientSyncService wrapper and settings.
3) Wire CLI with dry-run output of WorklistItem.
4) End-to-end submission with polling and clear status reporting.
