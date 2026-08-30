using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

/// <summary>Implements the command that removes all media tags.</summary>
public static class ClearCommand
{
    /// <summary>Executes the clear command with parsed arguments.</summary>
    public static Task<int> RunAsync(string file, bool yes, bool noBackup, bool keepMtime)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia clear <file> [--yes]");
            return Task.FromResult(1);
        }

        file = file.Trim();
        if (!File.Exists(file))
        {
            ErrorHandler.Fail($"File not found: \"{file}\"");
            return Task.FromResult(1);
        }

        if (!yes)
        {
            if (!AnsiConsole.Confirm($"Remove [red]all tags[/] from \"{Markup.Escape(file)}\"?", false))
                return Task.FromResult(0);
        }

        try
        {
            var service = new MediaTagService();
            service.Clear(file, noBackup, keepMtime);
            AnsiConsole.MarkupLine($"[green]Cleared:[/] {Markup.Escape(file)}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    /// <summary>Writes the clear command help text.</summary>
    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia clear <file> [--yes] [--no-backup] [--keep-mtime]");
        AnsiConsole.WriteLine("  Remove all tags from file.");
        AnsiConsole.WriteLine("  --yes        Skip confirmation");
        AnsiConsole.WriteLine("  --no-backup  Do not create backup (.bak)");
        AnsiConsole.WriteLine("  --keep-mtime Preserve file modification time");
    }
}