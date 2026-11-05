using System.Text.Json;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

/// <summary>Implements the command that displays media information.</summary>
public static class InfoCommand
{
    /// <summary>Executes the info command with parsed arguments.</summary>
    public static Task<int> RunAsync(string file, bool json, bool analyzeCadence = false)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia info <file> [--json]");
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
            var service = new MediaTagService();
            var info = service.Read(file);
            if (analyzeCadence) FFprobeService.AnalyzeCadence(file, info.Details);

            if (json)
            {
                var obj = new
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
                Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                MediaProbeService.Render(info);
            }

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            ErrorHandler.Fail(ex.Message, 2);
            return Task.FromResult(2);
        }
    }

    /// <summary>Writes the info command help text.</summary>
    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia info <file> [--json]");
        AnsiConsole.WriteLine("  Show media technical info and tags.");
        AnsiConsole.WriteLine("  --json  Output JSON instead of tables");
    }
}
