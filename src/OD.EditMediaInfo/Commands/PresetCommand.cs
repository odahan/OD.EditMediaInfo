using System.Text.Json;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

/// <summary>Implements commands used to create and apply tag presets.</summary>
public static class PresetCommand
{
    /// <summary>Parses and executes a preset subcommand.</summary>
    public static Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return Task.FromResult(0);
        }

        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        return sub switch
        {
            "save" => SaveAsync(rest),
            "create" => SaveAsync(rest),
            "list" => ListAsync(rest),
            "ls" => ListAsync(rest),
            "show" => ShowAsync(rest),
            "apply" => ApplyAsync(rest),
            "delete" => DeleteAsync(rest),
            "remove" => DeleteAsync(rest),
            "rm" => DeleteAsync(rest),
            "--help" or "-h" or "help" => Task.FromResult(PrintHelpAndReturn()),
            _ => Task.FromResult(UnknownSub(sub))
        };
    }

    private static int UnknownSub(string sub)
    {
        ErrorHandler.Fail($"Unknown preset subcommand: \"{sub}\". See odmedia preset --help");
        return 1;
    }

    private static int PrintHelpAndReturn() { PrintHelp(); return 0; }

    // odmedia preset save <sourceFile> --name <name> [--artist ... --cover ...]
    // odmedia preset save --name <name> --artist ... (from scratch)
    private static Task<int> SaveAsync(string[] args)
    {
        string? sourceFile = null;
        string? name = null;
        string[]? artists = null;
        string? album = null;
        string[]? albumArtists = null;
        string[]? genres = null;
        uint? year = null;
        string? comment = null;
        string[]? composers = null;
        string? copyright = null;
        string? publisher = null;
        string? coverPath = null;
        string? coverDesc = null;
        bool force = false;

        var positional = new List<string>();
        bool hasExplicitTagOption = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--name": name = Next(args, ref i, "--name"); break;
                case "--artist": artists = Split(Next(args, ref i, "--artist")); hasExplicitTagOption = true; break;
                case "--album": album = Next(args, ref i, "--album"); hasExplicitTagOption = true; break;
                case "--album-artist": albumArtists = Split(Next(args, ref i, "--album-artist")); hasExplicitTagOption = true; break;
                case "--genre": genres = Split(Next(args, ref i, "--genre")); hasExplicitTagOption = true; break;
                case "--year": year = uint.Parse(Next(args, ref i, "--year")); hasExplicitTagOption = true; break;
                case "--comment": comment = Next(args, ref i, "--comment"); hasExplicitTagOption = true; break;
                case "--composer": composers = Split(Next(args, ref i, "--composer")); hasExplicitTagOption = true; break;
                case "--copyright": copyright = Next(args, ref i, "--copyright"); hasExplicitTagOption = true; break;
                case "--publisher": publisher = Next(args, ref i, "--publisher"); hasExplicitTagOption = true; break;
                case "--cover": coverPath = Next(args, ref i, "--cover"); hasExplicitTagOption = true; break;
                case "--cover-desc": coverDesc = Next(args, ref i, "--cover-desc"); break;
                case "--force": force = true; break;
                case "--help": case "-h": PrintSaveHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    positional.Add(a);
                    break;
            }
        }

        if (positional.Count > 1)
        {
            ErrorHandler.Fail("Too many positional arguments — expected at most one source file.");
            return Task.FromResult(1);
        }
        if (positional.Count == 1) sourceFile = positional[0].Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorHandler.Fail("Missing --name <presetName>. Usage: odmedia preset save [--name <name>] [sourceFile] [options]");
            return Task.FromResult(1);
        }

        // Validate source if provided
        MediaInfo? sourceInfo = null;
        if (sourceFile is not null)
        {
            if (!File.Exists(sourceFile))
            {
                ErrorHandler.Fail($"Source file not found: \"{sourceFile}\"");
                return Task.FromResult(1);
            }
            try
            {
                sourceInfo = new MediaTagService().Read(sourceFile);
            }
            catch (Exception ex)
            {
                ErrorHandler.Fail(ex.Message, 2);
                return Task.FromResult(2);
            }
        }

        // Build preset
        try
        {
            var tags = new PresetTags();

            // If explicit tags given via CLI, use them (from scratch or override source)
            // If source provided and no explicit tag options, we default to include all generic fields from source (user wanted checkbox in interactive, but CLI non-interactive should still be useful)
            // For CLI non-interactive: if source + no explicit options -> copy all generic fields from source
            if (sourceInfo is not null && !hasExplicitTagOption)
            {
                tags.Artists = sourceInfo.Artists.Length > 0 ? sourceInfo.Artists : null;
                tags.Album = string.IsNullOrWhiteSpace(sourceInfo.Album) ? null : sourceInfo.Album;
                tags.AlbumArtists = sourceInfo.AlbumArtists.Length > 0 ? sourceInfo.AlbumArtists : null;
                tags.Genres = sourceInfo.Genres.Length > 0 ? sourceInfo.Genres : null;
                tags.Year = sourceInfo.Year == 0 ? null : sourceInfo.Year;
                tags.Comment = string.IsNullOrWhiteSpace(sourceInfo.Comment) ? null : sourceInfo.Comment;
                tags.Composers = sourceInfo.Composers.Length > 0 ? sourceInfo.Composers : null;
                tags.Copyright = string.IsNullOrWhiteSpace(sourceInfo.Copyright) ? null : sourceInfo.Copyright;
                tags.Publisher = string.IsNullOrWhiteSpace(sourceInfo.Publisher) ? null : sourceInfo.Publisher;
            }
            else
            {
                // CLI explicit options (or from scratch)
                // If source exists and explicit options partially given, we merge: explicit wins, otherwise take from source for missing fields? For now explicit only.
                // To also support "save source --name X --genre Y" meaning override only genre, we fill missing from source if source exists.
                if (sourceInfo is not null)
                {
                    tags.Artists = artists ?? (sourceInfo.Artists.Length > 0 ? sourceInfo.Artists : null);
                    // For album etc, if not explicitly passed, take from source? Only if user didn't pass --album at all, we keep source value if hasExplicitTagOption but album null -> still take source
                    // Simpler: if hasExplicitTagOption, take from source for fields not explicitly set? We'll do that.
                    tags.Album = album ?? (string.IsNullOrWhiteSpace(sourceInfo.Album) ? null : sourceInfo.Album);
                    tags.AlbumArtists = albumArtists ?? (sourceInfo.AlbumArtists.Length > 0 ? sourceInfo.AlbumArtists : null);
                    tags.Genres = genres ?? (sourceInfo.Genres.Length > 0 ? sourceInfo.Genres : null);
                    tags.Year = year ?? (sourceInfo.Year == 0 ? null : sourceInfo.Year);
                    tags.Comment = comment ?? (string.IsNullOrWhiteSpace(sourceInfo.Comment) ? null : sourceInfo.Comment);
                    tags.Composers = composers ?? (sourceInfo.Composers.Length > 0 ? sourceInfo.Composers : null);
                    tags.Copyright = copyright ?? (string.IsNullOrWhiteSpace(sourceInfo.Copyright) ? null : sourceInfo.Copyright);
                    tags.Publisher = publisher ?? (string.IsNullOrWhiteSpace(sourceInfo.Publisher) ? null : sourceInfo.Publisher);
                    // If exactly one explicit option was given, still fill rest from source (useful). If truly want only that field, user can create from scratch without source.
                }
                else
                {
                    tags.Artists = artists;
                    tags.Album = album;
                    tags.AlbumArtists = albumArtists;
                    tags.Genres = genres;
                    tags.Year = year;
                    tags.Comment = comment;
                    tags.Composers = composers;
                    tags.Copyright = copyright;
                    tags.Publisher = publisher;
                }
            }

            // Cover
            PresetCover? cover = null;
            if (coverPath is not null)
            {
                cover = PresetService.CoverFromFile(coverPath, coverDesc);
            }
            else if (sourceInfo is not null && hasExplicitTagOption == false)
            {
                // auto-include cover from source when saving all fields CLI without explicit cover option? Keep optional: include if source has cover
                // For now include automatically when no explicit tags (full copy). If user wants without cover, they can use explicit opts without cover.
                try { cover = PresetService.CoverFromMediaInfo(sourceInfo); } catch (Exception ex) { ErrorHandler.Fail(ex.Message, 2); return Task.FromResult(2); }
            }

            if (tags.IsEmpty && cover is null)
            {
                ErrorHandler.Fail("Preset is empty — no tag provided. Use --artist, --genre, --copyright, etc.");
                return Task.FromResult(1);
            }

            var preset = new PresetFile
            {
                Name = name,
                Created = DateTime.UtcNow,
                SourceFile = sourceFile,
                Tags = tags,
                Cover = cover
            };

            PresetService.Save(preset, force);
            AnsiConsole.MarkupLine($"[green]Preset saved:[/] {Markup.Escape(preset.Name)} [grey]({Markup.Escape(PresetService.GetPresetPath(preset.Name))})[/]");
            if (cover is not null)
            {
                var len = PresetService.GetCoverByteLength(cover);
                var warn = len > 5 * 1024 * 1024 ? " [yellow]Warning: large cover.[/]" : "";
                AnsiConsole.MarkupLine($"[grey]Cover: {len / 1024} KB{warn}[/]");
            }
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    private static Task<int> ListAsync(string[] args)
    {
        bool json = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "--json") json = true;
            else if (a is "--help" or "-h") { PrintListHelp(); return Task.FromResult(0); }
            else if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
            else { ErrorHandler.Fail($"Unexpected argument: \"{a}\""); return Task.FromResult(1); }
        }

        var names = PresetService.List();
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(names, new JsonSerializerOptions { WriteIndented = true }));
            return Task.FromResult(0);
        }

        if (names.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]No presets found in {Markup.Escape(PresetService.GetPresetDirectory())}[/]");
            return Task.FromResult(0);
        }

        var table = new Table().Border(TableBorder.Rounded).Title("[cyan]Presets[/]");
        table.AddColumn("Name");
        table.AddColumn("File");
        table.AddColumn("Fields");
        table.AddColumn("Cover");
        foreach (var n in names)
        {
            try
            {
                var p = PresetService.Load(n);
                var fields = DescribeFields(p);
                var hasCover = p.Cover is not null ? $"{PresetService.GetCoverByteLength(p.Cover) / 1024} KB" : "-";
                table.AddRow(Markup.Escape(p.Name), Markup.Escape(PresetService.GetPresetPath(n)), Markup.Escape(fields), hasCover);
            }
            catch
            {
                table.AddRow(Markup.Escape(n), Markup.Escape(PresetService.GetPresetPath(n)), "[red]corrupted[/]", "-");
            }
        }
        AnsiConsole.Write(table);
        return Task.FromResult(0);
    }

    private static Task<int> ShowAsync(string[] args)
    {
        string? name = null;
        bool json = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--json": json = true; break;
                case "--help": case "-h": PrintShowHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    if (name is null) name = a;
                    else { ErrorHandler.Fail("Too many arguments"); return Task.FromResult(1); }
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorHandler.Fail("Missing preset name. Usage: odmedia preset show <name>");
            return Task.FromResult(1);
        }

        try
        {
            var p = PresetService.Load(name);
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
                return Task.FromResult(0);
            }

            AnsiConsole.MarkupLine($"[cyan]Preset:[/] {Markup.Escape(p.Name)} [grey]({p.Created:u})[/]");
            if (!string.IsNullOrWhiteSpace(p.SourceFile))
                AnsiConsole.MarkupLine($"[grey]Source: {Markup.Escape(p.SourceFile!)}[/]");

            var table = new Table().Border(TableBorder.Rounded).Title("[yellow]Tags[/]");
            table.AddColumn("Field");
            table.AddColumn("Value");
            var t = p.Tags;
            if (t.Artists is not null) table.AddRow("Artist", Markup.Escape(string.Join("; ", t.Artists)));
            if (t.Album is not null) table.AddRow("Album", Markup.Escape(t.Album));
            if (t.AlbumArtists is not null) table.AddRow("Album Artist", Markup.Escape(string.Join("; ", t.AlbumArtists)));
            if (t.Genres is not null) table.AddRow("Genre", Markup.Escape(string.Join("; ", t.Genres)));
            if (t.Year.HasValue) table.AddRow("Year", t.Year.Value.ToString());
            if (t.Composers is not null) table.AddRow("Composer", Markup.Escape(string.Join("; ", t.Composers)));
            if (t.Comment is not null) table.AddRow("Comment", Markup.Escape(t.Comment));
            if (t.Copyright is not null) table.AddRow("Copyright", Markup.Escape(t.Copyright));
            if (t.Publisher is not null) table.AddRow("Publisher", Markup.Escape(t.Publisher));
            if (t.IsEmpty) table.AddRow("[grey]-[/]", "[grey]no tag fields[/]");
            AnsiConsole.Write(table);

            if (p.Cover is not null)
            {
                var len = PresetService.GetCoverByteLength(p.Cover);
                var sizeStr = len > 1024 * 1024 ? $"{len / (1024.0 * 1024):F1} MB" : $"{len / 1024} KB";
                AnsiConsole.MarkupLine($"[green]Cover:[/] {Markup.Escape(p.Cover.MimeType)} {sizeStr} [grey]({Markup.Escape(p.Cover.Description)})[/]");
                if (len > 5 * 1024 * 1024)
                    AnsiConsole.MarkupLine("[yellow]Warning: cover is larger than 5 MB.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Cover: none[/]");
            }

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    private static Task<int> ApplyAsync(string[] args)
    {
        string? target = null;
        string? presetName = null;
        bool noBackup = false;
        bool keepMtime = false;
        bool dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--name": presetName = Next(args, ref i, "--name"); break;
                case "--preset": presetName = Next(args, ref i, "--preset"); break;
                case "--no-backup": noBackup = true; break;
                case "--backup": noBackup = false; break;
                case "--keep-mtime": keepMtime = true; break;
                case "--dry-run": dryRun = true; break;
                case "--help": case "-h": PrintApplyHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    if (target is null) target = a;
                    else { ErrorHandler.Fail("Too many files — apply one file at a time"); return Task.FromResult(1); }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia preset apply <file> --name <presetName>");
            return Task.FromResult(1);
        }
        if (string.IsNullOrWhiteSpace(presetName))
        {
            ErrorHandler.Fail("Missing --name <presetName>");
            return Task.FromResult(1);
        }

        target = target.Trim().Trim('"');
        if (!File.Exists(target))
        {
            ErrorHandler.Fail($"File not found: \"{target}\"");
            return Task.FromResult(1);
        }

        try
        {
            var preset = PresetService.Load(presetName);
            var opts = PresetService.ToEditOptions(preset, target, noBackup, keepMtime, dryRun);

            if (dryRun)
            {
                AnsiConsole.MarkupLine($"[yellow]Dry-run:[/] would apply preset [cyan]{Markup.Escape(preset.Name)}[/] to {Markup.Escape(target)}");
                DumpPresetApply(preset, target);
                PresetService.CleanupTempCover(opts);
                return Task.FromResult(0);
            }

            var service = new MediaTagService();
            // Save with temp cover file handling
            try
            {
                service.Save(opts);
            }
            finally
            {
                PresetService.CleanupTempCover(opts);
            }

            AnsiConsole.MarkupLine($"[green]Applied preset[/] {Markup.Escape(preset.Name)} [green]to[/] {Markup.Escape(target)}" + (noBackup ? "" : " [grey](backup .bak available)[/]"));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    private static Task<int> DeleteAsync(string[] args)
    {
        string? name = null;
        bool yes = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--yes": case "-y": yes = true; break;
                case "--help": case "-h": PrintDeleteHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    if (name is null) name = a;
                    else { ErrorHandler.Fail("Too many arguments"); return Task.FromResult(1); }
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorHandler.Fail("Missing preset name. Usage: odmedia preset delete <name> [--yes]");
            return Task.FromResult(1);
        }

        if (!PresetService.Exists(name))
        {
            ErrorHandler.Fail($"Preset not found: \"{name}\"");
            return Task.FromResult(1);
        }

        if (!yes)
        {
            if (!AnsiConsole.Confirm($"Delete preset \"{Markup.Escape(name)}\"?", false))
                return Task.FromResult(0);
        }

        try
        {
            PresetService.Delete(name);
            AnsiConsole.MarkupLine($"[green]Deleted:[/] {Markup.Escape(name)}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    private static void DumpPresetApply(PresetFile p, string target)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("New Value");
        var t = p.Tags;
        if (t.Artists is not null) table.AddRow("Artist", Markup.Escape(string.Join("; ", t.Artists)));
        if (t.Album is not null) table.AddRow("Album", Markup.Escape(t.Album));
        if (t.AlbumArtists is not null) table.AddRow("Album Artist", Markup.Escape(string.Join("; ", t.AlbumArtists)));
        if (t.Genres is not null) table.AddRow("Genre", Markup.Escape(string.Join("; ", t.Genres)));
        if (t.Year.HasValue) table.AddRow("Year", t.Year.Value.ToString());
        if (t.Composers is not null) table.AddRow("Composer", Markup.Escape(string.Join("; ", t.Composers)));
        if (t.Comment is not null) table.AddRow("Comment", Markup.Escape(t.Comment));
        if (t.Copyright is not null) table.AddRow("Copyright", Markup.Escape(t.Copyright));
        if (t.Publisher is not null) table.AddRow("Publisher", Markup.Escape(t.Publisher));
        if (p.Cover is not null) table.AddRow("Cover", $"[green]embed {PresetService.GetCoverByteLength(p.Cover) / 1024} KB[/]");
        AnsiConsole.Write(table);
    }

    private static string DescribeFields(PresetFile p)
    {
        var list = new List<string>();
        var t = p.Tags;
        if (t.Artists is not null) list.Add("Artist");
        if (t.Album is not null) list.Add("Album");
        if (t.AlbumArtists is not null) list.Add("AlbumArt");
        if (t.Genres is not null) list.Add("Genre");
        if (t.Year.HasValue) list.Add("Year");
        if (t.Composers is not null) list.Add("Composer");
        if (t.Comment is not null) list.Add("Comment");
        if (t.Copyright is not null) list.Add("Copyright");
        if (t.Publisher is not null) list.Add("Publisher");
        if (p.Cover is not null) list.Add("Cover");
        return list.Count == 0 ? "-" : string.Join(", ", list);
    }

    private static string[] Split(string s) => s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string Next(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {name}");
        i++;
        return args[i];
    }

    /// <summary>Writes the preset command help text.</summary>
    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia preset <command> [options]");
        AnsiConsole.WriteLine("  Manage presets stored in <exeDir>/presets/*.json");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Commands:");
        AnsiConsole.WriteLine("  save   <sourceFile> --name <name> [options]   Create preset from file or from scratch");
        AnsiConsole.WriteLine("  list   [--json]                                List presets");
        AnsiConsole.WriteLine("  show   <name> [--json]                          Show preset details");
        AnsiConsole.WriteLine("  apply  <file> --name <name> [--dry-run]        Apply preset to file (one at a time)");
        AnsiConsole.WriteLine("  delete <name> [--yes]                           Delete preset");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Aliases: presets");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("See: odmedia preset save --help, odmedia preset apply --help, etc.");
    }

    private static void PrintSaveHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia preset save [sourceFile] --name <name> [options]");
        AnsiConsole.WriteLine("  Create a preset. If sourceFile is provided, tags are copied from it (filtered to generic fields).");
        AnsiConsole.WriteLine("  Fields: only generic tags are stored (Title/Track/Disc excluded by design).");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Options:");
        AnsiConsole.WriteLine("  --name <name>              Preset name (required, max 64 chars)");
        AnsiConsole.WriteLine("  --artist <a;b>             Artist(s)");
        AnsiConsole.WriteLine("  --album <text>             Album");
        AnsiConsole.WriteLine("  --album-artist <a;b>       Album artist(s)");
        AnsiConsole.WriteLine("  --genre <g1;g2>            Genre(s)");
        AnsiConsole.WriteLine("  --year <n>                 Year");
        AnsiConsole.WriteLine("  --comment <text>           Comment");
        AnsiConsole.WriteLine("  --composer <c1;c2>         Composer(s)");
        AnsiConsole.WriteLine("  --copyright <text>         Copyright");
        AnsiConsole.WriteLine("  --publisher <text>         Publisher / Label");
        AnsiConsole.WriteLine("  --cover <path>             Cover image (jpg/png/webp/bmp, max 10 MB) — optional, with size warning");
        AnsiConsole.WriteLine("  --cover-desc <text>        Cover description");
        AnsiConsole.WriteLine("  --force                    Overwrite an existing preset");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Examples:");
        AnsiConsole.WriteLine("  odmedia preset save track.mp3 --name \"MonLabel\"");
        AnsiConsole.WriteLine("  odmedia preset save --name \"MonLabel\" --artist \"John\" --genre \"Rock\" --copyright \"©2024\"");
        AnsiConsole.WriteLine("  odmedia preset save track.mp3 --name \"MonLabel\" --cover cover.jpg");
    }

    private static void PrintListHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia preset list [--json]");
        AnsiConsole.WriteLine("  List presets in <exeDir>/presets/");
    }

    private static void PrintShowHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia preset show <name> [--json]");
        AnsiConsole.WriteLine("  Show preset details.");
    }

    private static void PrintApplyHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia preset apply <file> --name <presetName> [options]");
        AnsiConsole.WriteLine("  Apply preset tags to file (one file at a time). Title/Track/Disc are never modified.");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Options:");
        AnsiConsole.WriteLine("  --name <name>      Preset name (required)");
        AnsiConsole.WriteLine("  --no-backup        Do not create .bak backup");
        AnsiConsole.WriteLine("  --keep-mtime       Preserve file modification time");
        AnsiConsole.WriteLine("  --dry-run          Show what would be done");
    }

    private static void PrintDeleteHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia preset delete <name> [--yes]");
        AnsiConsole.WriteLine("  Delete preset file.");
    }
}

