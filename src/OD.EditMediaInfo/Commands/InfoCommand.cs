using System.Text;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

/// <summary>Implements the command that displays or exports media information.</summary>
public static class InfoCommand
{
    /// <summary>Executes the info command with parsed arguments.</summary>
    public static Task<int> RunAsync(string file, bool json, bool analyzeCadence = false,
        MediaExportFormat? export = null, string? output = null)
    {
        if (string.IsNullOrWhiteSpace(file))
            return Fail("Missing file. Usage: odmedia info <file> [--json | --export <json|csv|html>]");
        if (json && export.HasValue)
            return Fail("Use either --json or --export.");
        if (export.HasValue && !Enum.IsDefined(export.Value))
            return Fail("Choose JSON, CSV, or HTML for --export.");
        if (output is not null && (!export.HasValue || string.IsNullOrWhiteSpace(output)))
            return Fail("--output requires --export and a report file path.");

        file = file.Trim();
        if (!File.Exists(file))
            return Fail($"File not found: \"{file}\"");

        try
        {
            var info = new MediaTagService().Read(file);
            if (analyzeCadence)
                FFprobeService.AnalyzeCadence(file, info.Details);

            if (export is { } format)
            {
                if (output is null)
                {
                    Console.OutputEncoding = new UTF8Encoding(false);
                    MediaExportService.Write(Console.Out, info, format);
                }
                else
                    MediaExportService.Save(output, info, format);
            }
            else if (json)
                MediaExportService.WriteLegacyInfoJson(Console.Out, info);
            else
                MediaProbeService.Render(info);

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message, 2);
        }
    }

    private static Task<int> Fail(string message, int exitCode = 1)
    {
        Console.Error.WriteLine($"Error: {message}");
        return Task.FromResult(exitCode);
    }

    /// <summary>Writes the info command help text.</summary>
    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia info <file> [--json | --export <json|csv|html> [--output <path>]]");
        AnsiConsole.WriteLine("  Show or export media technical information and tags.");
    }
}
