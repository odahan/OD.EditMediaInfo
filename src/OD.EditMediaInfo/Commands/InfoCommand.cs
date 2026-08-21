using System.Text.Json;
using OD.EditMediaInfo.Services;
using Spectre.Console;

namespace OD.EditMediaInfo.Commands;

public static class InfoCommand
{
    public static Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        string? input = null;
        bool json = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--json": json = true; break;
                case "--help": case "-h": PrintHelp(); return Task.FromResult(0);
                default:
                    if (a.StartsWith("-")) { ErrorHandler.Fail($"Unknown option: \"{a}\""); return Task.FromResult(1); }
                    if (input is null) input = a;
                    else { ErrorHandler.Fail("Too many arguments"); return Task.FromResult(1); }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            ErrorHandler.Fail("Missing file. Usage: odmedia info <file> [--json]");
            return Task.FromResult(1);
        }

        input = input.Trim().Trim('"');
        if (!File.Exists(input))
        {
            ErrorHandler.Fail($"File not found: \"{input}\"");
            return Task.FromResult(1);
        }

        try
        {
            var service = new MediaTagService();
            var info = service.Read(input);

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
                    pictures = info.Pictures
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

    public static void PrintHelp()
    {
        AnsiConsole.WriteLine("Usage: odmedia info <file> [--json]");
        AnsiConsole.WriteLine("  Show media technical info and tags.");
        AnsiConsole.WriteLine("  --json  Output JSON instead of tables");
    }
}
