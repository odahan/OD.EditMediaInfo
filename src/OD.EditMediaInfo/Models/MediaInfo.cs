namespace OD.EditMediaInfo.Models;

public sealed record MediaInfo(
    string FilePath,
    string Format,
    string Codec,
    TimeSpan Duration,
    int AudioBitrate,
    int AudioSampleRate,
    int AudioChannels,
    int? AudioBitsPerSample,
    long FileSize,
    string Title,
    string[] Artists,
    string Album,
    string[] AlbumArtists,
    string[] Genres,
    uint Year,
    uint Track,
    uint TrackCount,
    uint Disc,
    uint DiscCount,
    string Comment,
    string[] Composers,
    string Copyright,
    string Publisher,
    IReadOnlyList<PictureInfo> Pictures,
    string? Error = null);

public sealed record PictureInfo(
    string MimeType,
    int Width,
    int Height,
    string Type,
    string Description,
    long DataLength);
