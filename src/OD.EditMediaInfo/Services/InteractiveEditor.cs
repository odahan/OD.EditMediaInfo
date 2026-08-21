using OD.EditMediaInfo.Models;
using Spectre.Console;

namespace OD.EditMediaInfo.Services;

public static class InteractiveEditor
{
    public static async Task<int> RunAsync(string? initialInput, CancellationToken ct)
    {
        var service = new MediaTagService();

        string? currentFile = initialInput?.Trim().Trim('"');

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Console.Clear();
            AnsiConsole.Write(new FigletText("odmedia").Color(Color.Cyan1));
            AnsiConsole.MarkupLine("[grey]Media info — viewer & editor | leave empty to quit[/]");

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
                "Cover — Add/Replace",
                "Cover — Remove",
                "Cover — Extract to file",
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
                        AnsiConsole.MarkupLine("[green]All tags cleared (backup .bak created).[/]");
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
                    AnsiConsole.MarkupLine("[green]Cover removed (backup .bak created).[/]");
                }
                catch (Exception ex)
                {
                    ErrorHandler.FailInteractive(ex.Message);
                }
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                try { Console.ReadKey(true); } catch { Console.ReadLine(); }
                continue;
            }

            try
            {
                var edit = await PromptForEditAsync(choice, info, currentFile);
                if (edit is null) continue; // cancelled

                bool confirm = AnsiConsole.Confirm("Save changes?", true);
                if (!confirm) continue;

                service.Save(edit);
                AnsiConsole.MarkupLine("[green]Saved (backup .bak created).[/]");
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
}
