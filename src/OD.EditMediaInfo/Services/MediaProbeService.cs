using OD.EditMediaInfo.Models;
using Spectre.Console;

namespace OD.EditMediaInfo.Services;

/// <summary>Renders media information in terminal tables.</summary>
public static class MediaProbeService
{
    /// <summary>Renders technical details, tags, and cover summaries.</summary>
    public static void Render(MediaInfo info)
    {
        // Technical table
        var tech = new Table().Border(TableBorder.Rounded).Title("[cyan]Technical[/]");
        tech.AddColumn("Property");
        tech.AddColumn("Value");
        tech.AddRow("File", Markup.Escape(info.FilePath));
        tech.AddRow("Format", Markup.Escape(info.Format));
        tech.AddRow("Codec", Markup.Escape(string.IsNullOrWhiteSpace(info.Codec) ? "-" : info.Codec));
        tech.AddRow("Duration", info.Duration.TotalSeconds > 0 ? $"{info.Duration:hh\\:mm\\:ss\\.ff} ({info.Duration.TotalSeconds:F1}s)" : "-");
        tech.AddRow("Bitrate", info.AudioBitrate > 0 ? $"{info.AudioBitrate} kb/s" : "-");
        tech.AddRow("Sample Rate", info.AudioSampleRate > 0 ? $"{info.AudioSampleRate} Hz" : "-");
        tech.AddRow("Channels", info.AudioChannels > 0 ? info.AudioChannels.ToString() : "-");
        if (info.AudioBitsPerSample.HasValue)
            tech.AddRow("Bits/Sample", info.AudioBitsPerSample.Value.ToString());
        tech.AddRow("File Size", $"{info.FileSize / 1024.0:F1} KB ({info.FileSize} bytes)");
        AnsiConsole.Write(tech);

        // Tags table
        var tags = new Table().Border(TableBorder.Rounded).Title("[yellow]Tags[/]");
        tags.AddColumn("Field");
        tags.AddColumn("Value");
        tags.AddRow("Title", Markup.Escape(string.IsNullOrWhiteSpace(info.Title) ? "-" : info.Title));
        tags.AddRow("Artist", Markup.Escape(info.Artists.Length == 0 ? "-" : string.Join("; ", info.Artists)));
        tags.AddRow("Album", Markup.Escape(string.IsNullOrWhiteSpace(info.Album) ? "-" : info.Album));
        tags.AddRow("Album Artist", Markup.Escape(info.AlbumArtists.Length == 0 ? "-" : string.Join("; ", info.AlbumArtists)));
        tags.AddRow("Genre", Markup.Escape(info.Genres.Length == 0 ? "-" : string.Join("; ", info.Genres)));
        tags.AddRow("Year", info.Year == 0 ? "-" : info.Year.ToString());
        tags.AddRow("Track", info.Track == 0 ? "-" : $"{info.Track}" + (info.TrackCount > 0 ? $"/{info.TrackCount}" : ""));
        tags.AddRow("Disc", info.Disc == 0 ? "-" : $"{info.Disc}" + (info.DiscCount > 0 ? $"/{info.DiscCount}" : ""));
        tags.AddRow("Composer", Markup.Escape(info.Composers.Length == 0 ? "-" : string.Join("; ", info.Composers)));
        tags.AddRow("Comment", Markup.Escape(string.IsNullOrWhiteSpace(info.Comment) ? "-" : info.Comment));
        tags.AddRow("Copyright", Markup.Escape(string.IsNullOrWhiteSpace(info.Copyright) ? "-" : info.Copyright));
        tags.AddRow("Publisher", Markup.Escape(string.IsNullOrWhiteSpace(info.Publisher) ? "-" : info.Publisher));
        AnsiConsole.Write(tags);

        // Cover summary
        if (info.Pictures.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Cover: none[/]");
        }
        else
        {
            var coverTable = new Table().Border(TableBorder.Rounded).Title("[green]Covers[/]");
            coverTable.AddColumn("#");
            coverTable.AddColumn("Mime");
            coverTable.AddColumn("Size");
            coverTable.AddColumn("Type");
            coverTable.AddColumn("Desc");
            coverTable.AddColumn("Dimensions");
            for (int i = 0; i < info.Pictures.Count; i++)
            {
                var p = info.Pictures[i];
                var sizeKb = p.DataLength / 1024.0;
                var sizeStr = sizeKb > 1024 ? $"{sizeKb / 1024:F1} MB" : $"{sizeKb:F0} KB";
                var dim = p.Width > 0 && p.Height > 0 ? $"{p.Width}x{p.Height}" : "-";
                coverTable.AddRow((i + 1).ToString(), Markup.Escape(p.MimeType), sizeStr, Markup.Escape(p.Type), Markup.Escape(string.IsNullOrWhiteSpace(p.Description) ? "-" : p.Description), dim);
            }
            AnsiConsole.Write(coverTable);
        }
    }
}
