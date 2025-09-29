# Selection Feature Plan

This plan turns the sketch into a concrete, staged implementation using Spectre.Console and the Eclipse Scripting API (ESAPI) to build a navigable tree of DICOM-relevant items and produce export-ready identifiers.

## Goals

- Provide a two-stage (multi-stage) selection UX with Spectre.Console.
- Allow users to pick from Courses and Series, then drill into plans/children or instances.
- Resolve each selection to concrete export identifiers: SeriesUIDs and (SeriesUID, InstanceUID) pairs.
- Keep code modular: data access (ESAPI), tree building, UI selection, and export mapping are separate.
- Be resilient: handle missing data, large trees, and cancellations gracefully.

## High-Level Architecture

- ESAPI Access Layer: opens patient and enumerates Courses, Plans, and Studies/Series/Instances.
- Tree Model: in-memory representation of selectable nodes (Course, Plan, Dose, StructureSet, Image, Series, Instance).
- TreeBuilder: builds the tree from ESAPI objects with friendly names and UIDs.
- Selection Workflow (Spectre.Console): staged prompts to pick items at each depth.
- Export Mapper: converts final selections into export tasks (SeriesUIDs and Instance tuples).
- CLI Integration: command(s) to run the flow and emit/export results.

## Data Model

- Enum `DicomNodeType`: Course, Plan, Dose, StructureSet, Image, Series, Instance.
- Class `DicomNode`:
  - Id: string (human ID or stable key)
  - DisplayName: string (friendly name for Spectre)
  - Type: DicomNodeType
  - Children: List<DicomNode>
  - Uids: optional fields where applicable (PlanUID, SeriesUID, InstanceUID)
  - Modality: optional (for Series)
  - Metadata: dictionary for extra info (e.g., parent IDs)
- Class `DicomTree`:
  - Courses: List<DicomNode> (Type=Course; children: Plans; plan children: Dose/StructureSet/Image)
  - Series: List<DicomNode> (Type=Series; children: Instances)

Notes:
- Only Dose/StructureSet/Image (under a Plan) and Series/Instance nodes map to export identifiers.
- Course and Plan nodes are selection containers to navigate down a level.

## Tree Building (ESAPI)

- Input: `Application app`, `string patientId`.
- Steps:
  1. Open patient via `app.OpenPatientById(patientId)`.
  2. Build Course subtree:
     - For each Course: add Course node.
     - For each Plan in Course: add Plan node with PlanUID and SeriesUID (if available).
     - Under each Plan, add children:
       - Dose: UID + SeriesUID if present.
       - Structure Set: UID + SeriesUID.
       - Image: SeriesUID (and any useful identifying information like image Id).
  3. Build Series subtree:
     - Enumerate `pat.Studies.SelectMany(s => s.Series)`.
     - For each Series: add Series node with SeriesUID and Modality, friendly DisplayName.
     - Optionally add Instance children: each with InstanceUID + SeriesUID.
       - If instance enumeration is heavy, support lazy-load (load on demand) or page large lists.
  4. Compute Friendly Names:
     - CT/MR/PT/RTIMAGE: prefer first image with valid `Origin` to derive a human-friendly `Id`.
     - RTSTRUCT: try match to `pat.StructureSets` by `HistoryDateTime` to display the associated structure set Id.
     - Fallback to modality + truncated UID or simple index when no name available.
- Error handling: wrap ESAPI calls defensively; continue on partial failures, log issues.

## Selection Workflow (Spectre.Console)

Spectre’s multi-select supports a single-depth display; we will stage the selection:

1) Top-Level Selection (MultiSelectionPrompt<string>)
- Title: "Which items do you want to export?"
- Groups:
  - "Courses": list of Course display names (or Course.Id) — selecting here means you will drill into plans for each selected course.
  - "Series": list of Series display names — selecting here means you will drill into instances for each selected series.
- Result: sets of selected Course IDs and Series UIDs to drill down into.

2) Drill-Down per Course (Sequentially for each selected Course)
- Prompt to choose one or more Plans under the course.
- For each selected Plan, prompt to choose one or more children: Dose, Structure Set, Image.
- The selection of these children determines export targets (by their SeriesUID/UIDs).

3) Drill-Down per Series (Sequentially for each selected Series)
- Prompt to choose one or more Instances.
- If instances are numerous, provide helpers:
  - Add an option to select "All Instances" (maps to export by SeriesUID only).
  - Support paging or chunking when listing instances.

4) Confirmation & Summary
- Present a summary table: counts of selected Plans, Doses, Structure Sets, Images, Series, Instances.
- Show preview of export identifiers that will be emitted.
- Confirm to proceed or go back and edit (offer small loop to re-run a stage if user cancels confirmation).

UX Notes:
- Provide `.InstructionsText("Press <space> to toggle, <enter> to accept")` consistently.
- Use succinct `DisplayName` for readability; include modality tags for Series.
- Handle empty sets by skipping prompts and showing explanatory messages.

## Selection-To-Worklist Mapping

- Selection Output:
  - Produce `SelectionResult` containing:
    - `SeriesUids: HashSet<string>`
    - `Instances: HashSet<(string SeriesUid, string InstanceUid)>`
  - De-duplicate overlap between Series and Instances.
- Worklist Conversion:
  - Convert `SelectionResult` to `WorklistItem` (DICOMAnon.API) with `List<WorklistInstance>`.
  - For each SeriesUid in `SeriesUids`: add `WorklistInstance { SeriesInstanceUID = seriesUid }`.
  - For each tuple in `Instances`: add `WorklistInstance { SeriesInstanceUID = seriesUid, SOPInstanceUID = instanceUid }`.
  - Populate `WorklistItem.PatientId` when available from ESAPI context.
- Plan Children Mapping:
  - Dose/StructureSet/Image selections ultimately resolve to Series- and/or Instance-based identifiers; normalize them into `SelectionResult` first, then into `WorklistItem`.

## CLI Integration

- Command: `select` or `export select`.
- Arguments: `--patient-id <id>`; optional `--all-instances` default off; optional `--no-instance-load` to force series-only selection.
- Output options:
  - Emit JSON file of `SelectionResult` or the final `WorklistItem` to stdout or a path.
  - Optionally invoke an `IExporter`/`DAClientSyncService` to submit the worklist and poll status; otherwise hand off to downstream pipeline.
  - Depends on `IAppSettingsService` for API host, API key, and port configuration (see Test DICOM API plan).

## Implementation Steps

1) Models and Contracts
- Add `DicomNodeType`, `DicomNode`, `DicomTree`, `ExportSelection`.
- Add interfaces: `IPatientDataSource` (ESAPI wrapper), `ITreeBuilder`, `ISelectionWorkflow`, `IExportMapper`.

2) ESAPI Data Access
- Implement `EsapiPatientDataSource` to open patient and expose iterables for Courses, Plans, Series, Instances.
- Ensure proper disposal and threading model consistent with ESAPI requirements.

3) TreeBuilder
- Implement `TreeBuilder.BuildTree(app, patientId)` using the provided sketch as guidance.
- Add FriendlyNameResolver for Series and Plan children.

4) Spectre Selection Workflow
- Implement staged prompts with MultiSelectionPrompt across the three phases.
- Add paging for instances when count > N (configurable threshold, e.g., 500).

5) Export Mapping
- Implement `ExportMapper.MapSelections` to produce `ExportSelection`.
- De-duplicate results and validate required UIDs.

6) CLI Command
- Wire command to run: build tree -> selection workflow -> mapping -> output/dispatch.
- Add `--output` path and `--json` to emit serialized selection for downstream tooling.

7) Logging and Errors
- Add structured logging (e.g., Serilog) or minimal console logging.
- Graceful handling for missing/invalid patient ID and empty trees.

8) Validation
- Unit tests for TreeBuilder with fakes/mocks (no ESAPI dependency in tests).
- Unit tests for ExportMapper de-duplication and mapping rules.
- Manual validation against a test patient in the ESAPI environment.

## Edge Cases & Performance

- Very large series/instance counts: lazy-load instances and offer "All Instances".
- Missing Dose/StructureSet/Image under some plans: show only what exists.
- Duplicate or ambiguous names: include modality/timestamps to disambiguate.
- ESAPI exceptions and permissions: wrap calls and recover where possible.
- Ensure that selected items across Courses and Series don’t conflict; de-duplicate export identifiers.

## Example Prompt Flow (Pseudo-code)

```csharp
// Stage 1: Top-level selection
var courseChoices = tree.Courses.Select(c => c.DisplayName).ToList();
var seriesChoices = tree.Series.Select(s => s.DisplayName).ToList();
var topSelections = AnsiConsole.Prompt(
    new MultiSelectionPrompt<string>()
        .Title("Which items do you want to export?")
        .InstructionsText("Press <space> to toggle, <enter> to accept")
        .AddChoiceGroup("Courses", courseChoices)
        .AddChoiceGroup("Series", seriesChoices));

// Stage 2a: For each selected course
foreach (var course in SelectedCourses(topSelections))
{
    var planChoices = course.Children.Select(p => p.DisplayName).ToList();
    var selectedPlans = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
        .Title($"Select plans for {course.DisplayName}")
        .AddChoices(planChoices));

    foreach (var plan in FindNodes(course, selectedPlans))
    {
        var childChoices = plan.Children.Select(ch => ch.DisplayName).ToList();
        var selectedChildren = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
            .Title($"Select items under {plan.DisplayName}")
            .AddChoices(childChoices));
        // Map selected children to ExportSelection
    }
}

// Stage 2b: For each selected series
foreach (var series in SelectedSeries(topSelections))
{
    var instanceChoices = LoadInstanceDisplayNames(series);
    var includeAll = instanceChoices.Count > Threshold ? ConfirmAllInstances(series) : false;
    if (includeAll) { AddSeries(series.SeriesUID); continue; }

    var selectedInstances = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
        .Title($"Select instances for {series.DisplayName}")
        .AddChoices(instanceChoices));
    // Map selected instances to ExportSelection
}

// Summary & confirmation
ShowSummary(exportSelection);
if (!ConfirmProceed()) { /* allow user to go back or exit */ }
```

## Deliverables

- New classes: models, TreeBuilder, ESAPI wrapper, selection workflow, export mapper.
- CLI command(s) and options, wired into existing exporter CLI.
- Documentation: README section explaining selection flow and examples.
- Unit tests for pure logic components; manual test checklist for ESAPI integration.

## Milestones

1) Tree model + TreeBuilder with friendly names (mocked tests pass).
2) Spectre workflow with sample in-memory tree (no ESAPI yet).
3) ESAPI integration and real tree population.
4) Export mapping and summary confirmation.
5) CLI integration, logging, docs, and manual validation.

```
Outcome: Users select course/plan children and/or series/instances via Spectre prompts, producing validated SeriesUIDs and (SeriesUID, InstanceUID) tuples ready for automated DICOM export.
```
