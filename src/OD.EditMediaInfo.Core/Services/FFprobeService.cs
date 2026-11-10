using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>Inspects local media tracks using an optional, locally installed FFprobe executable.</summary>
public static class FFprobeService
{
    /// <summary>Locates an explicitly configured analyzer, a bundled copy, or a PATH installation.</summary>
    public static string? FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("ODMEDIA_FFPROBE");
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? Path.GetFullPath(configured) : null;
        var bundled = Path.Combine(MediaToolsService.ExecutableDirectory, "ffprobe.exe");
        if (File.Exists(bundled)) return bundled;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), "ffprobe.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    /// <summary>Reads a local file with a bounded execution time and reports partial-analysis failures.</summary>
    public static bool Read(string path, MediaDetails details)
    {
        var executable = FindExecutable();
        if (executable is null)
        {
            details.Diagnostics.Add("Extended analysis unavailable. Use Actions > Install media analyzer, or odmedia tools install.");
            return false;
        }
        try
        {
            ReadAsync(executable, path, details).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException or OperationCanceledException)
        {
            details.Diagnostics.Add($"Extended analysis failed: {ex.Message}");
            return false;
        }
    }

    private static async Task ReadAsync(string executable, string path, MediaDetails details)
    {
        var arguments = new[] { "-v", "error", "-protocol_whitelist", "file", "-show_format", "-show_streams", "-show_chapters", "-of", "json", "-i", Path.GetFullPath(path) };
        ApplyJson(await ExecuteAsync(executable, arguments), details);
    }

    /// <summary>Samples actual timestamps from the first video track without claiming a full-file cadence classification.</summary>
    public static void AnalyzeCadence(string path, MediaDetails details)
    {
        var executable = FindExecutable() ?? throw new InvalidOperationException("Install the media analyzer first.");
        var stream = details.Streams.FirstOrDefault(s => s.Type == "video") ?? throw new InvalidOperationException("No video track is available for cadence analysis.");
        var arguments = new[] { "-v", "error", "-protocol_whitelist", "file", "-select_streams", stream.Index.ToString(CultureInfo.InvariantCulture),
            "-read_intervals", "%+#300", "-show_frames", "-show_entries", "frame=best_effort_timestamp_time", "-of", "json", "-i", Path.GetFullPath(path) };
        ApplyCadenceJson(ExecuteAsync(executable, arguments).GetAwaiter().GetResult(), stream.Index, details);
    }

    /// <summary>Reports observed timing variation, explicitly limiting conclusions to the sampled frames.</summary>
    public static void ApplyCadenceJson(string json, int streamIndex, MediaDetails details)
    {
        using var document = JsonDocument.Parse(json);
        var times = document.RootElement.TryGetProperty("frames", out var frames)
            ? frames.EnumerateArray().Select(f => Decimal(f, "best_effort_timestamp_time")).Where(t => t.HasValue).Select(t => t!.Value).ToArray() : [];
        var differences = times.Zip(times.Skip(1), (before, after) => after - before).ToArray();
        string result;
        if (differences.Length < 2 || differences.Any(d => d <= 0)) result = "Unknown: insufficient or non-monotonic timestamps in sample.";
        else
        {
            var median = differences.Order().ElementAt(differences.Length / 2);
            var variable = differences.Any(d => Math.Abs(d - median) > Math.Max(0.00001, median * 0.001));
            result = variable ? $"Variable frame timing observed ({times.Length} sampled frames)."
                : $"Uniform frame timing in {times.Length} sampled frames; full-file cadence remains unverified.";
        }
        var name = $"Track {streamIndex} · Cadence sample";
        details.Properties.RemoveAll(p => p.Name == name || p.Name == $"Track {streamIndex} · Cadence mode");
        details.Properties.Add(new("Video", name, result, "FFprobe · first 300 video packets, up to 300 frames"));
    }

    private static async Task<string> ExecuteAsync(string executable, IEnumerable<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var errors = ReadBoundedAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0) throw new InvalidOperationException((await errors).Trim());
            return await output;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var result = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int length;
        while ((length = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (result.Length + length > 8 * 1024 * 1024) throw new IOException("Analyzer output exceeds 8 MB.");
            result.Append(buffer, 0, length);
        }
        return result.ToString();
    }

    /// <summary>Maps a probe document without counting cover art as a video track or assuming a constant cadence.</summary>
    public static void ApplyJson(string json, MediaDetails details)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        details.ProbeData = root.Clone();
        if (root.TryGetProperty("format", out var format))
        {
            details.Container = Text(format, "format_long_name") ?? Text(format, "format_name");
            var overallBitrate = Long(format, "bit_rate");
            details.OverallBitrate = overallBitrate > 0 ? overallBitrate : null;
            Add(details, "Overview", "Container", details.Container);
            Add(details, "Overview", "Overall bitrate", details.OverallBitrate is > 0 ? $"{details.OverallBitrate / 1_000_000.0:0.###} Mb/s" : null);
            Flatten(format, "General", details, "Container");
        }
        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var type = Text(stream, "codec_type") ?? "unknown";
                var index = (int)(Long(stream, "index") ?? details.Streams.Count);
                var attached = stream.TryGetProperty("disposition", out var disposition) && Long(disposition, "attached_pic") == 1;
                details.Streams.Add(new(index, attached ? "artwork" : type, stream.Clone()));
                var section = attached ? "Images" : type switch
                {
                    "video" => "Video",
                    "audio" => "Audio",
                    "subtitle" => "Subtitles",
                    _ => "Advanced"
                };
                var prefix = $"Track {index}";
                foreach (var pair in Labels)
                {
                    var value = Text(stream, pair.Key);
                    if (pair.Key is "bits_per_sample" or "bits_per_raw_sample" && value == "0") continue;
                    Add(details, section, $"{prefix} · {pair.Value}", value);
                }
                if (stream.TryGetProperty("tags", out var trackTags))
                {
                    Add(details, section, $"{prefix} · Language", Text(trackTags, "language"));
                    Add(details, section, $"{prefix} · Title", Text(trackTags, "title"));
                    Add(details, section, $"{prefix} · Rotation (degrees)", Text(trackTags, "rotate"));
                }
                if (stream.TryGetProperty("disposition", out var flags))
                {
                    Add(details, section, $"{prefix} · Default", Long(flags, "default") == 1 ? "Yes" : "No");
                    Add(details, section, $"{prefix} · Forced", Long(flags, "forced") == 1 ? "Yes" : "No");
                }
                if (type == "audio")
                {
                    Add(details, "Overview", $"{prefix} · Audio codec", Text(stream, "codec_long_name") ?? Text(stream, "codec_name"));
                    Add(details, "Overview", $"{prefix} · Audio sample rate (Hz)", Text(stream, "sample_rate"));
                    Add(details, "Overview", $"{prefix} · Audio channels", Text(stream, "channel_layout") ?? Text(stream, "channels"));
                    var codecName = Text(stream, "codec_name") ?? "";
                    if (codecName is "flac" or "alac" or "ape" or "truehd" || codecName.StartsWith("pcm_", StringComparison.Ordinal))
                        Add(details, section, $"{prefix} · Compression", "Lossless codec (does not establish source quality)");
                }
                if (type == "video" && !attached)
                {
                    var width = Long(stream, "width");
                    var height = Long(stream, "height");
                    Add(details, "Overview", $"{prefix} · Resolution", width > 0 && height > 0 ? $"{width} × {height} pixels" : null);
                    Add(details, "Overview", $"{prefix} · Video codec", Text(stream, "codec_long_name") ?? Text(stream, "codec_name"));
                    Add(details, "Overview", $"{prefix} · Average frame rate", FrameRate(Text(stream, "avg_frame_rate")));
                    Add(details, "Video", $"{prefix} · Nominal frame rate", FrameRate(Text(stream, "r_frame_rate")));
                    Add(details, "Video", $"{prefix} · Cadence mode", "Unknown (requires frame timing analysis)");
                    var transfer = Text(stream, "color_transfer");
                    if (transfer is "smpte2084" or "arib-std-b67")
                        Add(details, "Overview", $"{prefix} · Dynamic range", transfer == "smpte2084" ? "HDR · PQ transfer" : "HDR · HLG transfer");
                    if (stream.TryGetProperty("side_data_list", out var sideData))
                        foreach (var data in sideData.EnumerateArray())
                        {
                            Add(details, "Video", $"{prefix} · Rotation (degrees)", Text(data, "rotation"));
                            var sideType = Text(data, "side_data_type");
                            if (sideType?.Contains("DOVI", StringComparison.OrdinalIgnoreCase) == true)
                                Add(details, "Overview", $"{prefix} · HDR metadata", "Dolby Vision configuration present");
                        }
                }
                Flatten(stream, "Advanced", details, prefix);
            }
            if (details.Kind != "Photo")
                details.Kind = details.Streams.Any(s => s.Type == "video") ? "Video" : details.Streams.Any(s => s.Type == "audio") ? "Audio" : "Unknown";
        }
        if (root.TryGetProperty("chapters", out var chapters))
        {
            foreach (var chapter in chapters.EnumerateArray())
            {
                var title = chapter.TryGetProperty("tags", out var tags) ? Text(tags, "title") : null;
                details.Chapters.Add(new(Decimal(chapter, "start_time"), Decimal(chapter, "end_time"), title));
                Flatten(chapter, "Chapters", details, $"Chapter {details.Chapters.Count}");
            }
        }
    }

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["codec_long_name"] = "Codec",
        ["profile"] = "Profile",
        ["level"] = "Level (codec-specific)",
        ["bit_rate"] = "Bitrate (bit/s)",
        ["duration"] = "Duration (s)",
        ["start_time"] = "Start time (s)",
        ["sample_rate"] = "Sample rate (Hz)",
        ["channels"] = "Channels",
        ["channel_layout"] = "Channel layout",
        ["bits_per_sample"] = "Bits per sample",
        ["bits_per_raw_sample"] = "Raw bits per sample",
        ["pix_fmt"] = "Pixel format",
        ["display_aspect_ratio"] = "Display aspect ratio",
        ["sample_aspect_ratio"] = "Pixel aspect ratio",
        ["field_order"] = "Scan / field order",
        ["nb_frames"] = "Frame count (declared)",
        ["color_range"] = "Color range",
        ["color_space"] = "Color matrix",
        ["color_transfer"] = "Transfer function",
        ["color_primaries"] = "Color primaries"
    };

    private static void Flatten(JsonElement element, string section, MediaDetails details, string prefix)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject()) Flatten(property.Value, section, details, $"{prefix} · {property.Name}");
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray()) Flatten(item, section, details, $"{prefix} [{index++}]");
        }
        else Add(details, section, prefix, element.ValueKind == JsonValueKind.Null ? null : element.ToString());
    }

    private static void Add(MediaDetails details, string section, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != "N/A") details.Properties.Add(new(section, name, value, "FFprobe"));
    }

    /// <summary>Formats a rational frame rate without losing its exact representation.</summary>
    public static string? FrameRate(string? value)
    {
        if (value is null) return null;
        var parts = value.Split('/');
        if (parts.Length > 2) return null;
        if (!double.TryParse(parts[0], CultureInfo.InvariantCulture, out var numerator)) return null;
        double denominator = 1;
        if (parts.Length > 1 && !double.TryParse(parts[1], CultureInfo.InvariantCulture, out denominator)) return null;
        return double.IsFinite(numerator) && double.IsFinite(denominator) && numerator > 0 && denominator > 0 ? $"{(numerator / denominator).ToString("0.###", CultureInfo.InvariantCulture)} fps ({value})" : null;
    }

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static long? Long(JsonElement element, string name) => long.TryParse(Text(element, name), CultureInfo.InvariantCulture, out var value) ? value : null;
    private static double? Decimal(JsonElement element, string name) => double.TryParse(Text(element, name), CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
}
