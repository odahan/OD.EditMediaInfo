using System.Text.Json;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

/// <summary>Implements commands used to create and apply tag presets.</summary>
public static class PresetCommand
{
    public static Task<int> SaveAsync(string? sourceFile, string name, string? artist, string? album, string? albumArtist,
        string? genre, uint? year, string? comment, string? composer, string? copyright, string? publisher,
        string? cover, string? coverType, string? coverDesc, bool force)
    {
        // parse arrays
        string[]? artists = artist is not null ? ArgHelpers.Split(artist) : null;
        string[]? albumArtists = albumArtist is not null ? ArgHelpers.Split(albumArtist) : null;
        string[]? genres = genre is not null ? ArgHelpers.Split(genre) : null;
        string[]? composers = composer is not null ? ArgHelpers.Split(composer) : null;
        bool hasExplicitTagOption = artist is not null || album is not null || albumArtist is not null ||
                                     genre is not null || year.HasValue || comment is not null || composer is not null ||
                                     copyright is not null || publisher is not null || cover is not null;

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorHandler.Fail("Missing --name <presetName>. Usage: odmedia preset save [--name <name>] [sourceFile] [options]");
            return Task.FromResult(1);
        }

        MediaInfo? sourceInfo = null;
        if (!string.IsNullOrWhiteSpace(sourceFile))
        {
            sourceFile = sourceFile.Trim();
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

        try
        {
            var tags = new PresetTags();

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
                if (sourceInfo is not null)
                {
                    tags.Artists = artists ?? (sourceInfo.Artists.Length > 0 ? sourceInfo.Artists : null);
                    tags.Album = album ?? (string.IsNullOrWhiteSpace(sourceInfo.Album) ? null : sourceInfo.Album);
                    tags.AlbumArtists = albumArtists ?? (sourceInfo.AlbumArtists.Length > 0 ? sourceInfo.AlbumArtists : null);
                    tags.Genres = genres ?? (sourceInfo.Genres.Length > 0 ? sourceInfo.Genres : null);
                    tags.Year = year ?? (sourceInfo.Year == 0 ? null : sourceInfo.Year);
                    tags.Comment = comment ?? (string.IsNullOrWhiteSpace(sourceInfo.Comment) ? null : sourceInfo.Comment);
                    tags.Composers = composers ?? (sourceInfo.Composers.Length > 0 ? sourceInfo.Composers : null);
                    tags.Copyright = copyright ?? (string.IsNullOrWhiteSpace(sourceInfo.Copyright) ? null : sourceInfo.Copyright);
                    tags.Publisher = publisher ?? (string.IsNullOrWhiteSpace(sourceInfo.Publisher) ? null : sourceInfo.Publisher);
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

            PresetCover? presetCover = null;
            if (cover is not null)
            {
                var coverTypeEnum = coverType?.ToLowerInvariant() switch
                {
                    "back" => "Back",
                    "other" => "Other",
                    _ => "Front"
                };
                presetCover = PresetService.CoverFromFile(cover, coverDesc, coverTypeEnum);
            }
            else if (sourceInfo is not null && !hasExplicitTagOption)
            {
                try { presetCover = PresetService.CoverFromMediaInfo(sourceInfo); }
                catch (Exception ex) { ErrorHandler.Fail(ex.Message, 2); return Task.FromResult(2); }
            }

            if (tags.IsEmpty && presetCover is null)
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
                Cover = presetCover
            };

            PresetService.Save(preset, force);
            AnsiConsole.MarkupLine($"[green]Preset saved:[/] {Markup.Escape(preset.Name)} [grey]({Markup.Escape(PresetService.GetPresetPath(preset.Name))})[/]");
            if (presetCover is not null)
            {
                var len = PresetService.GetCoverByteLength(presetCover);
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

    public static Task<int> ListAsync(bool json)
    {
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

    public static Task<int> ShowAsync(string name, bool json)
    {
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
                AnsiConsole.MarkupLine($"[green]Cover:[/] {Markup.Escape(p.Cover.MimeType)} {sizeStr} [grey](type {p.Cover.Type ?? "Front"})[/]");
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

    public static Task<int> ApplyAsync(string file, string name, bool noBackup, bool keepMtime, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia preset apply <file> --name <presetName>");
            return Task.FromResult(1);
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorHandler.Fail("Missing --name <presetName>");
            return Task.FromResult(1);
        }

        file = file.Trim();
        if (!File.Exists(file))
        {
            ErrorHandler.Fail($"File not found: \"{file}\"");
            return Task.FromResult(1);
        }

        try
        {
            var preset = PresetService.Load(name);
            var opts = PresetService.ToEditOptions(preset, file, noBackup, keepMtime, dryRun);

            if (dryRun)
            {
                AnsiConsole.MarkupLine($"[yellow]Dry-run:[/] would apply preset [cyan]{Markup.Escape(preset.Name)}[/] to {Markup.Escape(file)}");
                DumpPresetApply(preset, file);
                PresetService.CleanupTempCover(opts);
                return Task.FromResult(0);
            }

            var service = new MediaTagService();
            try
            {
                service.Save(opts);
            }
            finally
            {
                PresetService.CleanupTempCover(opts);
            }

            AnsiConsole.MarkupLine($"[green]Applied preset[/] {Markup.Escape(preset.Name)} [green]to[/] {Markup.Escape(file)}" + (noBackup ? "" : " [grey](backup .bak available)[/]"));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    public static Task<int> DeleteAsync(string name, bool yes)
    {
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
}