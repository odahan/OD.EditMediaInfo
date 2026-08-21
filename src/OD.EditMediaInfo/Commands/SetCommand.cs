using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

public static class SetCommand
{
    public static Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        string? input = null;
        string? title = null;
        string[]? artists = null;
        string? album = null;
        string[]? albumArtists = null;
        string[]? genres = null;
        uint? year = null;
        uint? track = null;
        uint? trackCount = null;
        uint? disc = null;
        uint? discCount = null;
        string? comment = null;
        string[]? composers = null;
        string? copyright = null;
        string? publisher = null;
        string? cover = null;
        bool removeCover = false;
        string? coverDesc = null;
        bool noBackup = false;
        bool keepMtime = false;
        bool dryRun = false;

        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--title": title = Next(args, ref i, "--title"); break;
                case "--artist": artists = Split(Next(args, ref i, "--artist")); break;
                case "--album": album = Next(args, ref i, "--album"); break;
                case "--album-artist": albumArtists = Split(Next(args, ref i, "--album-artist")); break;
                case "--genre": genres = Split(Next(args, ref i, "--genre")); break;
                case "--year": year = uint.Parse(Next(args, ref i, "--year")); break;
                case "--track": track = uint.Parse(Next(args, ref i, "--track")); break;
                case "--track-count": trackCount = uint.Parse(Next(args, ref i, "--track-count")); break;
                case "--disc": disc = uint.Parse(Next(args, ref i, "--disc")); break;
                case "--disc-count": discCount = uint.Parse(Next(args, ref i, "--disc-count")); break;
                case "--comment": comment = Next(args, ref i, "--comment"); break;
                case "--composer": composers = Split(Next(args, ref i, "--composer")); break;
                case "--copyright": copyright = Next(args, ref i, "--copyright"); break;
                case "--publisher": publisher = Next(args, ref i, "--publisher"); break;
                case "--cover": cover = Next(args, ref i, "--cover"); break;
                case "--remove-cover": removeCover = true; break;
                case "--cover-desc": coverDesc = Next(args, ref i, "--cover-desc"); break;
                case "--no-backup": noBackup = true; break;
                case "--backup": noBackup = false; break;
                case "--keep-mtime": keepMtime = true; break;
                case "--dry-run": dryRun = true; break;
                case "--help": case "-h": PrintHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    positional.Add(a);
                    break;
            }
        }

        if (positional.Count == 0)
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia set <file> [options]");
            return Task.FromResult(1);
        }
        if (positional.Count > 1)
        {
            ErrorHandler.Fail("Too many files — only one file at a time");
            return Task.FromResult(1);
        }

        input = positional[0].Trim().Trim('"');
        if (!File.Exists(input))
        {
            ErrorHandler.Fail($"File not found: \"{input}\"");
            return Task.FromResult(1);
        }

        var opts = new EditOptions
        {
            FilePath = input,
            Title = title,
            Artists = artists,
            Album = album,
            AlbumArtists = albumArtists,
            Genres = genres,
            Year = year,
            Track = track,
            TrackCount = trackCount,
            Disc = disc,
            DiscCount = discCount,
            Comment = comment,
            Composers = composers,
            Copyright = copyright,
            Publisher = publisher,
            CoverPath = cover,
            RemoveCover = removeCover,
            CoverDescription = coverDesc,
            NoBackup = noBackup,
            KeepMtime = keepMtime,
            DryRun = dryRun
        };

        if (!opts.HasAnyTagChange)
        {
            ErrorHandler.Fail("No tag to set. Use --title, --artist, --cover, etc. See odmedia set --help");
            return Task.FromResult(1);
        }

        if (dryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry-run:[/] would apply to: " + Markup.Escape(input));
            DumpOptions(opts);
            return Task.FromResult(0);
        }

        try
        {
            var service = new MediaTagService();
            service.Save(opts);
            AnsiConsole.MarkupLine($"[green]Saved:[/] {Markup.Escape(input)}" + (noBackup ? "" : " [grey](backup .bak created)[/]"));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    private static void DumpOptions(EditOptions o)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("New Value");
        if (o.Title is not null) table.AddRow("Title", Markup.Escape(o.Title));
        if (o.Artists is not null) table.AddRow("Artist", Markup.Escape(string.Join("; ", o.Artists)));
        if (o.Album is not null) table.AddRow("Album", Markup.Escape(o.Album));
        if (o.AlbumArtists is not null) table.AddRow("Album Artist", Markup.Escape(string.Join("; ", o.AlbumArtists)));
        if (o.Genres is not null) table.AddRow("Genre", Markup.Escape(string.Join("; ", o.Genres)));
        if (o.Year.HasValue) table.AddRow("Year", o.Year.Value.ToString());
        if (o.Track.HasValue) table.AddRow("Track", o.Track.Value.ToString());
        if (o.TrackCount.HasValue) table.AddRow("TrackCount", o.TrackCount.Value.ToString());
        if (o.Disc.HasValue) table.AddRow("Disc", o.Disc.Value.ToString());
        if (o.DiscCount.HasValue) table.AddRow("DiscCount", o.DiscCount.Value.ToString());
        if (o.Comment is not null) table.AddRow("Comment", Markup.Escape(o.Comment));
        if (o.Composers is not null) table.AddRow("Composer", Markup.Escape(string.Join("; ", o.Composers)));
        if (o.Copyright is not null) table.AddRow("Copyright", Markup.Escape(o.Copyright));
        if (o.Publisher is not null) table.AddRow("Publisher", Markup.Escape(o.Publisher));
        if (o.CoverPath is not null) table.AddRow("Cover", Markup.Escape(o.CoverPath));
        if (o.RemoveCover) table.AddRow("Cover", "[red]remove[/]");
        AnsiConsole.Write(table);
    }

    private static string[] Split(string s) => s.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string Next(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {name}");
        i++;
        return args[i];
    }

    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia set <file> [options]");
        AnsiConsole.WriteLine("  Edit media tags. At least one tag option required.");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Options:");
        AnsiConsole.WriteLine("  --title <text>           Title");
        AnsiConsole.WriteLine("  --artist <a;b>           Artist(s), ; separated");
        AnsiConsole.WriteLine("  --album <text>           Album");
        AnsiConsole.WriteLine("  --album-artist <a;b>     Album artist(s)");
        AnsiConsole.WriteLine("  --genre <g1;g2>          Genre(s)");
        AnsiConsole.WriteLine("  --year <n>               Year");
        AnsiConsole.WriteLine("  --track <n>              Track number");
        AnsiConsole.WriteLine("  --track-count <n>        Total tracks");
        AnsiConsole.WriteLine("  --disc <n>               Disc number");
        AnsiConsole.WriteLine("  --disc-count <n>         Total discs");
        AnsiConsole.WriteLine("  --comment <text>         Comment");
        AnsiConsole.WriteLine("  --composer <c1;c2>       Composer(s)");
        AnsiConsole.WriteLine("  --copyright <text>       Copyright");
        AnsiConsole.WriteLine("  --publisher <text>       Publisher / Label");
        AnsiConsole.WriteLine("  --cover <path>           Cover image (jpg/png, max 10 MB)");
        AnsiConsole.WriteLine("  --remove-cover           Remove all covers");
        AnsiConsole.WriteLine("  --cover-desc <text>      Cover description");
        AnsiConsole.WriteLine("  --backup / --no-backup   Create .bak backup (default backup)");
        AnsiConsole.WriteLine("  --keep-mtime             Preserve file modification time");
        AnsiConsole.WriteLine("  --dry-run                Show what would be done");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Examples:");
        AnsiConsole.WriteLine("  odmedia set track.mp3 --title \"New Title\" --artist \"Artist\"");
        AnsiConsole.WriteLine("  odmedia set track.mp3 --genre \"Rock;Pop\" --year 2024");
        AnsiConsole.WriteLine("  odmedia set track.mp3 --cover cover.jpg");
    }
}
