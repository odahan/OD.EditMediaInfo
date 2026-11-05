using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>Combines native tags, photographic metadata, and optional stream inspection.</summary>
public static class MediaInspectionService
{
    /// <summary>Reads all available information while preserving partial results and diagnostics.</summary>
    public static MediaInfo Read(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Media file not found.", path);
        var details = new MediaDetails();
        MediaInfo? info = null;
        try
        {
            info = new MediaTagService().ReadTags(path);
            using var file = TagLib.File.Create(path);
            var props = file.Properties;
            details.Kind = props.MediaTypes.HasFlag(TagLib.MediaTypes.Photo) ? "Photo"
                : props.MediaTypes.HasFlag(TagLib.MediaTypes.Video) ? "Video" : "Audio";
            details.CanWritePhoto = file.Tag is TagLib.Image.ImageTag &&
                Path.GetExtension(path).ToLowerInvariant() is (".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff");
            details.CanWriteTags = file.Tag is not TagLib.Image.ImageTag || details.CanWritePhoto;
            foreach (var codec in props.Codecs)
            {
                if (codec.MediaTypes.HasFlag(TagLib.MediaTypes.Audio))
                    details.Properties.Add(new("Audio", "Codec", codec.Description, "TagLibSharp"));
                if (codec.MediaTypes.HasFlag(TagLib.MediaTypes.Video))
                    details.Properties.Add(new("Video", "Codec", codec.Description, "TagLibSharp"));
            }
            if (props.VideoWidth > 0 && props.VideoHeight > 0)
                details.Properties.Add(new("Overview", "Resolution", $"{props.VideoWidth} × {props.VideoHeight} pixels", "TagLibSharp"));
            foreach (var field in TagFieldCatalog.GetFields(file.Tag))
                details.Properties.Add(new(field.Section, field.Label, field.Read(file.Tag), "TagLibSharp"));
        }
        catch (InvalidOperationException ex) { details.Diagnostics.Add(ex.Message); }
        catch (TagLib.UnsupportedFormatException ex) { details.Diagnostics.Add(ex.Message); }
        catch (TagLib.CorruptFileException ex) { details.Diagnostics.Add(ex.Message); }

        var photo = PhotoMetadataService.IsPhoto(path) || details.Kind == "Photo";
        bool additionalRead;
        if (photo)
        {
            details.Kind = "Photo";
            additionalRead = PhotoMetadataService.Read(path, details);
            if (!additionalRead) additionalRead = FFprobeService.Read(path, details);
        }
        else additionalRead = FFprobeService.Read(path, details);
        if (info is null && !additionalRead)
            throw new InvalidOperationException(string.Join(Environment.NewLine, details.Diagnostics));
        info ??= new MediaInfo(path, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), "", TimeSpan.Zero,
            0, 0, 0, null, new FileInfo(path).Length, "", [], "", [], [], 0, 0, 0, 0, 0, "", [], "", "", []);
        if (details.ProbeData is { } probe && probe.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var duration) && double.TryParse(duration.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds) && seconds >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds)
            info = info with { Duration = TimeSpan.FromSeconds(seconds) };
        details.Container ??= Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        if (details.OverallBitrate is null && info.Duration.TotalSeconds > 0)
            details.Properties.Add(new("General", "Overall bitrate (estimated)", $"{info.FileSize * 8 / info.Duration.TotalSeconds / 1_000_000:0.###} Mb/s", "Estimated from file size / duration, including container overhead"));
        return info with { Details = details };
    }
}
