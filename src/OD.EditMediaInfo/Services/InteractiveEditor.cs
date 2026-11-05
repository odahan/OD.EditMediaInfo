using OD.EditMediaInfo.Models;
using Spectre.Console;

namespace OD.EditMediaInfo.Services;

/// <summary>Provides interactive preset management.</summary>
public static partial class InteractiveEditor
{
    private static string[] Split(string value) => value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Lists fields supported by reusable audio/video presets.</summary>

    private static readonly string[] PresetFieldChoices =
    {
        "Artist",
        "Album",
        "Album Artist",
        "Genre",
        "Year",
        "Composer",
        "Comment",
        "Copyright",
        "Publisher",
        "Cover (warning: max. 10 MB)"
    };

    private static async Task HandlePresetCreateFromFileAsync(string currentFile, MediaInfo info)
    {
        try
        {
            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Fields to include in the preset [grey](Space to select, Enter to confirm)[/]")
                    .PageSize(14)
                    .Required()
                    .AddChoices(PresetFieldChoices));

            var tags = new PresetTags();
            PresetCover? cover = null;

            foreach (var field in selected)
            {
                switch (field)
                {
                    case "Artist":
                        tags.Artists = Split(AnsiConsole.Prompt(new TextPrompt<string>("Artist(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.Artists)).AllowEmpty()));
                        if (tags.Artists.Length == 0) tags.Artists = null;
                        break;
                    case "Album":
                        tags.Album = AnsiConsole.Prompt(new TextPrompt<string>("Album:").DefaultValue(info.Album).AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Album)) tags.Album = null;
                        break;
                    case "Album Artist":
                        tags.AlbumArtists = Split(AnsiConsole.Prompt(new TextPrompt<string>("Album Artist(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.AlbumArtists)).AllowEmpty()));
                        if (tags.AlbumArtists.Length == 0) tags.AlbumArtists = null;
                        break;
                    case "Genre":
                        tags.Genres = Split(AnsiConsole.Prompt(new TextPrompt<string>("Genre(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.Genres)).AllowEmpty()));
                        if (tags.Genres.Length == 0) tags.Genres = null;
                        break;
                    case "Year":
                        var yStr = AnsiConsole.Prompt(new TextPrompt<string>("Year:").DefaultValue(info.Year == 0 ? "" : info.Year.ToString()).AllowEmpty()).Trim();
                        if (string.IsNullOrWhiteSpace(yStr)) tags.Year = null;
                        else if (uint.TryParse(yStr, out var y)) tags.Year = y;
                        else throw new InvalidOperationException("Invalid year.");
                        break;
                    case "Composer":
                        tags.Composers = Split(AnsiConsole.Prompt(new TextPrompt<string>("Composer(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.Composers)).AllowEmpty()));
                        if (tags.Composers.Length == 0) tags.Composers = null;
                        break;
                    case "Comment":
                        tags.Comment = AnsiConsole.Prompt(new TextPrompt<string>("Comment:").DefaultValue(info.Comment).AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Comment)) tags.Comment = null;
                        break;
                    case "Copyright":
                        tags.Copyright = AnsiConsole.Prompt(new TextPrompt<string>("Copyright:").DefaultValue(info.Copyright).AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Copyright)) tags.Copyright = null;
                        break;
                    case "Publisher":
                        tags.Publisher = AnsiConsole.Prompt(new TextPrompt<string>("Publisher:").DefaultValue(info.Publisher).AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Publisher)) tags.Publisher = null;
                        break;
                    case "Cover (warning: max. 10 MB)":
                        cover = await PromptCoverForPresetAsync(info);
                        break;
                }
            }

            if (tags.IsEmpty && cover is null)
            {
                ErrorHandler.FailInteractive("No fields were selected. Preset creation was cancelled.");
                return;
            }

            var name = AnsiConsole.Ask<string>("Preset name [grey](max. 64 characters)[/]:");
            name = name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorHandler.FailInteractive("Preset name cannot be empty. Creation was cancelled.");
                return;
            }

            var safe = PresetService.SanitizeName(name);
            if (PresetService.Exists(safe))
            {
                if (!AnsiConsole.Confirm($"Preset \"{Markup.Escape(safe)}\" already exists. Overwrite it?", false))
                    return;
            }

            var preset = new PresetFile
            {
                Name = safe,
                Created = DateTime.UtcNow,
                SourceFile = currentFile,
                Tags = tags,
                Cover = cover
            };

            PresetService.Save(preset, overwrite: true);
            var bytesInfo = cover is not null ? $" [grey](cover {PresetService.GetCoverByteLength(cover) / 1024} KB)[/]" : "";
            AnsiConsole.MarkupLine($"[green]Preset created:[/] {Markup.Escape(safe)}[grey] -> {Markup.Escape(PresetService.GetPresetPath(safe))}[/]{bytesInfo}");
            if (cover is not null && PresetService.GetCoverByteLength(cover) > 5 * 1024 * 1024)
                AnsiConsole.MarkupLine("[yellow]Warning: cover is larger than 5 MB.[/]");
        }
        catch (Exception ex)
        {
            ErrorHandler.FailInteractive(ex.Message);
        }
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        try { Console.ReadKey(true); } catch { Console.ReadLine(); }
    }

    private static async Task HandlePresetCreateFromScratchAsync()
    {
        try
        {
            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Fields to include in the new preset [grey](Space to select)[/]")
                    .PageSize(14)
                    .Required()
                    .AddChoices(PresetFieldChoices));

            var tags = new PresetTags();
            PresetCover? cover = null;

            foreach (var field in selected)
            {
                switch (field)
                {
                    case "Artist":
                        tags.Artists = Split(AnsiConsole.Prompt(new TextPrompt<string>("Artist(s) [grey]; separated[/]:").AllowEmpty()));
                        if (tags.Artists.Length == 0) tags.Artists = null;
                        break;
                    case "Album":
                        tags.Album = AnsiConsole.Prompt(new TextPrompt<string>("Album:").AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Album)) tags.Album = null;
                        break;
                    case "Album Artist":
                        tags.AlbumArtists = Split(AnsiConsole.Prompt(new TextPrompt<string>("Album Artist(s) [grey]; separated[/]:").AllowEmpty()));
                        if (tags.AlbumArtists.Length == 0) tags.AlbumArtists = null;
                        break;
                    case "Genre":
                        tags.Genres = Split(AnsiConsole.Prompt(new TextPrompt<string>("Genre(s) [grey]; separated[/]:").AllowEmpty()));
                        if (tags.Genres.Length == 0) tags.Genres = null;
                        break;
                    case "Year":
                        var yStr = AnsiConsole.Prompt(new TextPrompt<string>("Year:").AllowEmpty()).Trim();
                        if (string.IsNullOrWhiteSpace(yStr)) tags.Year = null;
                        else if (uint.TryParse(yStr, out var y)) tags.Year = y;
                        else throw new InvalidOperationException("Invalid year.");
                        break;
                    case "Composer":
                        tags.Composers = Split(AnsiConsole.Prompt(new TextPrompt<string>("Composer(s) [grey]; separated[/]:").AllowEmpty()));
                        if (tags.Composers.Length == 0) tags.Composers = null;
                        break;
                    case "Comment":
                        tags.Comment = AnsiConsole.Prompt(new TextPrompt<string>("Comment:").AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Comment)) tags.Comment = null;
                        break;
                    case "Copyright":
                        tags.Copyright = AnsiConsole.Prompt(new TextPrompt<string>("Copyright:").AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Copyright)) tags.Copyright = null;
                        break;
                    case "Publisher":
                        tags.Publisher = AnsiConsole.Prompt(new TextPrompt<string>("Publisher:").AllowEmpty());
                        if (string.IsNullOrWhiteSpace(tags.Publisher)) tags.Publisher = null;
                        break;
                    case "Cover (warning: max. 10 MB)":
                        cover = await PromptCoverForPresetAsync(null);
                        break;
                }
            }

            if (tags.IsEmpty && cover is null)
            {
                ErrorHandler.FailInteractive("No fields were selected. Preset creation was cancelled.");
                return;
            }

            var name = AnsiConsole.Ask<string>("Preset name [grey](max. 64 characters)[/]:");
            name = name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorHandler.FailInteractive("Preset name cannot be empty. Creation was cancelled.");
                return;
            }
            var safe = PresetService.SanitizeName(name);
            if (PresetService.Exists(safe))
            {
                if (!AnsiConsole.Confirm($"Preset \"{Markup.Escape(safe)}\" already exists. Overwrite it?", false))
                    return;
            }

            var preset = new PresetFile
            {
                Name = safe,
                Created = DateTime.UtcNow,
                SourceFile = null,
                Tags = tags,
                Cover = cover
            };
            PresetService.Save(preset, overwrite: true);
            var bytesInfo = cover is not null ? $" [grey](cover {PresetService.GetCoverByteLength(cover) / 1024} KB)[/]" : "";
            AnsiConsole.MarkupLine($"[green]Preset created:[/] {Markup.Escape(safe)}[grey] -> {Markup.Escape(PresetService.GetPresetPath(safe))}[/]{bytesInfo}");
            if (cover is not null && PresetService.GetCoverByteLength(cover) > 5 * 1024 * 1024)
                AnsiConsole.MarkupLine("[yellow]Warning: cover is larger than 5 MB.[/]");
        }
        catch (Exception ex)
        {
            ErrorHandler.FailInteractive(ex.Message);
        }
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        try { Console.ReadKey(true); } catch { Console.ReadLine(); }
    }

    private static Task<PresetCover?> PromptCoverForPresetAsync(MediaInfo? info)
    {
        if (info is not null && info.Pictures.Count > 0)
        {
            var useSource = AnsiConsole.Confirm($"Use the current file cover? [grey]({info.Pictures.Count} cover(s))[/]", true);
            if (useSource)
            {
                var c = PresetService.CoverFromMediaInfo(info);
                if (c is not null)
                {
                    var len = PresetService.GetCoverByteLength(c);
                    AnsiConsole.MarkupLine($"[grey]Cover source: {len / 1024} KB, {Markup.Escape(c.MimeType)}[/]");
                    if (len > 5 * 1024 * 1024)
                        AnsiConsole.MarkupLine("[yellow]Warning: cover is larger than 5 MB.[/]");
                    if (len > 10L * 1024 * 1024)
                        throw new InvalidOperationException("Cover is too large (more than 10 MB).");
                }
                return Task.FromResult(c);
            }
        }

        var path = AnsiConsole.Prompt(new TextPrompt<string>("Cover image path [grey](JPG/PNG/WebP/BMP, max. 10 MB, empty to cancel)[/]:").AllowEmpty());
        path = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult<PresetCover?>(null);
        if (!File.Exists(path)) throw new FileNotFoundException($"Cover file not found: \"{path}\"");
        var fi = new FileInfo(path);
        if (fi.Length > 10L * 1024 * 1024) throw new InvalidOperationException($"Cover is too large ({fi.Length / (1024.0 * 1024):F1} MB > 10 MB).");
        if (fi.Length > 5 * 1024 * 1024) AnsiConsole.MarkupLine("[yellow]Warning: cover is larger than 5 MB.[/]");
        var cover = PresetService.CoverFromFile(path);
        return Task.FromResult<PresetCover?>(cover);
    }

    private static Task HandlePresetManageAsync()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine($"[cyan]Presets[/] — {Markup.Escape(PresetService.GetPresetDirectory())}");
            var names = PresetService.List();
            if (names.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No presets found.[/]");
                AnsiConsole.MarkupLine("[grey]Press any key to return...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
                return Task.CompletedTask;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("#");
            table.AddColumn("Name");
            table.AddColumn("Fields");
            table.AddColumn("Cover");
            table.AddColumn("Created");
            for (int i = 0; i < names.Count; i++)
            {
                var n = names[i];
                try
                {
                    var p = PresetService.Load(n);
                    var fields = string.Join(", ", new[]
                    {
                        p.Tags.Artists is not null ? "Artist" : null,
                        p.Tags.Album is not null ? "Album" : null,
                        p.Tags.AlbumArtists is not null ? "AlbumArt" : null,
                        p.Tags.Genres is not null ? "Genre" : null,
                        p.Tags.Year.HasValue ? "Year" : null,
                        p.Tags.Composers is not null ? "Composer" : null,
                        p.Tags.Comment is not null ? "Comment" : null,
                        p.Tags.Copyright is not null ? "Copy" : null,
                        p.Tags.Publisher is not null ? "Pub" : null,
                        p.Cover is not null ? "Cover" : null
                    }.Where(s => s is not null));
                    var coverStr = p.Cover is not null ? $"{PresetService.GetCoverByteLength(p.Cover) / 1024} KB" : "-";
                    table.AddRow((i + 1).ToString(), Markup.Escape(p.Name), Markup.Escape(string.IsNullOrEmpty(fields) ? "-" : fields), coverStr, p.Created.ToString("u"));
                }
                catch
                {
                    table.AddRow((i + 1).ToString(), Markup.Escape(n), "[red]corrupted[/]", "-", "-");
                }
            }
            AnsiConsole.Write(table);

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Action?")
                    .AddChoices(new[] { "View details", "Delete", "Back" }));

            if (action == "Back") return Task.CompletedTask;

            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose a preset:")
                    .AddChoices(names));

            if (action == "View details")
            {
                try
                {
                    var p = PresetService.Load(chosen);
                    AnsiConsole.MarkupLine($"[cyan]{Markup.Escape(p.Name)}[/] [grey]{p.Created:u}[/]");
                    var det = new Table().Border(TableBorder.Rounded);
                    det.AddColumn("Field");
                    det.AddColumn("Value");
                    var t = p.Tags;
                    if (t.Artists is not null) det.AddRow("Artist", Markup.Escape(string.Join("; ", t.Artists)));
                    if (t.Album is not null) det.AddRow("Album", Markup.Escape(t.Album));
                    if (t.AlbumArtists is not null) det.AddRow("Album Artist", Markup.Escape(string.Join("; ", t.AlbumArtists)));
                    if (t.Genres is not null) det.AddRow("Genre", Markup.Escape(string.Join("; ", t.Genres)));
                    if (t.Year.HasValue) det.AddRow("Year", t.Year.Value.ToString());
                    if (t.Composers is not null) det.AddRow("Composer", Markup.Escape(string.Join("; ", t.Composers)));
                    if (t.Comment is not null) det.AddRow("Comment", Markup.Escape(t.Comment));
                    if (t.Copyright is not null) det.AddRow("Copyright", Markup.Escape(t.Copyright));
                    if (t.Publisher is not null) det.AddRow("Publisher", Markup.Escape(t.Publisher));
                    if (p.Cover is not null) det.AddRow("Cover", $"{PresetService.GetCoverByteLength(p.Cover) / 1024} KB {Markup.Escape(p.Cover.MimeType)}");
                    AnsiConsole.Write(det);
                }
                catch (Exception ex)
                {
                    ErrorHandler.FailInteractive(ex.Message);
                }
                AnsiConsole.MarkupLine("[grey]Press any key...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
            }
            else if (action == "Delete")
            {
                if (AnsiConsole.Confirm($"Delete \"{Markup.Escape(chosen)}\"?", false))
                {
                    try
                    {
                        PresetService.Delete(chosen);
                        AnsiConsole.MarkupLine("[green]Deleted.[/]");
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler.FailInteractive(ex.Message);
                    }
                    AnsiConsole.MarkupLine("[grey]Press any key...[/]");
                    try { Console.ReadKey(true); } catch { Console.ReadLine(); }
                }
            }
        }
    }
}

