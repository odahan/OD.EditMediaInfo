using OD.EditMediaInfo.Models;
using Spectre.Console;

namespace OD.EditMediaInfo.Services;

/// <summary>Provides the interactive terminal workflow for viewing and editing media files.</summary>
public static class InteractiveEditor
{
    /// <summary>Runs the interactive editor until the user exits or cancels it.</summary>
    public static async Task<int> RunAsync(string? initialInput, CancellationToken ct)
    {
        var service = new MediaTagService();

        string? currentFile = initialInput?.Trim().Trim('"');

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Console.Clear();
            AnsiConsole.Write(new FigletText("odmedia").Color(Color.Cyan1));
            var version = typeof(InteractiveEditor).Assembly.GetName().Version?.ToString(3) ?? "unknown";
            AnsiConsole.MarkupLine($"[grey]Version {Markup.Escape(version)} | Media info viewer & editor | leave empty to quit[/]");

    if (string.IsNullOrWhiteSpace(currentFile))
    {
        currentFile = AnsiConsole.Prompt(
            new TextPrompt<string>("Media file [grey](drag & drop or path)[/] :")
                .AllowEmpty()
                .Validate(p =>
                {
                    if (string.IsNullOrWhiteSpace(p)) return ValidationResult.Success();
                    var t = p.Trim().Trim('"');
                    return File.Exists(t) ? ValidationResult.Success() : ValidationResult.Error("File not found");
                }));
        currentFile = currentFile.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(currentFile))
            return 0;
    }
    else
    {
        currentFile = currentFile.Trim().Trim('"');
        // If the passed file doesn't exist, clear it to force prompt later (but it will be caught below anyway)
    }

            if (!File.Exists(currentFile))
            {
                ErrorHandler.FailInteractive($"File not found: \"{currentFile}\"");
                currentFile = null;
                continue;
            }

            MediaInfo info;
            try
            {
                info = service.Read(currentFile);
            }
            catch (Exception ex)
            {
                ErrorHandler.FailInteractive(ex.Message);
                currentFile = null;
                continue;
            }

            MediaProbeService.Render(info);

            var choices = new List<string>
            {
                "Edit Title",
                "Edit Artist",
                "Edit Album",
                "Edit Album Artist",
                "Edit Genre",
                "Edit Year",
                "Edit Track",
                "Edit Disc",
                "Edit Comment",
                "Edit Composer",
                "Edit Copyright",
                "Edit Publisher",
                "Cover — Add/Replace",
                "Cover — Remove",
                "Cover — Extract to file",
                "Preset — Create from this file",
                "Preset — Create from scratch",
                "Preset — Apply to this file",
                "Preset — Manage (list/delete)",
                "Clear All Tags",
                "Change File",
                "Quit"
            };

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What to do? [grey](arrows + Enter)[/]")
                    .PageSize(16)
                    .AddChoices(choices));

            if (choice == "Quit")
                return 0;

            if (choice == "Change File")
            {
                currentFile = null;
                continue;
            }

            if (choice == "Clear All Tags")
            {
                if (AnsiConsole.Confirm($"Remove [red]all tags[/] from \"{Markup.Escape(currentFile)}\"?", false))
                {
                    try
                    {
                        service.Clear(currentFile, noBackup: false, keepMtime: false);
                        AnsiConsole.MarkupLine("[green]All tags cleared (backup .bak available).[/]");
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler.FailInteractive(ex.Message);
                    }
                }
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
                continue;
            }

            if (choice == "Cover — Extract to file")
            {
                if (info.Pictures.Count == 0)
                {
                    ErrorHandler.FailInteractive("No cover to extract.");
                    continue;
                }
                var outPath = AnsiConsole.Ask<string>("Output image path:", Path.Combine(Path.GetDirectoryName(currentFile) ?? ".", Path.GetFileNameWithoutExtension(currentFile) + "-cover.jpg"));
                outPath = outPath.Trim().Trim('"');
                try
                {
                    using var tagFile = TagLib.File.Create(currentFile);
                    var pic = tagFile.Tag.Pictures.FirstOrDefault();
                    if (pic is null) throw new InvalidOperationException("No cover found.");
                    File.WriteAllBytes(outPath, pic.Data.Data);
                    AnsiConsole.MarkupLine($"[green]Cover extracted:[/] {Markup.Escape(outPath)} ({pic.Data.Data.Length / 1024} KB)");
                }
                catch (Exception ex)
                {
                    ErrorHandler.FailInteractive(ex.Message);
                    continue;
                }
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
                continue;
            }

            if (choice == "Cover — Remove")
            {
                if (info.Pictures.Count == 0)
                {
                    ErrorHandler.FailInteractive("No cover to remove.");
                    continue;
                }
                if (!AnsiConsole.Confirm("Remove all covers?", false)) continue;
                try
                {
                    service.Save(new EditOptions { FilePath = currentFile, RemoveCover = true });
                    AnsiConsole.MarkupLine("[green]Cover removed (backup .bak available).[/]");
                }
                catch (Exception ex)
                {
                    ErrorHandler.FailInteractive(ex.Message);
                }
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
                continue;
            }

            if (choice == "Preset — Create from this file")
            {
                await HandlePresetCreateFromFileAsync(currentFile, info);
                continue;
            }

            if (choice == "Preset — Create from scratch")
            {
                await HandlePresetCreateFromScratchAsync();
                continue;
            }

            if (choice == "Preset — Apply to this file")
            {
                await HandlePresetApplyAsync(currentFile, service);
                continue;
            }

            if (choice == "Preset — Manage (list/delete)")
            {
                await HandlePresetManageAsync();
                continue;
            }

            try
            {
                var edit = await PromptForEditAsync(choice, info, currentFile);
                if (edit is null) continue; // cancelled

                bool confirm = AnsiConsole.Confirm("Save changes?", true);
                if (!confirm) continue;

                service.Save(edit);
                AnsiConsole.MarkupLine("[green]Saved (backup .bak available).[/]");
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
            }
            catch (OperationCanceledException)
            {
                return 130;
            }
            catch (Exception ex)
            {
                ErrorHandler.FailInteractive(ex.Message);
            }
        }
    }

    private static Task<EditOptions?> PromptForEditAsync(string choice, MediaInfo info, string file)
    {
        EditOptions? result = choice switch
        {
            "Edit Title" => new EditOptions
            {
                FilePath = file,
                Title = AnsiConsole.Prompt(new TextPrompt<string>("Title:").DefaultValue(info.Title).AllowEmpty())
            },
            "Edit Artist" => new EditOptions
            {
                FilePath = file,
                Artists = Split(AnsiConsole.Prompt(new TextPrompt<string>("Artist(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.Artists)).AllowEmpty()))
            },
            "Edit Album" => new EditOptions
            {
                FilePath = file,
                Album = AnsiConsole.Prompt(new TextPrompt<string>("Album:").DefaultValue(info.Album).AllowEmpty())
            },
            "Edit Album Artist" => new EditOptions
            {
                FilePath = file,
                AlbumArtists = Split(AnsiConsole.Prompt(new TextPrompt<string>("Album Artist(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.AlbumArtists)).AllowEmpty()))
            },
            "Edit Genre" => new EditOptions
            {
                FilePath = file,
                Genres = Split(AnsiConsole.Prompt(new TextPrompt<string>("Genre(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.Genres)).AllowEmpty()))
            },
            "Edit Year" => BuildYear(file, info),
            "Edit Track" => BuildTrack(file, info),
            "Edit Disc" => BuildDisc(file, info),
            "Edit Comment" => new EditOptions
            {
                FilePath = file,
                Comment = AnsiConsole.Prompt(new TextPrompt<string>("Comment:").DefaultValue(info.Comment).AllowEmpty())
            },
            "Edit Composer" => new EditOptions
            {
                FilePath = file,
                Composers = Split(AnsiConsole.Prompt(new TextPrompt<string>("Composer(s) [grey]; separated[/]:").DefaultValue(string.Join("; ", info.Composers)).AllowEmpty()))
            },
            "Edit Copyright" => new EditOptions
            {
                FilePath = file,
                Copyright = AnsiConsole.Prompt(new TextPrompt<string>("Copyright:").DefaultValue(info.Copyright).AllowEmpty())
            },
            "Edit Publisher" => new EditOptions
            {
                FilePath = file,
                Publisher = AnsiConsole.Prompt(new TextPrompt<string>("Publisher:").DefaultValue(info.Publisher).AllowEmpty())
            },
            "Cover — Add/Replace" => BuildCover(file),
            _ => null
        };
        return Task.FromResult(result);
    }

    private static EditOptions BuildYear(string file, MediaInfo info)
    {
        var s = AnsiConsole.Prompt(new TextPrompt<string>("Year:").DefaultValue(info.Year == 0 ? "" : info.Year.ToString()).AllowEmpty());
        s = s.Trim();
        if (string.IsNullOrEmpty(s)) return new EditOptions { FilePath = file, Year = 0 };
        if (!uint.TryParse(s, out var y)) throw new InvalidOperationException("Invalid year");
        return new EditOptions { FilePath = file, Year = y };
    }

    private static EditOptions BuildTrack(string file, MediaInfo info)
    {
        var cur = info.Track == 0 ? "" : info.Track.ToString();
        var curCount = info.TrackCount == 0 ? "" : info.TrackCount.ToString();
        var t = AnsiConsole.Prompt(new TextPrompt<string>("Track number:").DefaultValue(cur).AllowEmpty()).Trim();
        var tc = AnsiConsole.Prompt(new TextPrompt<string>("Track count (total):").DefaultValue(curCount).AllowEmpty()).Trim();
        uint? track = string.IsNullOrEmpty(t) ? 0 : uint.Parse(t);
        uint? trackCount = string.IsNullOrEmpty(tc) ? 0 : uint.Parse(tc);
        return new EditOptions { FilePath = file, Track = track, TrackCount = trackCount };
    }

    private static EditOptions BuildDisc(string file, MediaInfo info)
    {
        var cur = info.Disc == 0 ? "" : info.Disc.ToString();
        var curCount = info.DiscCount == 0 ? "" : info.DiscCount.ToString();
        var d = AnsiConsole.Prompt(new TextPrompt<string>("Disc number:").DefaultValue(cur).AllowEmpty()).Trim();
        var dc = AnsiConsole.Prompt(new TextPrompt<string>("Disc count (total):").DefaultValue(curCount).AllowEmpty()).Trim();
        uint? disc = string.IsNullOrEmpty(d) ? 0 : uint.Parse(d);
        uint? discCount = string.IsNullOrEmpty(dc) ? 0 : uint.Parse(dc);
        return new EditOptions { FilePath = file, Disc = disc, DiscCount = discCount };
    }

    private static EditOptions BuildCover(string file)
    {
        var path = AnsiConsole.Prompt(new TextPrompt<string>("Cover image path [grey](jpg/png, max 10 MB)[/]:").AllowEmpty());
        path = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Cover path empty — cancelled.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Cover file not found: \"{path}\"");
        var len = new FileInfo(path).Length;
        if (len > 10L * 1024 * 1024)
            throw new InvalidOperationException($"Cover too large ({len / (1024.0 * 1024):F1} MB > 10 MB).");
        return new EditOptions { FilePath = file, CoverPath = path };
    }

    private static string[] Split(string s) => s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    // ---- Preset helpers ----

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

    private static Task HandlePresetApplyAsync(string currentFile, MediaTagService svc)
    {
        try
        {
            var names = PresetService.List();
            if (names.Count == 0)
            {
                ErrorHandler.FailInteractive($"No presets found in {PresetService.GetPresetDirectory()}");
                return Task.CompletedTask;
            }

            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose a preset to apply:")
                    .PageSize(12)
                    .AddChoices(names));

            var preset = PresetService.Load(chosen);

            // Preview
            AnsiConsole.MarkupLine($"[cyan]Preset:[/] {Markup.Escape(preset.Name)} [grey]({preset.Created:u})[/]");
            var preview = new Table().Border(TableBorder.Rounded).Title("[yellow]Contenu preset[/]");
            preview.AddColumn("Field");
            preview.AddColumn("Value");
            var t = preset.Tags;
            if (t.Artists is not null) preview.AddRow("Artist", Markup.Escape(string.Join("; ", t.Artists)));
            if (t.Album is not null) preview.AddRow("Album", Markup.Escape(t.Album));
            if (t.AlbumArtists is not null) preview.AddRow("Album Artist", Markup.Escape(string.Join("; ", t.AlbumArtists)));
            if (t.Genres is not null) preview.AddRow("Genre", Markup.Escape(string.Join("; ", t.Genres)));
            if (t.Year.HasValue) preview.AddRow("Year", t.Year.Value.ToString());
            if (t.Composers is not null) preview.AddRow("Composer", Markup.Escape(string.Join("; ", t.Composers)));
            if (t.Comment is not null) preview.AddRow("Comment", Markup.Escape(t.Comment));
            if (t.Copyright is not null) preview.AddRow("Copyright", Markup.Escape(t.Copyright));
            if (t.Publisher is not null) preview.AddRow("Publisher", Markup.Escape(t.Publisher));
            if (preset.Cover is not null) preview.AddRow("Cover", $"{PresetService.GetCoverByteLength(preset.Cover) / 1024} KB {Markup.Escape(preset.Cover.MimeType)} [yellow]attention taille[/]");
            AnsiConsole.Write(preview);

            if (!AnsiConsole.Confirm($"Apply this preset to \"{Markup.Escape(currentFile)}\"?", true))
                return Task.CompletedTask;

            var opts = PresetService.ToEditOptions(preset, currentFile);
            try
            {
                svc.Save(opts);
                AnsiConsole.MarkupLine("[green]Preset applied (backup .bak available).[/]");
            }
            finally
            {
                PresetService.CleanupTempCover(opts);
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.FailInteractive(ex.Message);
        }
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        try { Console.ReadKey(true); } catch { Console.ReadLine(); }
        return Task.CompletedTask;
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
                    var coverStr = p.Cover is not null ? $"{PresetService.GetCoverByteLength(p.Cover)/1024} KB" : "-";
                    table.AddRow((i+1).ToString(), Markup.Escape(p.Name), Markup.Escape(string.IsNullOrEmpty(fields) ? "-" : fields), coverStr, p.Created.ToString("u"));
                }
                catch
                {
                    table.AddRow((i+1).ToString(), Markup.Escape(n), "[red]corrupted[/]", "-", "-");
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
                    if (p.Cover is not null) det.AddRow("Cover", $"{PresetService.GetCoverByteLength(p.Cover)/1024} KB {Markup.Escape(p.Cover.MimeType)}");
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

