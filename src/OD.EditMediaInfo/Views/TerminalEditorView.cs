using OD.EditMediaInfo.ViewModels;
using Spectre.Console;

namespace OD.EditMediaInfo.Views;

/// <summary>Renders one bounded page of the interactive editor.</summary>
public static class TerminalEditorView
{
    /// <summary>Renders a grid, keeping large values and additional rows inside the terminal viewport.</summary>
    public static void Render(IAnsiConsole console, EditorViewModel model, string section, int selection, int pageSize, string status)
    {
        var width = console.Profile.Width;
        console.MarkupLine($"[bold cyan]{Escape($"odmedia · {Path.GetFileName(model.Info.FilePath)}", width - 1)}[/]");
        console.MarkupLine($"[grey]{Escape($"{model.Info.Details.Kind} · {model.Info.Details.Container} · {model.Info.FileSize / 1048576.0:0.##} MiB", width - 1)}[/]");
        console.MarkupLine($"[bold]{Escape(section, Math.Max(8, width - 45))}[/]  [grey]Tab / Shift+Tab: section · F2: choose[/]");
        console.MarkupLine(Escape($"Search: {(model.Search.Length == 0 ? "(none)" : model.Search)} | Modified only: {(model.ModifiedOnly ? "yes" : "no")} | {model.PendingCount} pending", width - 1));
        var fields = model.VisibleFields(section);
        var editing = fields.Any(f => f.IsEditable || f.IsModified);
        var pageStart = selection / pageSize * pageSize;
        var table = new Table().Border(TableBorder.Simple).Expand();
        var fieldWidth = Math.Max(12, (width - 30) / 3);
        var propertyWidth = Math.Max(16, width / 3);
        var valueWidth = Math.Max(16, width - propertyWidth - 30);
        if (editing)
        {
            table.AddColumn(new TableColumn("Field").Width(fieldWidth).NoWrap());
            table.AddColumn(new TableColumn("Current value").Width(fieldWidth).NoWrap());
            table.AddColumn(new TableColumn("New value").Width(fieldWidth).NoWrap());
            table.AddColumn(new TableColumn("Action").Width(14).NoWrap());
        }
        else
        {
            table.AddColumn(new TableColumn("Property (Read-only)").Width(propertyWidth).NoWrap());
            table.AddColumn(new TableColumn("Value").Width(valueWidth).NoWrap());
            table.AddColumn(new TableColumn("Source").Width(18).NoWrap());
        }
        for (int index = pageStart; index < Math.Min(fields.Count, pageStart + pageSize); index++)
        {
            var field = fields[index];
            var style = index == selection ? "black on cyan" : field.Action == "Remove" ? "red" : field.IsModified ? "yellow" : "default";
            Markup Cell(string value, int max) => new Markup($"[{style}]{Escape(value, max)}[/]").Ellipsis();
            var label = (index == selection ? "> " : "  ") + field.Label;
            var current = string.IsNullOrEmpty(field.CurrentValue) ? "—" : field.CurrentValue;
            if (editing)
                table.AddRow(Cell(label, fieldWidth), Cell(current, fieldWidth),
                    Cell(field.Replacement ?? "", fieldWidth), Cell(field.IsEditable ? field.Action : "Read-only", 9));
            else
                table.AddRow(Cell(label, propertyWidth), Cell(current, valueWidth), Cell(field.Source, 18));
        }
        if (fields.Count == 0) table.AddRow("No matching fields", "", "");
        console.Write(table);
        console.MarkupLine($"[grey]{Escape($"Rows {(fields.Count == 0 ? 0 : pageStart + 1)}–{Math.Min(fields.Count, pageStart + pageSize)} / {fields.Count} | {model.CoverAction}", width - 1)}[/]");
        console.MarkupLine(Escape("↑↓ Move  Enter Edit/details  Del Remove  Ctrl+Z Revert  / Search  F3 Modified", width - 1));
        console.MarkupLine(Escape("Ctrl+S / F6 Save  F1 Help  F4 Actions  Ctrl+O Open  Esc Quit", width - 1));
        console.MarkupLine($"[yellow]{Escape(status, width - 1)}[/]");
    }

    /// <summary>Escapes markup and control sequences, and limits grid cells to a single line.</summary>
    public static string Escape(string value, int maximum)
    {
        var clean = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        maximum = Math.Max(1, maximum);
        if (clean.Length > maximum) clean = clean[..Math.Max(0, maximum - 1)] + "…";
        return Markup.Escape(clean);
    }
}
