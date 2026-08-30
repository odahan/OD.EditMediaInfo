using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>
/// Reads, validates, updates, and clears tags in supported media files.
/// </summary>
public sealed class MediaTagService : IMediaTagService
{
    private const long MaxCoverBytes = 10L * 1024 * 1024;

    static MediaTagService()
    {
        // Force ID3v2.3 for Windows Media Player compatibility
        TagLib.Id3v2.Tag.DefaultVersion = 3;
        TagLib.Id3v2.Tag.ForceDefaultVersion = true;
    }

    /// <summary>Reads technical information and tags from a media file.</summary>
    public MediaInfo Read(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"File not found: \"{filePath}\"");

        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var props = tagFile.Properties;
            var tag = tagFile.Tag;

            var pictures = tag.Pictures.Select(p => new PictureInfo(
                p.MimeType ?? "unknown",
                0, 0,
                p.Type.ToString(),
                p.Description ?? string.Empty,
                p.Data?.Data?.Length ?? 0)).ToList();

            return new MediaInfo(
                FilePath: filePath,
                Format: props.Description ?? Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant(),
                Codec: string.Join(", ", props.Codecs.Where(c => c != null).Select(c => c.Description).Where(d => !string.IsNullOrWhiteSpace(d))) ?? string.Empty,
                Duration: props.Duration,
                AudioBitrate: props.AudioBitrate,
                AudioSampleRate: props.AudioSampleRate,
                AudioChannels: props.AudioChannels,
                AudioBitsPerSample: props.BitsPerSample > 0 ? props.BitsPerSample : null,
                FileSize: fileInfo.Length,
                Title: tag.Title ?? string.Empty,
                Artists: tag.Performers ?? Array.Empty<string>(),
                Album: tag.Album ?? string.Empty,
                AlbumArtists: tag.AlbumArtists ?? Array.Empty<string>(),
                Genres: tag.Genres ?? Array.Empty<string>(),
                Year: tag.Year,
                Track: tag.Track,
                TrackCount: tag.TrackCount,
                Disc: tag.Disc,
                DiscCount: tag.DiscCount,
                Comment: tag.Comment ?? string.Empty,
                Composers: tag.Composers ?? Array.Empty<string>(),
                Copyright: tag.Copyright ?? string.Empty,
                Publisher: tag.Publisher ?? string.Empty,
                Pictures: pictures);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read media file \"{filePath}\": {ex.Message}", ex);
        }
    }

    /// <summary>Applies an edit request and preserves the first backup file by default.</summary>
    public void Save(EditOptions options)
    {
        Validate(options);

        var filePath = options.FilePath;

        if (options.DryRun)
            return;

        // Backup
        if (!options.NoBackup)
        {
            var bak = filePath + ".bak";
            if (!File.Exists(bak))
                File.Copy(filePath, bak);
        }

        DateTime? originalMtime = null;
        if (options.KeepMtime)
            originalMtime = File.GetLastWriteTimeUtc(filePath);

        using var tagFile = TagLib.File.Create(filePath);

        if (options.Title is not null) tagFile.Tag.Title = options.Title;
        if (options.Artists is not null) tagFile.Tag.Performers = options.Artists;
        if (options.Album is not null) tagFile.Tag.Album = options.Album;
        if (options.AlbumArtists is not null) tagFile.Tag.AlbumArtists = options.AlbumArtists;
        if (options.Genres is not null) tagFile.Tag.Genres = options.Genres;
        if (options.Year.HasValue) tagFile.Tag.Year = options.Year.Value;
        if (options.Track.HasValue) tagFile.Tag.Track = options.Track.Value;
        if (options.TrackCount.HasValue) tagFile.Tag.TrackCount = options.TrackCount.Value;
        if (options.Disc.HasValue) tagFile.Tag.Disc = options.Disc.Value;
        if (options.DiscCount.HasValue) tagFile.Tag.DiscCount = options.DiscCount.Value;
        if (options.Comment is not null) tagFile.Tag.Comment = options.Comment;
        if (options.Composers is not null) tagFile.Tag.Composers = options.Composers;
        if (options.Copyright is not null) tagFile.Tag.Copyright = options.Copyright;
        if (options.Publisher is not null) tagFile.Tag.Publisher = options.Publisher;

        // Handle cover
        var existing = tagFile.Tag.Pictures?.ToList() ?? new List<TagLib.IPicture>();

        if (options.RemoveCover)
        {
            existing.Clear();
        }
        else if (options.CoverPath is not null)
        {
            // Determine type
            var picType = options.CoverType switch
            {
                CoverType.Back => TagLib.PictureType.BackCover,
                CoverType.Other => TagLib.PictureType.Other,
                _ => TagLib.PictureType.FrontCover
            };

            // Remove existing pictures of the same type
            existing.RemoveAll(p => p.Type == picType);

            // Add new picture
            var pic = new TagLib.Picture(options.CoverPath)
            {
                Type = picType,
                Description = options.CoverDescription ?? string.Empty
            };
            existing.Add(pic);
        }

        tagFile.Tag.Pictures = existing.ToArray();
        tagFile.Save();

        if (originalMtime.HasValue)
            File.SetLastWriteTimeUtc(filePath, originalMtime.Value);
    }

    /// <summary>Removes all tags from a media file.</summary>
    public void Clear(string filePath, bool noBackup, bool keepMtime)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: \"{filePath}\"");

        if (!noBackup && !File.Exists(filePath + ".bak"))
            File.Copy(filePath, filePath + ".bak");

        DateTime? originalMtime = keepMtime ? File.GetLastWriteTimeUtc(filePath) : null;

        using var tagFile = TagLib.File.Create(filePath);
        tagFile.RemoveTags(TagLib.TagTypes.AllTags);
        tagFile.Save();

        if (originalMtime.HasValue)
            File.SetLastWriteTimeUtc(filePath, originalMtime.Value);
    }

    /// <summary>
    /// Validates an edit request without changing the target media file.
    /// </summary>
    /// <param name="options">The edit request to validate.</param>
    /// <exception cref="FileNotFoundException">Thrown when the media file or cover file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when incompatible options are supplied.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a cover exceeds the supported size or has an unsupported extension.</exception>
    public void Validate(EditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.FilePath))
            throw new FileNotFoundException($"File not found: \"{options.FilePath}\"");

        if (options.RemoveCover && options.CoverPath is not null)
            throw new ArgumentException("Cannot use --cover and --remove-cover together.");

        if (options.CoverDescription is not null && options.CoverPath is null)
            throw new ArgumentException("--cover-desc can only be used together with --cover.");

        if (options.CoverPath is null)
            return;

        var coverInfo = new FileInfo(options.CoverPath);
        if (!coverInfo.Exists)
            throw new FileNotFoundException($"Cover file not found: \"{options.CoverPath}\"");
        if (coverInfo.Length > MaxCoverBytes)
            throw new InvalidOperationException($"Cover too large ({coverInfo.Length / (1024.0 * 1024):F1} MB > 10 MB). Maximum is 10 MB.");

        var extension = coverInfo.Extension.ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".bmp")
            throw new InvalidOperationException($"Unsupported cover format \"{extension}\". Use JPG, PNG, WebP, or BMP.");
    }

    private static TagLib.PictureType ParseCoverType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "back" => TagLib.PictureType.BackCover,
            "other" => TagLib.PictureType.Other,
            _ => TagLib.PictureType.FrontCover
        };
    }
}