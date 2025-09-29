using System;
using System.Linq;
using Spectre.Console;
using DICOMAnon.Exporter.Services;
using DICOMAnon.Exporter.Models;
using DICOMAnon.Exporter.Helpers;

class Entry
{
    static int Main(string[] args)
    {
        var appSettingsService = new AppSettingsService();
        var daService = new DAClientSyncService(appSettingsService);
        string patientIdArg = ParseArg(args, "-id") ?? ParseArg(args, "--id");

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select an action")
                    .AddChoices(new[] {
                        "1) Settings & API test",
                        "2) Selection & Export",
                        "Exit"
                    }));

            if (choice.StartsWith("1"))
            {
                SettingsScreen(appSettingsService, daService);
            }
            else if (choice.StartsWith("2"))
            {
                SelectionAndExportScreen(appSettingsService, daService, patientIdArg);
            }
            else
            {
                break;
            }
        }

        return 0;
    }

    static string ParseArg(string[] args, string key)
    {
        if (args == null || args.Length == 0) return null;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) return args[i + 1];
            }
            else if (args[i].StartsWith(key + "="))
            {
                return args[i].Substring(key.Length + 1);
            }
        }
        return null;
    }

    static void SettingsScreen(IAppSettingsService settings, DAClientSyncService da)
    {
        bool dirty = false;
        while (true)
        {
            var s = settings.AppSettings;
            var table = new Table().Border(TableBorder.Rounded).Title("Current Settings");
            table.AddColumn("Key");
            table.AddColumn("Value");
            table.AddRow("IP Address", s.DICOMAnonIPAddress ?? "");
            table.AddRow("API Key", string.IsNullOrEmpty(s.DICOMAnonAPIKey) ? "" : new string('*', Math.Min(8, s.DICOMAnonAPIKey.Length)) + "…");
            table.AddRow("Port", s.DICOMAnonPort.ToString());
            AnsiConsole.Write(table);

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Settings Menu")
                .AddChoices("Edit IP", "Edit API Key", "Edit Port", "Test Connection", dirty ? "Save" : "Save (no changes)", "Back"));

            if (choice.StartsWith("Edit IP"))
            {
                var val = AnsiConsole.Ask<string>("Enter IP or host:", s.DICOMAnonIPAddress ?? "");
                if (!string.IsNullOrWhiteSpace(val)) { settings.Update(x => x.DICOMAnonIPAddress = val.Trim()); dirty = true; }
            }
            else if (choice.StartsWith("Edit API Key"))
            {
                var val = AnsiConsole.Prompt(new TextPrompt<string>("Enter API key:").PromptStyle("yellow").Secret());
                settings.Update(x => x.DICOMAnonAPIKey = val ?? string.Empty); dirty = true;
            }
            else if (choice.StartsWith("Edit Port"))
            {
                var val = AnsiConsole.Prompt(new TextPrompt<int>("Enter port:").Validate(p => p is >= 1 and <= 65535 ? ValidationResult.Success() : ValidationResult.Error("Port must be 1-65535")));
                settings.Update(x => x.DICOMAnonPort = val); dirty = true;
            }
            else if (choice.StartsWith("Test Connection"))
            {
                var ok = da.TestConnection();
                AnsiConsole.MarkupLine(ok ? "[green]Connection OK[/]" : "[red]Connection failed[/]");
            }
            else if (choice.StartsWith("Save"))
            {
                if (dirty)
                {
                    settings.Save(settings.AppSettings);
                    dirty = false;
                    AnsiConsole.MarkupLine("[green]Settings saved[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]No changes to save[/]");
                }
            }
            else
            {
                if (dirty && AnsiConsole.Confirm("You have unsaved changes. Save now?"))
                {
                    settings.Save(settings.AppSettings);
                    AnsiConsole.MarkupLine("[green]Settings saved[/]");
                }
                break;
            }
        }
    }

    static void SelectionAndExportScreen(IAppSettingsService settings, DAClientSyncService da, string patientIdArg)
    {
        // Require a patient id first if not provided
        var patientId = patientIdArg;
        if (string.IsNullOrWhiteSpace(patientId))
        {
            patientId = AnsiConsole.Ask<string>("Enter patient ID:");
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            AnsiConsole.MarkupLine("[red]Patient ID is required[/]");
            return;
        }

        // Choose data source: mock or ESAPI-backed
        var useMock = AnsiConsole.Confirm("Use mock tree data (no ESAPI)?", true);
        DicomTree tree = null;
        try
        {
            ITreeBuilder builder = useMock ? (ITreeBuilder)new MockTreeBuilder() : new EsapiTreeBuilder();
            tree = builder.BuildTree(patientId);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to build tree:[/] {ex.Message}");
            return;
        }

        if ((tree.Courses?.Count ?? 0) == 0 && (tree.Series?.Count ?? 0) == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No courses or series found for this patient.[/]");
            return;
        }

        // Top-level selection
        var courseChoices = tree.Courses.Select(c => c.DisplayName).ToList();
        var seriesChoices = tree.Series.Select(s => s.DisplayName).ToList();
        var topSelections = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Which items do you want to export?")
                .InstructionsText("Press <space> to toggle, <enter> to accept")
                .AddChoiceGroup("Courses", courseChoices)
                .AddChoiceGroup("Series", seriesChoices));

        var selection = new SelectionResult { PatientId = patientId };

        // Drill into courses
        foreach (var course in tree.Courses.Where(c => topSelections.Contains(c.DisplayName)))
        {
            var planChoices = course.Children.Select(p => p.DisplayName).ToList();
            if (planChoices.Count == 0) continue;
            var selectedPlans = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title($"Select plans for {course.DisplayName}")
                .AddChoices(planChoices));

            foreach (var plan in course.Children.Where(p => selectedPlans.Contains(p.DisplayName)))
            {
                var childChoices = plan.Children.Select(ch => ch.DisplayName).ToList();
                if (childChoices.Count == 0) continue;
                var selectedChildren = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                    .Title($"Select items under {plan.DisplayName}")
                    .AddChoices(childChoices));

                foreach (var ch in plan.Children.Where(ch => selectedChildren.Contains(ch.DisplayName)))
                {
                    if (!string.IsNullOrEmpty(ch.SeriesUID))
                    {
                        selection.SeriesUids.Add(ch.SeriesUID);
                    }
                }
            }
        }

        // Drill into series
        foreach (var series in tree.Series.Where(s => topSelections.Contains(s.DisplayName)))
        {
            var instanceChoices = series.Children.Select(i => i.DisplayName).ToList();
            bool all = false;
            if (instanceChoices.Count > 500)
            {
                all = AnsiConsole.Confirm($"Series {series.DisplayName} has {instanceChoices.Count} instances. Export all?");
            }
            if (all || instanceChoices.Count == 0)
            {
                selection.SeriesUids.Add(series.SeriesUID);
                continue;
            }

            var selectedInstances = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title($"Select instances for {series.DisplayName}")
                .AddChoices(instanceChoices));

            foreach (var inst in series.Children.Where(i => selectedInstances.Contains(i.DisplayName)))
            {
                selection.Instances.Add(new SelectionInstance { SeriesUid = inst.SeriesUID, InstanceUid = inst.InstanceUID });
            }
        }

        if (selection.SeriesUids.Count == 0 && selection.Instances.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No items selected.[/]");
            return;
        }

        // Build worklist and export
        var worklist = new WorklistBuilder().Build(selection);

        var proceed = AnsiConsole.Confirm($"Submit {worklist.Instances.Count} items to DICOMAnon now?");
        if (!proceed) return;

        var jobId = da.Export(worklist, new DAAPI.Models.IdentityMapping(), false).GetAwaiter().GetResult();
        AnsiConsole.MarkupLine($"[green]Job submitted[/]: {jobId}");

        if (AnsiConsole.Confirm("Poll job status?"))
        {
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
                var prog = da.GetJobStatus(jobId).GetAwaiter().GetResult();
                var (state, percent, isDone) = ReadProgress(prog);
                AnsiConsole.MarkupLine($"Status: {state} {percent}%");
                if (isDone) break;
            }
        }
    }

    static (string state, int percent, bool isDone) ReadProgress(object prog)
    {
        if (prog == null) return ("unknown", 0, true);
        var t = prog.GetType();
        string state = TryGet<string>(prog, t, new[] { "State", "Status" }) ?? "";
        int percent = 0;
        var pInt = TryGet<int?>(prog, t, new[] { "PercentComplete", "Progress", "Percent" });
        if (pInt.HasValue) percent = pInt.Value;
        else
        {
            var pDbl = TryGet<double?>(prog, t, new[] { "PercentComplete", "Progress", "Percent" });
            if (pDbl.HasValue) percent = (int)pDbl.Value;
        }

        bool isComplete = TryGet<bool?>(prog, t, new[] { "IsComplete", "Complete", "Finished", "IsFinished" }) ?? false;
        bool isFailed = TryGet<bool?>(prog, t, new[] { "IsFailed", "Failed", "Error" }) ?? false;
        if (!isComplete && string.IsNullOrEmpty(state) == false)
        {
            var s = state.ToLowerInvariant();
            if (s.Contains("complete") || s.Contains("done") || s.Contains("finished")) isComplete = true;
            if (s.Contains("fail") || s.Contains("error")) isFailed = true;
        }
        return (string.IsNullOrEmpty(state) ? (isFailed ? "Failed" : (isComplete ? "Complete" : "Running")) : state, percent, isComplete || isFailed);
    }

    static T TryGet<T>(object obj, Type t, string[] names)
    {
        foreach (var n in names)
        {
            var pi = t.GetProperty(n);
            if (pi != null && pi.CanRead)
            {
                var val = pi.GetValue(obj);
                if (val is T cast) return cast;
                try
                {
                    if (val != null) return (T)Convert.ChangeType(val, typeof(T));
                }
                catch { }
            }
        }
        return default(T);
    }
}
