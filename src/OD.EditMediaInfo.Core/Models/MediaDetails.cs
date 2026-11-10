using System.Text.Json;

namespace OD.EditMediaInfo.Models;

/// <summary>Describes a readable property and its origin without inventing missing values.</summary>
public sealed record MediaProperty(string Section, string Name, string? Value, string Source);

/// <summary>Preserves the complete probe result for a single track, including side data and tags.</summary>
public sealed record MediaStreamInfo(int Index, string Type, JsonElement Data);

/// <summary>Describes a chapter using seconds relative to the media timeline.</summary>
public sealed record ChapterInfo(double? StartSeconds, double? EndSeconds, string? Title);

/// <summary>Contains extended technical metadata and the capabilities of the selected readers.</summary>
public sealed class MediaDetails
{
    public string Kind { get; set; } = "Unknown";
    public string? Container { get; set; }
    public long? OverallBitrate { get; set; }
    public bool CanWriteTags { get; set; }
    public bool CanWritePhoto { get; set; }
    public List<MediaProperty> Properties { get; } = [];
    public List<MediaStreamInfo> Streams { get; } = [];
    public List<ChapterInfo> Chapters { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public JsonElement? ProbeData { get; set; }
}
