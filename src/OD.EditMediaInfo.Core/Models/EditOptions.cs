namespace OD.EditMediaInfo.Models;

public enum CoverType { Front, Back, Other }

/// <summary>Represents requested changes to a media file's tags and cover.</summary>
public sealed class EditOptions
{
    public string FilePath { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string[]? Artists { get; init; }
    public string? Album { get; init; }
    public string[]? AlbumArtists { get; init; }
    public string[]? Genres { get; init; }
    public uint? Year { get; init; }
    public uint? Track { get; init; }
    public uint? TrackCount { get; init; }
    public uint? Disc { get; init; }
    public uint? DiscCount { get; init; }
    public string? Comment { get; init; }
    public string[]? Composers { get; init; }
    public string? Copyright { get; init; }
    public string? Publisher { get; init; }
    public string? CoverPath { get; init; }
    public CoverType? CoverType { get; init; }
    public bool RemoveCover { get; init; }
    public string? CoverDescription { get; init; }
    public bool NoBackup { get; init; }
    public bool KeepMtime { get; init; }
    public bool DryRun { get; init; }
    /// <summary>Gets explicit replacements; null values request removal, absent keys preserve a field.</summary>
    public IReadOnlyDictionary<string, string?> FieldChanges { get; init; } = new Dictionary<string, string?>();

    public bool HasAnyTagChange =>
        Title is not null || Artists is not null || Album is not null || AlbumArtists is not null ||
        Genres is not null || Year.HasValue || Track.HasValue || TrackCount.HasValue ||
        Disc.HasValue || DiscCount.HasValue || Comment is not null || Composers is not null ||
        Copyright is not null || Publisher is not null ||
        CoverPath is not null || RemoveCover || FieldChanges.Count > 0;
}
