using Spectre.Console;

namespace OD.EditMediaInfo.Services;

/// <summary>Formats errors consistently for command-line and interactive usage.</summary>
public static class ErrorHandler
{
    /// <summary>Writes an error message and returns the requested exit code.</summary>
    public static int Fail(string message, int exitCode = 1)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        return exitCode;
    }

    /// <summary>Writes an error message and waits for interactive acknowledgement.</summary>
    public static void FailInteractive(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        try { Console.ReadKey(true); } catch { Console.ReadLine(); }
    }
}
