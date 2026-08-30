using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

/// <summary>Implements the command that updates media tags.</summary>
public static class SetCommand
{
    /// <summary>Executes the set command with parsed arguments.</summary>
    public static Task<int> RunAsync(string file, string? title, string? artist, string? album, string? albumArtist, string? genre,
        uint? year, uint? track, uint? trackCount, uint? disc, uint? discCount, string? comment, string? composer,
        string? copyright, string? publisher, string? cover, string? coverType, bool removeCover, string? coverDesc,
        bool noBackup, bool keepMtime, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia set <file> [options]");
            return Task.FromResult(1);
        }

        file = file.Trim();
        if (!File.Exists(file))
        {
            ErrorHandler.Fail($"File not found: \"{file}\"");
            return Task.FromResult(1);
        }

        var artists = artist is not null ? ArgHelpers.Split(artist) : null;
        var albumArtists = albumArtist is not null ? ArgHelpers.Split(albumArtist) : null;
        var genres = genre is not null ? ArgHelpers.Split(genre) : null;
        var composers = composer is not null ? ArgHelpers.Split(composer) : null;
        var coverTypeEnum = coverType?.ToLowerInvariant() switch
        {
            "back" => CoverType.Back,
            "other" => CoverType.Other,
            _ => CoverType.Front
        };

        var opts = new EditOptions
        {
            FilePath = file,
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
            CoverType = coverTypeEnum,
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

        try
        {
            var service = new MediaTagService();
            if (dryRun)
            {
                service.Validate(opts);
                AnsiConsole.MarkupLine("[yellow]Dry-run:[/] would apply to: " + Markup.Escape(file));
                DumpOptions(opts);
                return Task.FromResult(0);
            }
            service.Save(opts);
            AnsiConsole.MarkupLine($"[green]Saved:[/] {Markup.Escape(file)}" + (noBackup ? "" : " [grey](backup .bak available)[/]"));
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

    /// <summary>Writes the set command help text.</summary>
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
        AnsiConsole.WriteLine("  --cover-type <type>      Cover type: front, back, other (default front)");
        AnsiConsole.WriteLine("  --remove-cover           Remove all covers");
        AnsiConsole.WriteLine("  --cover-desc <text>      Cover description");
        AnsiConsole.WriteLine("  --backup / --no-backup   Create .bak backup (default backup)");
        AnsiConsole.WriteLine("  --keep-mtime             Preserve file modification time");
        AnsiConsole.WriteLine("  --dry-run                Show what would be done");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Examples:");
        AnsiConsole.WriteLine("  odmedia set track.mp3 --title \"New Title\" --artist \"Artist\"");
        AnsiConsole.WriteLine("  odmedia set track.mp3 --genre \"Rock;Pop\" --year 2024");
        AnsiConsole.WriteLine("  odmedia set track.mp3 --cover cover.jpg --cover-type back");
    }
}