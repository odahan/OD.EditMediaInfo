using System.Text;
using System.Text.Json;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.ViewModels;
using OD.EditMediaInfo.Views;
using Spectre.Console;

namespace OD.EditMediaInfo.Services;

/// <summary>Provides keyboard navigation and grouped save operations for the terminal workspace.</summary>
public static partial class InteractiveEditor
{
    /// <summary>Runs the full-screen workspace, preserving pending edits when navigation or saving fails.</summary>
    public static async Task<int> RunAsync(string? initialInput, CancellationToken cancellationToken)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            return ErrorHandler.Fail("Interactive mode requires a terminal. Use odmedia info <file> [--json] for redirected output.");
        var previousControlC = Console.TreatControlCAsInput;
        var previousCursor = !OperatingSystem.IsWindows() || Console.CursorVisible;
        Console.TreatControlCAsInput = true;
        string? path = initialInput?.Trim().Trim('"');
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = ReadInput("Open media", "Media file path (drag and drop supported). Empty to quit.", "")?.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(path)) return 0;
                }
                EditorViewModel model;
                try
                {
                    Console.Clear();
                    AnsiConsole.WriteLine("Inspecting media...");
                    model = new EditorViewModel(new MediaTagService().Read(path));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ShowMessage("Unable to open media", ex.Message);
                    if (FFprobeService.FindExecutable() is null)
                    {
                        var recovery = Choose("Media analysis", ["Choose another file", "Install media analyzer and retry", "Quit"]);
                        if (recovery == "Quit") return 0;
                        if (recovery == "Install media analyzer and retry")
                        {
                            try
                            {
                                if (await InstallAnalyzerInteractiveAsync(cancellationToken)) continue;
                            }
                            catch (Exception installError) when (installError is not OperationCanceledException)
                            {
                                ShowMessage("Installation failed", installError.Message);
                            }
                        }
                    }
                    path = null;
                    continue;
                }
                using (model)
                {
                    var result = await RunWorkspaceAsync(model, cancellationToken);
                    if (result == "quit") return 0;
                    path = result == "open" ? null : model.Info.FilePath;
                }
            }
        }
        catch (OperationCanceledException) { return 130; }
        finally
        {
            Console.TreatControlCAsInput = previousControlC;
            Console.CursorVisible = previousCursor;
            Console.Clear();
        }
    }

    private static async Task<string> RunWorkspaceAsync(EditorViewModel model, CancellationToken token)
    {
        int sectionIndex = 0;
        int selection = 0;
        string status = "Technical fields are read-only. Enter shows complete values and their source.";
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var section = model.Sections[sectionIndex];
            var fields = model.VisibleFields(section);
            selection = Math.Clamp(selection, 0, Math.Max(0, fields.Count - 1));
            int pageSize = Math.Max(1, Console.WindowHeight - 13);
            Console.Clear();
            Console.CursorVisible = false;
            if (Console.WindowWidth < 76 || Console.WindowHeight < 18)
                AnsiConsole.WriteLine("Enlarge the terminal to at least 76 × 18. Esc exits; Ctrl+O opens another file.");
            else TerminalEditorView.Render(AnsiConsole.Console, model, section, selection, pageSize, status);
            var key = Console.ReadKey(intercept: true);
            var control = key.Modifiers.HasFlag(ConsoleModifiers.Control);
            try
            {
                if (key.Key == ConsoleKey.F1)
                {
                    OpenManual();
                }
                else if (key.Key == ConsoleKey.Escape || control && key.Key == ConsoleKey.C)
                {
                    if (CanLeave(model)) return "quit";
                }
                else if (control && key.Key == ConsoleKey.O)
                {
                    if (CanLeave(model)) return "open";
                }
                else if (control && key.Key == ConsoleKey.S || key.Key == ConsoleKey.F6)
                {
                    if (ReviewAndSave(model)) return "reload";
                }
                else if (key.Key is ConsoleKey.Tab or ConsoleKey.LeftArrow or ConsoleKey.RightArrow)
                {
                    var backwards = key.Key == ConsoleKey.LeftArrow || key.Modifiers.HasFlag(ConsoleModifiers.Shift);
                    sectionIndex = (sectionIndex + (backwards ? -1 : 1) + model.Sections.Length) % model.Sections.Length;
                    selection = 0;
                }
                else if (key.Key == ConsoleKey.F2)
                {
                    var selected = Choose("Section", model.Sections);
                    sectionIndex = Array.IndexOf(model.Sections, selected);
                    selection = 0;
                }
                else if (key.Key == ConsoleKey.DownArrow) selection = Math.Min(selection + 1, fields.Count - 1);
                else if (key.Key == ConsoleKey.UpArrow) selection = Math.Max(0, selection - 1);
                else if (key.Key == ConsoleKey.PageDown) selection += pageSize;
                else if (key.Key == ConsoleKey.PageUp) selection -= pageSize;
                else if (key.Key == ConsoleKey.Home) selection = 0;
                else if (key.Key == ConsoleKey.End) selection = Math.Max(0, fields.Count - 1);
                else if (key.KeyChar == '/' || control && key.Key == ConsoleKey.F)
                {
                    var search = ReadInput("Search all properties", "Search names, values, and sources. Empty clears the filter.", model.Search);
                    if (search is not null) model.Search = search;
                    selection = 0;
                }
                else if (key.Key == ConsoleKey.F3)
                {
                    model.ModifiedOnly = !model.ModifiedOnly;
                    selection = 0;
                }
                else if (key.Key == ConsoleKey.F4)
                {
                    if (await RunActionsAsync(model, token)) return "reload";
                }
                else if (fields.Count > 0)
                {
                    var field = fields[selection];
                    if (control && key.Key == ConsoleKey.Z) field.Revert();
                    else if (key.Key == ConsoleKey.Delete)
                    {
                        field.Remove();
                        status = "Removal staged. Ctrl+Z restores the selected field.";
                    }
                    else if (key.Key == ConsoleKey.Enter)
                    {
                        if (!field.IsEditable)
                        {
                            var reason = field.Definition is not null
                                ? "Capture and GPS editing is locked. Use Actions > Enable capture and GPS editing to allow corrections."
                                : "Read-only: no supported writer is available for this property.";
                            ShowMessage(field.Label, $"{field.CurrentValue}\n\nSource: {field.Source}\nSection: {field.Section}\n{reason}");
                        }
                        else
                        {
                            var value = ReadInput(field.Label, $"Current: {field.CurrentValue}\nEmpty input keeps the pending state; use Del in the grid to remove. Lists use semicolons.", field.Replacement ?? field.CurrentValue);
                            if (value is not null) field.Replace(value);
                            status = "Changes remain in memory until Ctrl+S. Ctrl+Z reverts the selected field.";
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                status = "The operation failed. Pending changes were retained.";
                ShowMessage("Operation failed", ex.Message);
            }
        }
    }

    private static bool CanLeave(EditorViewModel model)
    {
        if (model.PendingCount == 0) return true;
        return Choose($"{model.PendingCount} unsaved changes", ["Save", "Discard", "Cancel"]) switch
        {
            "Save" => ReviewAndSave(model),
            "Discard" => true,
            _ => false
        };
    }

    private static bool ReviewAndSave(EditorViewModel model)
    {
        if (model.PendingCount == 0) { ShowMessage("Save", "No pending changes."); return false; }
        int offset = 0;
        var modified = model.Fields.Where(f => f.IsModified).ToArray();
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine("[bold cyan]Review pending changes[/]");
            AnsiConsole.WriteLine($"Backup: {(model.CreateBackup ? "create .bak if absent" : "disabled")} | Keep modification time: {model.KeepMtime}");
            int size = Math.Max(1, (Console.WindowHeight - 8) / 3);
            foreach (var field in modified.Skip(offset).Take(size))
            {
                AnsiConsole.WriteLine($"{field.Label}: {field.Action}");
                AnsiConsole.WriteLine("  Before: " + Plain(field.CurrentValue, Console.WindowWidth - 11));
                AnsiConsole.WriteLine("  After:  " + Plain(field.Action == "Remove" ? "(remove field)" : field.Replacement ?? "", Console.WindowWidth - 11));
            }
            AnsiConsole.WriteLine(Plain(model.CoverAction, Console.WindowWidth - 1));
            AnsiConsole.WriteLine($"{model.PendingCount} changes · ↑↓ Scroll · Enter Save all · Esc Back");
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape) return false;
            if (key.Key == ConsoleKey.DownArrow) offset = Math.Min(Math.Max(0, modified.Length - size), offset + size);
            if (key.Key == ConsoleKey.UpArrow) offset = Math.Max(0, offset - size);
            if (key.Key != ConsoleKey.Enter) continue;
            new MediaTagService().Save(model.ToEditOptions());
            model.RevertAll();
            return true;
        }
    }

    private static async Task<bool> RunActionsAsync(EditorViewModel model, CancellationToken token)
    {
        var actions = new List<string> { "Back", "User manual", "Install media analyzer", "Add Explorer context menu", "Remove Explorer context menu", "Export complete JSON" };
        if (model.Info.Details.Kind == "Video") actions.Add("Analyze cadence (up to 300 frames)");
        if (model.Info.Details.CanWriteTags)
        {
            if (model.Info.Details.CanWritePhoto && model.Fields.Any(f => f.Definition is not null && !f.IsEditable))
                actions.Add("Enable capture and GPS editing");
            actions.AddRange(["Toggle backup", "Toggle keep modification time", "Discard pending changes", "Remove all editable fields"]);
            if (model.Info.Details.Kind != "Photo") actions.AddRange(["Artwork: add / replace", "Artwork: remove all", "Artwork: revert", "Preset: stage for this file", "Preset: create from this file", "Preset: create from scratch"]);
        }
        if (model.Info.Pictures.Count > 0) actions.Add("Artwork: extract");
        actions.Add("Preset: manage");
        var action = Choose("Actions", actions);
        switch (action)
        {
            case "User manual": OpenManual(); break;
            case "Enable capture and GPS editing":
                foreach (var field in model.Fields) field.EnableCaptureEditing();
                break;
            case "Analyze cadence (up to 300 frames)":
                Console.Clear();
                AnsiConsole.WriteLine("Inspecting video frame timestamps...");
                FFprobeService.AnalyzeCadence(model.Info.FilePath, model.Info.Details);
                model.RefreshCadenceResults();
                ShowMessage("Cadence analysis", model.Info.Details.Properties.Last(p => p.Name.EndsWith("Cadence sample", StringComparison.Ordinal)).Value ?? "Unknown");
                break;
            case "Install media analyzer":
                if (!await InstallAnalyzerInteractiveAsync(token)) break;
                ShowMessage("Analyzer installed", "FFmpeg and FFprobe are available beside odmedia.exe.");
                if (model.PendingCount == 0) return true;
                ShowMessage("Analysis", "Save or discard the pending edits, then reopen the file to refresh its technical information.");
                break;
            case "Add Explorer context menu":
                MediaToolsService.InstallExplorerMenu();
                ShowMessage("Explorer integration", "Added for this user. On Windows 11, use Show more options. Register again if you move odmedia.exe.");
                break;
            case "Remove Explorer context menu":
                MediaToolsService.RemoveExplorerMenu();
                ShowMessage("Explorer integration", "The odmedia context menu entry was removed.");
                break;
            case "Toggle backup": model.CreateBackup = !model.CreateBackup; break;
            case "Toggle keep modification time": model.KeepMtime = !model.KeepMtime; break;
            case "Discard pending changes":
                if (Choose("Discard all pending changes?", ["Cancel", "Discard"]) == "Discard") model.RevertAll();
                break;
            case "Remove all editable fields":
                foreach (var field in model.Fields.Where(f => f.IsEditable)) field.Remove();
                break;
            case "Artwork: add / replace":
                var cover = ReadInput("Artwork", "Image path (JPG, PNG, WebP, BMP; maximum 10 MB). Esc cancels.", "")?.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(cover)) model.StageCover(cover);
                break;
            case "Artwork: remove all": model.StageCover(null, remove: true); break;
            case "Artwork: revert": model.StageCover(null); break;
            case "Preset: stage for this file":
                var names = PresetService.List();
                if (names.Count == 0) ShowMessage("Presets", "No presets found.");
                else model.ApplyPreset(PresetService.Load(Choose("Choose preset", names)));
                break;
            case "Preset: create from this file": await HandlePresetCreateFromFileAsync(model.Info.FilePath, model.Info); break;
            case "Preset: create from scratch": await HandlePresetCreateFromScratchAsync(); break;
            case "Preset: manage": await HandlePresetManageAsync(); break;
            case "Artwork: extract": ExtractArtwork(model.Info); break;
            case "Export complete JSON":
                var path = ReadInput("Export inspection", "Exports saved values only; pending changes are not included.", model.Info.FilePath + ".json");
                if (!string.IsNullOrWhiteSpace(path) && AllowOverwrite(path))
                    File.WriteAllText(path, JsonSerializer.Serialize(model.Info, new JsonSerializerOptions { WriteIndented = true }));
                break;
        }
        return false;
    }

    private static async Task<bool> InstallAnalyzerInteractiveAsync(CancellationToken token)
    {
        Console.Clear();
        AnsiConsole.WriteLine("Installing the optional media analyzer. Esc cancels.");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var installation = MediaToolsService.InstallAnalyzerAsync(new InlineProgress(message => AnsiConsole.WriteLine(message)), cancellation.Token);
        while (!installation.IsCompleted)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) cancellation.Cancel();
            await Task.WhenAny(installation, Task.Delay(100, CancellationToken.None));
        }
        try { await installation; return true; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            ShowMessage("Installation cancelled", "The download was cancelled. No new analyzer installation was committed.");
            return false;
        }
    }

    private static void ExtractArtwork(MediaInfo info)
    {
        var choices = info.Pictures.Select((p, i) => $"{i + 1}: {p.Type} ({p.MimeType})").ToArray();
        var selected = Array.IndexOf(choices, Choose("Artwork", choices));
        var extension = info.Pictures[selected].MimeType switch { "image/png" => ".png", "image/webp" => ".webp", "image/bmp" => ".bmp", _ => ".jpg" };
        var output = ReadInput("Extract artwork", "Output image path", Path.ChangeExtension(info.FilePath, $"cover-{selected + 1}{extension}"));
        if (string.IsNullOrWhiteSpace(output) || !AllowOverwrite(output)) return;
        using var file = TagLib.File.Create(info.FilePath);
        File.WriteAllBytes(output, file.Tag.Pictures[selected].Data.Data);
    }

    private static bool AllowOverwrite(string path) => !File.Exists(path) || Choose("File exists. Replace it?", ["Cancel", "Replace"]) == "Replace";

    private static void OpenManual()
    {
        var language = Choose("User manual", ["English", "Français", "Back"]);
        if (language == "Back") return;
        var path = ManualService.Extract(language == "Français" ? "fr" : "en");
        try { ManualService.Open(path); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowMessage("Manual extracted", $"Open this file in your browser:\n{path}\n\n{ex.Message}");
        }
    }

    private static string Choose(string title, IEnumerable<string> values)
    {
        Console.Clear();
        Console.CursorVisible = true;
        return AnsiConsole.Prompt(new SelectionPrompt<string>().Title(Markup.Escape(title)).PageSize(Math.Max(3, Math.Min(12, Console.WindowHeight - 6))).AddChoices(values));
    }

    private static string Plain(string value, int max) => Markup.Remove(TerminalEditorView.Escape(value, max));

    private static void ShowMessage(string title, string text)
    {
        var lines = text.Replace("\r", "").Split('\n').SelectMany(line =>
        {
            var value = new string(line.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
            int width = Math.Max(10, Console.WindowWidth - 2);
            return value.Length == 0 ? new[] { "" } : Enumerable.Range(0, (value.Length + width - 1) / width).Select(i => value.Substring(i * width, Math.Min(width, value.Length - i * width)));
        }).ToArray();
        int offset = 0;
        while (true)
        {
            Console.Clear();
            AnsiConsole.WriteLine(Plain(title, Console.WindowWidth - 1));
            int size = Math.Max(1, Console.WindowHeight - 4);
            foreach (var line in lines.Skip(offset).Take(size)) AnsiConsole.WriteLine(line);
            AnsiConsole.WriteLine("↑↓ Scroll · Enter / Esc Back");
            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape) return;
            if (key.Key == ConsoleKey.DownArrow) offset = Math.Min(Math.Max(0, lines.Length - size), offset + size);
            if (key.Key == ConsoleKey.UpArrow) offset = Math.Max(0, offset - size);
        }
    }

    private static string? ReadInput(string title, string hint, string initial)
    {
        var buffer = new StringBuilder(initial);
        int cursor = buffer.Length;
        while (true)
        {
            Console.Clear();
            Console.CursorVisible = true;
            AnsiConsole.WriteLine(Plain(title, Console.WindowWidth - 1));
            foreach (var line in hint.Split('\n')) AnsiConsole.WriteLine(Plain(line, Console.WindowWidth - 1));
            AnsiConsole.WriteLine("Enter Accept · Esc Cancel · Ctrl+Enter New line · Ctrl+A Clear · F1 Help");
            var visible = buffer.ToString().Replace("\n", "↵");
            int width = Math.Max(10, Console.WindowWidth - 4);
            int start = Math.Max(0, cursor - width + 1);
            Console.Write("> " + Plain(visible.Substring(start, Math.Min(width, visible.Length - start)), width));
            Console.CursorLeft = 2 + cursor - start;
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.F1)
            {
                try { OpenManual(); }
                catch (Exception ex) when (ex is not OperationCanceledException) { ShowMessage("Manual unavailable", ex.Message); }
                continue;
            }
            if (key.Key == ConsoleKey.Escape) return null;
            if (key.Key == ConsoleKey.Enter)
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control)) { buffer.Insert(cursor++, '\n'); continue; }
                return buffer.ToString();
            }
            if (key.Key == ConsoleKey.A && key.Modifiers.HasFlag(ConsoleModifiers.Control)) { buffer.Clear(); cursor = 0; }
            else if (key.Key == ConsoleKey.Backspace && cursor > 0) buffer.Remove(--cursor, 1);
            else if (key.Key == ConsoleKey.Delete && cursor < buffer.Length) buffer.Remove(cursor, 1);
            else if (key.Key == ConsoleKey.LeftArrow) cursor = Math.Max(0, cursor - 1);
            else if (key.Key == ConsoleKey.RightArrow) cursor = Math.Min(buffer.Length, cursor + 1);
            else if (key.Key == ConsoleKey.Home) cursor = 0;
            else if (key.Key == ConsoleKey.End) cursor = buffer.Length;
            else if (!char.IsControl(key.KeyChar) && buffer.Length < 65536) buffer.Insert(cursor++, key.KeyChar);
        }
    }

    private sealed class InlineProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
