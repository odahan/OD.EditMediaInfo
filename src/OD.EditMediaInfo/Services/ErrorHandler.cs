using Spectre.Console;

namespace OD.EditMediaInfo.Services;

public static class ErrorHandler
{
    public static int Fail(string message, int exitCode = 1)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        return exitCode;
    }

    public static void FailInteractive(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        try { Console.ReadKey(true); } catch { Console.ReadLine(); }
    }
}
