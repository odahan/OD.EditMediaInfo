using System.Text.Json.Serialization;

namespace OD.EditMediaInfo.Models;

public sealed class PresetFile
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("sourceFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFile { get; set; }

    [JsonPropertyName("tags")]
    public PresetTags Tags { get; set; } = new();

    [JsonPropertyName("cover")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PresetCover? Cover { get; set; }
}

public sealed class PresetTags
{
    [JsonPropertyName("artists")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Artists { get; set; }

    [JsonPropertyName("album")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Album { get; set; }

    [JsonPropertyName("albumArtists")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? AlbumArtists { get; set; }

    [JsonPropertyName("genres")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Genres { get; set; }

    [JsonPropertyName("year")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? Year { get; set; }

    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }

    [JsonPropertyName("composers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Composers { get; set; }

    [JsonPropertyName("copyright")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Copyright { get; set; }

    [JsonPropertyName("publisher")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Publisher { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty =>
        Artists is null && Album is null && AlbumArtists is null && Genres is null &&
        Year is null && Comment is null && Composers is null &&
        Copyright is null && Publisher is null;
}

public sealed class PresetCover
{
    [JsonPropertyName("mime")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dataBase64")]
    public string DataBase64 { get; set; } = string.Empty;
}
