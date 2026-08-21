using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

public static class ClearCommand
{
    public static Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        string? input = null;
        bool yes = false;
        bool noBackup = false;
        bool keepMtime = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--yes": case "-y": yes = true; break;
                case "--no-backup": noBackup = true; break;
                case "--keep-mtime": keepMtime = true; break;
                case "--help": case "-h": PrintHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    if (input is null) input = a;
                    else { ErrorHandler.Fail("Too many arguments"); return Task.FromResult(1); }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia clear <file> [--yes]");
            return Task.FromResult(1);
        }

        input = input.Trim().Trim('"');
        if (!File.Exists(input))
        {
            ErrorHandler.Fail($"File not found: \"{input}\"");
            return Task.FromResult(1);
        }

        if (!yes)
        {
            if (!AnsiConsole.Confirm($"Remove [red]all tags[/] from \"{Markup.Escape(input)}\"?", false))
                return Task.FromResult(0);
        }

        try
        {
            var service = new MediaTagService();
            service.Clear(input, noBackup, keepMtime);
            AnsiConsole.MarkupLine($"[green]Cleared:[/] {Markup.Escape(input)}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia clear <file> [--yes] [--no-backup] [--keep-mtime]");
        AnsiConsole.WriteLine("  Remove all tags from file.");
        AnsiConsole.WriteLine("  --yes        Skip confirmation");
    }
}
