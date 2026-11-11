using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>Specifies the format of an inspection report.</summary>
public enum MediaExportFormat { Json, Csv, Html }

/// <summary>Exports saved inspection data without reopening or modifying the media file.</summary>
public static class MediaExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Returns the file extension for an inspection report, including the leading dot.</summary>
    public static string GetExtension(MediaExportFormat format) => format switch
    {
        MediaExportFormat.Json => ".json",
        MediaExportFormat.Csv => ".csv",
        MediaExportFormat.Html => ".html",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    /// <summary>Writes an inspection report to an existing text writer.</summary>
    public static void Write(TextWriter writer, MediaInfo info, MediaExportFormat format)
    {
        switch (format)
        {
            case MediaExportFormat.Json:
                writer.WriteLine(JsonSerializer.Serialize(info, JsonOptions));
                break;
            case MediaExportFormat.Csv:
                WriteCsv(writer, GetRows(info));
                break;
            case MediaExportFormat.Html:
                WriteHtml(writer, info, GetRows(info));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    /// <summary>Writes a report beside its destination and moves it into place after a successful export.</summary>
    public static void Save(string path, MediaInfo info, MediaExportFormat format, bool overwrite = false)
    {
        var destination = Path.GetFullPath(path);
        var source = Path.GetFullPath(info.FilePath);
        if (OperatingSystem.IsWindows())
        {
            destination = destination.TrimEnd(' ', '.');
            source = source.TrimEnd(' ', '.');
        }
        if (string.Equals(destination, source, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Choose a report path different from the media file.");

        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".odmedia-export-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(temporary, append: false, new UTF8Encoding(format == MediaExportFormat.Csv)))
                Write(writer, info, format);
            File.Move(temporary, destination, overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>Preserves the existing schema of the CLI's info --json option.</summary>
    public static void WriteLegacyInfoJson(TextWriter writer, MediaInfo info)
    {
        var output = new
        {
            file = info.FilePath,
            format = info.Format,
            codec = info.Codec,
            durationSeconds = info.Duration.TotalSeconds,
            bitrate = info.AudioBitrate,
            sampleRate = info.AudioSampleRate,
            channels = info.AudioChannels,
            bitsPerSample = info.AudioBitsPerSample,
            fileSize = info.FileSize,
            title = info.Title,
            artists = info.Artists,
            album = info.Album,
            albumArtists = info.AlbumArtists,
            genres = info.Genres,
            year = info.Year,
            track = info.Track,
            trackCount = info.TrackCount,
            disc = info.Disc,
            discCount = info.DiscCount,
            comment = info.Comment,
            composers = info.Composers,
            copyright = info.Copyright,
            publisher = info.Publisher,
            pictures = info.Pictures,
            details = info.Details
        };
        writer.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
    }

    private static IEnumerable<MediaProperty> GetRows(MediaInfo info)
    {
        MediaProperty[] summary =
        [
            new("Overview", "File", info.FilePath, "File"),
            new("Overview", "Media type", info.Details.Kind, "Reader"),
            new("Overview", "Format", info.Format, "Reader"),
            new("Overview", "Container", info.Details.Container, "Reader"),
            new("Overview", "Duration (s)", info.Duration > TimeSpan.Zero ? Number(info.Duration.TotalSeconds) : null, "Reader"),
            new("Overview", "File size (bytes)", Number(info.FileSize), "File"),
            new("Overview", "Overall bitrate (bit/s)", info.Details.OverallBitrate is > 0 ? Number(info.Details.OverallBitrate) : null, "Reader"),
            new("Overview", "Codec", info.Codec, "Reader"),
            new("Audio", "Audio bitrate (kb/s)", Positive(info.AudioBitrate), "TagLibSharp"),
            new("Audio", "Sample rate (Hz)", Positive(info.AudioSampleRate), "TagLibSharp"),
            new("Audio", "Channels", Positive(info.AudioChannels), "TagLibSharp"),
            new("Audio", "Bits per sample", Positive(info.AudioBitsPerSample ?? 0), "TagLibSharp"),
            new("Metadata", "Title", info.Title, "TagLibSharp"),
            new("Metadata", "Artist(s)", string.Join("; ", info.Artists), "TagLibSharp"),
            new("Metadata", "Album", info.Album, "TagLibSharp"),
            new("Metadata", "Album artist(s)", string.Join("; ", info.AlbumArtists), "TagLibSharp"),
            new("Metadata", "Genre(s)", string.Join("; ", info.Genres), "TagLibSharp"),
            new("Metadata", "Year", Positive(info.Year), "TagLibSharp"),
            new("Metadata", "Track", Positive(info.Track), "TagLibSharp"),
            new("Metadata", "Track count", Positive(info.TrackCount), "TagLibSharp"),
            new("Metadata", "Disc", Positive(info.Disc), "TagLibSharp"),
            new("Metadata", "Disc count", Positive(info.DiscCount), "TagLibSharp"),
            new("Metadata", "Composer(s)", string.Join("; ", info.Composers), "TagLibSharp"),
            new("Metadata", "Comment", info.Comment, "TagLibSharp"),
            new("Metadata", "Copyright", info.Copyright, "TagLibSharp"),
            new("Metadata", "Publisher", info.Publisher, "TagLibSharp")
        ];

        var pictures = info.Pictures.SelectMany((picture, index) =>
        {
            var label = $"Artwork {index + 1}";
            return new MediaProperty[]
            {
                new("Images", $"{label} · Type", picture.Type, "TagLibSharp"),
                new("Images", $"{label} · MIME type", picture.MimeType, "TagLibSharp"),
                new("Images", $"{label} · Width (px)", Positive(picture.Width), "TagLibSharp"),
                new("Images", $"{label} · Height (px)", Positive(picture.Height), "TagLibSharp"),
                new("Images", $"{label} · Size (bytes)", Number(picture.DataLength), "TagLibSharp"),
                new("Images", $"{label} · Description", picture.Description, "TagLibSharp")
            };
        });
        var diagnostics = info.Details.Diagnostics.Select(message => new MediaProperty("Diagnostics", "Analysis notice", message, "Reader"));
        return summary.Concat(info.Details.Properties).Concat(pictures).Concat(diagnostics)
            .Where(row => !string.IsNullOrEmpty(row.Value))
            .Distinct();
    }

    private static string? Number(IFormattable? number) => number?.ToString(null, CultureInfo.InvariantCulture);

    private static string? Positive(long number) => number > 0 ? Number(number) : null;

    private static void WriteCsv(TextWriter writer, IEnumerable<MediaProperty> rows)
    {
        writer.Write("Section,Field,Value,Source\r\n");
        foreach (var row in rows)
        {
            writer.Write(string.Join(',', CsvCell(row.Section), CsvCell(row.Name), CsvCell(row.Value ?? ""), CsvCell(row.Source)));
            writer.Write("\r\n");
        }
    }

    private static string CsvCell(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && (trimmed[0] is '=' or '+' or '-' or '@') &&
            !decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void WriteHtml(TextWriter writer, MediaInfo info, IEnumerable<MediaProperty> rows)
    {
        var sections = rows.GroupBy(row => row.Section)
            .Select((section, index) => (Rows: section, Id: $"section-{index + 1}"))
            .ToArray();
        var name = WebUtility.HtmlEncode(Path.GetFileName(info.FilePath));
        writer.WriteLine($$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{name}} · Media inspection</title>
              <style>
                :root { color-scheme: dark; background: #15171d; color: #eef0f6; font: 16px/1.5 system-ui, sans-serif; }
                body { margin: 0 auto; max-width: 72rem; padding: clamp(1rem, 4vw, 3rem); }
                h1, h2, .path { overflow-wrap: anywhere; }
                h1 { font-size: clamp(1.6rem, 4vw, 2.6rem); margin: 0; }
                h2 { margin: 2rem 0 .6rem; font-size: 1.25rem; }
                .path { color: #b7bece; margin: .3rem 0 1.25rem; }
                nav { display: flex; flex-wrap: wrap; gap: .55rem; }
                nav a { color: #a5ddf3; border: 1px solid #424958; padding: .28rem .65rem; border-radius: .35rem; }
                table { width: 100%; table-layout: fixed; border-collapse: collapse; background: #20232c; }
                th, td { text-align: left; vertical-align: top; padding: .55rem .7rem; border-bottom: 1px solid #3a404e; overflow-wrap: anywhere; }
                thead { background: #2a2e39; }
                tbody th { font-weight: 600; }
                tbody tr:nth-child(even) { background: #1a1d25; }
                .value { white-space: pre-wrap; }
                .source { color: #b7bece; }
                @media (max-width: 36rem) { th, td { padding: .4rem; } }
              </style>
            </head>
            <body>
              <header>
                <h1>{{name}}</h1>
                <p class="path">{{WebUtility.HtmlEncode(info.FilePath)}}</p>
              </header>
              <nav aria-label="Report sections">
            """);
        foreach (var (section, id) in sections)
            writer.WriteLine($"    <a href=\"#{id}\">{WebUtility.HtmlEncode(section.Key)}</a>");
        writer.WriteLine("  </nav>\n  <main>");
        foreach (var (section, id) in sections)
        {
            writer.WriteLine($"    <section aria-labelledby=\"{id}\">\n      <h2 id=\"{id}\">{WebUtility.HtmlEncode(section.Key)}</h2>");
            writer.WriteLine("""
                      <table>
                        <colgroup><col style="width: 31%"><col style="width: 52%"><col></colgroup>
                        <thead><tr><th scope="col">Field</th><th scope="col">Value</th><th scope="col">Source</th></tr></thead>
                        <tbody>
                """);
            foreach (var row in section)
                writer.WriteLine($"          <tr><th scope=\"row\">{WebUtility.HtmlEncode(row.Name)}</th><td class=\"value\">{WebUtility.HtmlEncode(row.Value)}</td><td class=\"source\">{WebUtility.HtmlEncode(row.Source)}</td></tr>");
            writer.WriteLine("        </tbody>\n      </table>\n    </section>");
        }
        writer.WriteLine("  </main>\n</body>\n</html>");
    }
}
