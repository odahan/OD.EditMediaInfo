using System.Text.Json;
using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>
/// Creates, validates, and persists reusable media-tag presets.
/// </summary>
public static class PresetService
{
    private const long MaxCoverBytes = 10L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Gets the directory used to store preset files.</summary>
    public static string GetPresetDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("ODMEDIA_PRESET_DIR");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return Path.GetFullPath(configuredDirectory);

        // For single-file publish, AppContext.BaseDirectory is the extraction temp dir, not the exe dir.
        // Use Environment.ProcessPath (odmedia.exe) when available, fallback to AppContext.BaseDirectory.
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            var fileName = Path.GetFileName(exePath);
            if (fileName.StartsWith("odmedia", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return Path.Combine(dir, "presets");
                if (!string.IsNullOrEmpty(dir))
                    return Path.Combine(dir, "presets");
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "presets");
    }

    /// <summary>Ensures that the preset directory can be created and written to.</summary>
    public static void EnsureWritable()
    {
        var dir = GetPresetDirectory();
        try
        {
            Directory.CreateDirectory(dir);
            // probe write
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Cannot write to \"{dir}\". Move odmedia.exe to a writable folder or run it as an administrator.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Cannot write to \"{dir}\": {ex.Message}", ex);
        }
    }

    /// <summary>Converts a user-supplied preset name into a safe file name.</summary>
    public static string SanitizeName(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Preset name cannot be empty.");
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        if (sanitized.Length > 64) sanitized = sanitized[..64].TrimEnd();
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Preset name is invalid after sanitization.");
        return sanitized;
    }

    /// <summary>Gets the full path of a named preset.</summary>
    public static string GetPresetPath(string name)
    {
        var safe = SanitizeName(name);
        return Path.Combine(GetPresetDirectory(), safe + ".json");
    }

    /// <summary>Lists the available preset names in alphabetical order.</summary>
    public static IReadOnlyList<string> List()
    {
        var dir = GetPresetDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.GetFiles(dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Determines whether a named preset exists.</summary>
    public static bool Exists(string name) => File.Exists(GetPresetPath(name));

    /// <summary>Loads and deserializes a named preset.</summary>
    public static PresetFile Load(string name)
    {
        var path = GetPresetPath(name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Preset not found: \"{name}\" ({path})");
        var json = File.ReadAllText(path);
        var preset = JsonSerializer.Deserialize<PresetFile>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Preset is corrupted: \"{name}\"");
        if (preset.Tags is null) preset.Tags = new PresetTags();
        return preset;
    }

    /// <summary>Saves a preset using a temporary file to avoid partial writes.</summary>
    public static void Save(PresetFile preset, bool overwrite = false)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(preset.Name))
            throw new ArgumentException("Preset name required");
        preset.Name = SanitizeName(preset.Name);
        preset.Created = preset.Created == default ? DateTime.UtcNow : preset.Created;
        preset.Version = 1;
        var path = GetPresetPath(preset.Name);
        if (File.Exists(path) && !overwrite)
            throw new IOException($"Preset \"{preset.Name}\" already exists. Use --force to overwrite it.");

        var json = JsonSerializer.Serialize(preset, JsonOpts);
        var temporaryPath = Path.Combine(GetPresetDirectory(), $".{preset.Name}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>Deletes a named preset.</summary>
    public static void Delete(string name)
    {
        var path = GetPresetPath(name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Preset not found: \"{name}\"");
        File.Delete(path);
    }

    /// <summary>Creates an in-memory preset cover from an image file.</summary>
    public static PresetCover? CoverFromFile(string coverPath, string? description = null, string? type = "Front")
    {
        var fi = new FileInfo(coverPath);
        if (!fi.Exists) throw new FileNotFoundException($"Cover file not found: \"{coverPath}\"");
        if (fi.Length > MaxCoverBytes)
            throw new InvalidOperationException($"Cover is too large ({fi.Length / (1024.0 * 1024):F1} MB > 10 MB). Maximum is 10 MB.");
        var ext = fi.Extension.ToLowerInvariant();
        if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".bmp")
            throw new InvalidOperationException($"Unsupported cover format \"{ext}\". Use JPG, PNG, WebP, or BMP.");
        var data = File.ReadAllBytes(coverPath);
        var mime = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
        return new PresetCover
        {
            MimeType = mime,
            Type = type ?? "Front",
            Description = description ?? "Front Cover",
            DataBase64 = Convert.ToBase64String(data)
        };
    }

    /// <summary>Creates an in-memory preset cover from the first embedded picture.</summary>
    public static PresetCover? CoverFromMediaInfo(MediaInfo info)
    {
        if (info.Pictures.Count == 0) return null;
        using var tagFile = TagLib.File.Create(info.FilePath);
        var pic = tagFile.Tag.Pictures.FirstOrDefault();
        if (pic is null || pic.Data.Data is null) return null;
        var len = pic.Data.Data.Length;
        if (len > MaxCoverBytes)
            throw new InvalidOperationException($"The source file cover is too large ({len / (1024.0 * 1024):F1} MB > 10 MB).");
        var type = pic.Type.ToString(); // "FrontCover", "BackCover", etc.
        return new PresetCover
        {
            MimeType = pic.MimeType ?? "image/jpeg",
            Type = type,
            Description = pic.Description ?? "Front Cover",
            DataBase64 = Convert.ToBase64String(pic.Data.Data)
        };
    }

    /// <summary>Converts a preset into an edit request for a target media file.</summary>
    public static EditOptions ToEditOptions(PresetFile preset, string targetFile, bool noBackup = false, bool keepMtime = false, bool dryRun = false)
    {
        string? tempCoverPath = null;
        CoverType? coverType = null;
        if (preset.Cover is not null)
        {
            var encodedLength = preset.Cover.DataBase64.Length;
            if (encodedLength > ((MaxCoverBytes + 2) / 3) * 4)
                throw new InvalidOperationException("The preset cover is too large.");

            var bytes = Convert.FromBase64String(preset.Cover.DataBase64);
            if (bytes.Length > MaxCoverBytes)
                throw new InvalidOperationException($"The preset cover is too large ({bytes.Length / (1024.0 * 1024):F1} MB > 10 MB).");
            var ext = preset.Cover.MimeType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                _ => ".jpg"
            };
            tempCoverPath = Path.Combine(Path.GetTempPath(), $"odmedia_preset_{Guid.NewGuid():N}{ext}");
            File.WriteAllBytes(tempCoverPath, bytes);

            coverType = preset.Cover.Type?.ToLowerInvariant() switch
            {
                "back" or "backcover" => CoverType.Back,
                "other" => CoverType.Other,
                _ => CoverType.Front
            };
        }

        return new EditOptions
        {
            FilePath = targetFile,
            Artists = preset.Tags.Artists,
            Album = preset.Tags.Album,
            AlbumArtists = preset.Tags.AlbumArtists,
            Genres = preset.Tags.Genres,
            Year = preset.Tags.Year,
            Comment = preset.Tags.Comment,
            Composers = preset.Tags.Composers,
            Copyright = preset.Tags.Copyright,
            Publisher = preset.Tags.Publisher,
            CoverPath = tempCoverPath,
            CoverType = coverType,
            CoverDescription = preset.Cover?.Description,
            NoBackup = noBackup,
            KeepMtime = keepMtime,
            DryRun = dryRun
        };
    }

    /// <summary>Deletes a temporary cover file created by <see cref="ToEditOptions"/>.</summary>
    public static void CleanupTempCover(EditOptions opts)
    {
        if (opts.CoverPath is not null && opts.CoverPath.Contains("odmedia_preset_"))
        {
            try { File.Delete(opts.CoverPath); } catch { /* ignore */ }
        }
    }

    /// <summary>Gets the decoded byte length of a preset cover, or zero when invalid.</summary>
    public static long GetCoverByteLength(PresetCover cover)
    {
        try { return Convert.FromBase64String(cover.DataBase64).Length; } catch { return 0; }
    }
}
