using OD.EditMediaInfo.ViewModels;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace OD.EditMediaInfo.Views;

/// <summary>Renders one bounded page of the interactive editor.</summary>
public static class TerminalEditorView
{
    /// <summary>Reserves space for the header, table borders, shortcuts, and status.</summary>
    internal static int GetPageSize(int terminalHeight) => Math.Max(1, terminalHeight - 14);

    /// <summary>Renders a grid, keeping large values and additional rows inside the terminal viewport.</summary>
    public static void Render(IAnsiConsole console, EditorViewModel model, string section, int selection, int pageSize, string status)
    {
        var width = console.Profile.Width - 1;
        var pendingCount = model.PendingCount;
        console.MarkupLine($"[bold cyan]odmedia[/] [grey]│[/] [bold]{Escape(Path.GetFileName(model.Info.FilePath), width - 10)}[/]");
        console.MarkupLine($"[grey]{Escape($"{model.Info.Details.Kind} · {model.Info.Details.Container} · {model.Info.FileSize / 1048576.0:0.##} MiB", width)}[/]");
        console.MarkupLine($"[bold cyan]{Escape(section, width - 26)}[/]  [grey]│[/]  {PendingBadge(pendingCount)}");
        var filter = model.ModifiedOnly ? "[bold yellow]Modified only[/]" : "[grey]All fields[/]";
        console.MarkupLine($"{filter}  [grey]│  Search[/] {Escape(model.Search.Length == 0 ? "(none)" : model.Search, width - 25)}");
        var fields = model.VisibleFields(section);
        var editing = fields.Any(f => f.IsEditable || f.IsModified);
        var pageStart = selection / pageSize * pageSize;
        var table = new Table { Border = TableBorder.Rounded, BorderStyle = new Style(Color.Grey) }.Width(width);
        var fieldWidth = Math.Max(12, (width - 23) / 3);
        var propertyWidth = Math.Max(20, width / 3);
        var valueWidth = Math.Max(16, width - propertyWidth - 27);
        if (editing)
        {
            table.AddColumn(new TableColumn("[bold cyan]Field[/]").Width(fieldWidth).NoWrap());
            table.AddColumn(new TableColumn("[bold]Saved value[/]").Width(fieldWidth).NoWrap());
            table.AddColumn(new TableColumn("[bold yellow]Pending value[/]").Width(fieldWidth).NoWrap());
            table.AddColumn(new TableColumn("[bold cyan]Change[/]").Width(10).NoWrap());
        }
        else
        {
            table.AddColumn(new TableColumn("[bold cyan]Property (Read-only)[/]").Width(propertyWidth).NoWrap());
            table.AddColumn(new TableColumn("[bold]Value[/]").Width(valueWidth).NoWrap());
            table.AddColumn(new TableColumn("[bold grey]Source[/]").Width(17).NoWrap());
        }
        for (int index = pageStart; index < Math.Min(fields.Count, pageStart + pageSize); index++)
        {
            var field = fields[index];
            var label = index == selection ? $"> {field.Label}" : $"  {field.Label}";
            var labelStyle = index == selection ? "bold black on cyan" : "default";
            var current = string.IsNullOrEmpty(field.CurrentValue) ? "—" : field.CurrentValue;
            if (editing)
            {
                var changeStyle = ChangeStyle(field);
                var pending = field.Action == "Remove" ? "—" : field.Replacement ?? "";
                table.AddRow(Cell(label, fieldWidth, labelStyle), Cell(current, fieldWidth),
                    Cell(pending, fieldWidth, changeStyle), Cell(field.IsEditable ? field.Action : "Read-only", 10, changeStyle));
            }
            else
                table.AddRow(Cell(label, propertyWidth, labelStyle), Cell(current, valueWidth), Cell(field.Source, 17, "grey"));
        }
        if (fields.Count == 0) table.AddRow("[grey]No matching fields[/]", "", "");
        console.Write(table);
        var rows = $"Rows {(fields.Count == 0 ? 0 : pageStart + 1)}–{Math.Min(fields.Count, pageStart + pageSize)} / {fields.Count}";
        console.MarkupLine($"[grey]{rows}  │[/] [{(model.HasPendingCoverChange ? "bold yellow" : "grey")}]{Escape(model.CoverAction, width - rows.Length - 5)}[/]");
        console.MarkupLine($"[grey]{Escape(status, width)}[/]");
        var save = pendingCount > 0 ? "[bold black on green] Ctrl+S / F6  Save [/]" : "[bold green]Ctrl+S / F6[/] Save";
        console.MarkupLine($"{save}  [grey]│[/]  [bold cyan]F4[/] Actions  [bold cyan]Ctrl+O[/] Open file  [bold cyan]Esc[/] Quit");
        console.MarkupLine("[cyan]↑↓[/] Move  [cyan]Tab[/] Sections  [cyan]F2[/] Choose  [cyan]/[/] Search  [cyan]F3[/] Modified  [cyan]F1[/] Help");
        console.MarkupLine("[cyan]Enter[/] Edit / details  [cyan]Del[/] Stage removal  [cyan]Ctrl+Z[/] Undo");
    }

    /// <summary>Displays the saved and pending values before confirming a grouped save.</summary>
    internal static void RenderSaveReview(IAnsiConsole console, EditorViewModel model, IEnumerable<EditorField> fields)
    {
        var width = console.Profile.Width - 1;
        console.MarkupLine($"[bold cyan]Review pending changes[/]  [grey]│[/]  {PendingBadge(model.PendingCount)}");
        var backup = model.CreateBackup ? "Create .bak if absent" : "Off";
        var keepTime = model.KeepMtime ? "Yes" : "No";
        console.MarkupLine($"[grey]Backup[/] {backup}  [grey]│  Keep modification time[/] {keepTime}");
        console.Write(new Rule { Style = new Style(Color.Grey) });
        foreach (var field in fields)
        {
            var changeStyle = ChangeStyle(field);
            var after = field.Action == "Remove" ? "(remove field)" : field.Replacement ?? "";
            console.MarkupLine($"[{changeStyle}]{field.Action}[/]  [bold]{Escape(field.Label, width - field.Action.Length - 2)}[/]");
            console.MarkupLine($"  [grey]Before[/]  {Escape(field.CurrentValue, width - 10)}");
            console.MarkupLine($"  [grey]After [/]  [{changeStyle}]{Escape(after, width - 10)}[/]");
        }
        var coverStyle = model.HasPendingCoverChange ? "bold yellow" : "grey";
        console.MarkupLine($"[{coverStyle}]{Escape(model.CoverAction, width)}[/]");
        console.Write(new Rule { Style = new Style(Color.Grey) });
        console.MarkupLine("[bold black on green] Enter  Save all [/]  [grey]│[/]  [cyan]↑↓[/] Scroll  [cyan]Esc[/] Back");
    }

    private static string PendingBadge(int count) => count == 0 ? "[grey]No unsaved changes[/]" :
        $"[bold black on yellow] ! {count} unsaved {(count == 1 ? "change" : "changes")} [/]";

    private static string ChangeStyle(EditorField field) => field.Action switch
    {
        "Replace" => "bold yellow",
        "Remove" => "bold red",
        _ => "grey"
    };

    private static Markup Cell(string value, int width, string style = "default") =>
        new Markup($"[{style}]{Escape(value, width)}[/]").Ellipsis();

    /// <summary>Escapes markup and control sequences, and limits grid cells to a single line.</summary>
    public static string Escape(string value, int maximum)
    {
        var clean = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        maximum = Math.Max(1, maximum);
        var segment = new Segment(clean, Style.Plain);
        if (segment.CellCount() <= maximum) return Markup.Escape(clean);
        for (var budget = maximum - 1; budget > 0; budget--)
        {
            var truncated = Segment.Truncate(segment, budget);
            if (truncated is null) break;
            if (truncated.CellCount() < maximum) return Markup.Escape(truncated.Text) + "…";
        }
        return "…";
    }
}
