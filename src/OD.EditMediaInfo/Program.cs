using OD.EditMediaInfo.Commands;
using OD.EditMediaInfo.Services;
using Spectre.Console;

var rawArgs = args;

if (rawArgs.Length == 0 || ShouldRunInteractive(rawArgs))
{
    string? initialInput = rawArgs.Length == 1 ? rawArgs[0].Trim().Trim('"') : null;

    if (initialInput != null && !File.Exists(initialInput) && !initialInput.StartsWith('-'))
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] File not found: \"{Markup.Escape(initialInput)}\"");
        return 1;
    }

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    try
    {
        return await InteractiveEditor.RunAsync(initialInput, cts.Token);
    }
    catch (OperationCanceledException)
    {
        return 130;
    }
}

var cmd = rawArgs[0].ToLowerInvariant();
string[] rest = rawArgs.Skip(1).ToArray();

using var cts2 = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts2.Cancel(); };

int exit;
switch (cmd)
{
    case "info":
    case "probe":
    case "show":
        exit = await InfoCommand.RunAsync(rest, cts2.Token);
        break;
    case "set":
    case "edit":
    case "tag":
        exit = await SetCommand.RunAsync(rest, cts2.Token);
        break;
    case "clear":
    case "remove":
        exit = await ClearCommand.RunAsync(rest, cts2.Token);
        break;
    case "preset":
    case "presets":
    case "empreinte":
    case "empreintes":
        exit = await PresetCommand.RunAsync(rest, cts2.Token);
        break;
    case "--help": case "-h": case "-?": case "help":
        PrintRootHelp(); exit = 0; break;
    case "--version": case "-v": case "version":
        Console.WriteLine("odmedia 1.2.0 (.NET 10 win-x64, TagLibSharp 2.3.0)");
        exit = 0; break;
    default:
        AnsiConsole.MarkupLine($"[red]Error:[/] Unknown command: \"{Markup.Escape(cmd)}\"");
        PrintRootHelp();
        exit = 1;
        break;
}
return exit;

static bool ShouldRunInteractive(string[] a)
{
    if (a.Length == 0) return true;
    if (a.Length == 1)
    {
        var p = a[0].Trim().Trim('"');
        if (File.Exists(p)) return true;
        if (!p.StartsWith('-') && !IsSubcommand(p) && (p.Contains('.') || p.Contains('\\') || p.Contains('/')))
            return true;
    }
    return false;
}

static bool IsSubcommand(string s) =>
    s is "info" or "probe" or "show" or "set" or "edit" or "tag" or "clear" or "remove" or "preset" or "presets" or "empreinte" or "empreintes" or "help" or "--help" or "-h" or "--version" or "-?";

static void PrintRootHelp()
{
    AnsiConsole.WriteLine("odmedia — media info viewer & editor");
    AnsiConsole.WriteLine("");
    AnsiConsole.WriteLine("Usage:");
    AnsiConsole.WriteLine("  odmedia <file>                 Open interactive viewer/editor");
    AnsiConsole.WriteLine("  odmedia info <file> [--json]   Show technical & tags");
    AnsiConsole.WriteLine("  odmedia set <file> [options]   Edit tags");
    AnsiConsole.WriteLine("  odmedia clear <file> [--yes]   Remove all tags");
    AnsiConsole.WriteLine("  odmedia preset <cmd> [options] Manage presets in <exeDir>/presets/");
    AnsiConsole.WriteLine("");
    AnsiConsole.WriteLine("Examples:");
    AnsiConsole.WriteLine("  odmedia song.mp3");
    AnsiConsole.WriteLine("  odmedia info track.m4a --json");
    AnsiConsole.WriteLine("  odmedia set track.mp3 --title \"New Title\" --artist \"Artist\" --year 2024");
    AnsiConsole.WriteLine("  odmedia set track.mp3 --cover cover.jpg");
    AnsiConsole.WriteLine("  odmedia set track.mp3 --remove-cover");
    AnsiConsole.WriteLine("  odmedia clear track.mp3 --yes");
    AnsiConsole.WriteLine("  odmedia preset save track.mp3 --name \"MonLabel\"");
    AnsiConsole.WriteLine("  odmedia preset apply other.mp3 --name \"MonLabel\"");
    AnsiConsole.WriteLine("");
    AnsiConsole.WriteLine("See: odmedia info --help, odmedia set --help, odmedia clear --help, odmedia preset --help");
}
