using System.Text.Json;
using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

public static class PresetService
{
    private const long MaxCoverBytes = 10L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetPresetDirectory()
    {
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

    public static void EnsureWritable()
    {
        var dir = GetPresetDirectory();
        try
        {
            Directory.CreateDirectory(dir);
            // probe write
            var probe = Path.Combine(dir, ".write_test");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Impossible d'écrire dans \"{dir}\" — dossier non inscriptible. Déplacez odmedia.exe dans un dossier inscriptible ou lancez en administrateur.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Impossible d'écrire dans \"{dir}\" — {ex.Message}", ex);
        }
    }

    public static string SanitizeName(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom du preset ne peut pas être vide.");
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        if (sanitized.Length > 64) sanitized = sanitized[..64].TrimEnd();
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Nom du preset invalide après sanitization.");
        return sanitized;
    }

    public static string GetPresetPath(string name)
    {
        var safe = SanitizeName(name);
        return Path.Combine(GetPresetDirectory(), safe + ".json");
    }

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

    public static bool Exists(string name) => File.Exists(GetPresetPath(name));

    public static PresetFile Load(string name)
    {
        var path = GetPresetPath(name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Preset introuvable: \"{name}\" ({path})");
        var json = File.ReadAllText(path);
        var preset = JsonSerializer.Deserialize<PresetFile>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Preset corrompu: \"{name}\"");
        if (preset.Tags is null) preset.Tags = new PresetTags();
        return preset;
    }

    public static void Save(PresetFile preset)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(preset.Name))
            throw new ArgumentException("Preset name required");
        preset.Name = SanitizeName(preset.Name);
        preset.Created = preset.Created == default ? DateTime.UtcNow : preset.Created;
        preset.Version = 1;
        var path = GetPresetPath(preset.Name);
        var json = JsonSerializer.Serialize(preset, JsonOpts);
        File.WriteAllText(path, json);
    }

    public static void Delete(string name)
    {
        var path = GetPresetPath(name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Preset introuvable: \"{name}\"");
        File.Delete(path);
    }

    public static PresetCover? CoverFromFile(string coverPath, string? description = null)
    {
        var fi = new FileInfo(coverPath);
        if (!fi.Exists) throw new FileNotFoundException($"Cover introuvable: \"{coverPath}\"");
        if (fi.Length > MaxCoverBytes)
            throw new InvalidOperationException($"Cover trop volumineuse ({fi.Length / (1024.0 * 1024):F1} MB > 10 MB). Maximum 10 MB.");
        var ext = fi.Extension.ToLowerInvariant();
        if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".bmp")
            throw new InvalidOperationException($"Format cover non supporté \"{ext}\" — utilisez jpg ou png.");
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
            Description = description ?? "Front Cover",
            DataBase64 = Convert.ToBase64String(data)
        };
    }

    public static PresetCover? CoverFromMediaInfo(MediaInfo info)
    {
        if (info.Pictures.Count == 0) return null;
        // Use TagLib directly to get raw bytes with correct mime
        using var tagFile = TagLib.File.Create(info.FilePath);
        var pic = tagFile.Tag.Pictures.FirstOrDefault();
        if (pic is null || pic.Data.Data is null) return null;
        var len = pic.Data.Data.Length;
        if (len > MaxCoverBytes)
            throw new InvalidOperationException($"Cover du fichier source trop volumineuse ({len / (1024.0 * 1024):F1} MB > 10 MB).");
        return new PresetCover
        {
            MimeType = pic.MimeType ?? "image/jpeg",
            Description = pic.Description ?? "Front Cover",
            DataBase64 = Convert.ToBase64String(pic.Data.Data)
        };
    }

    public static EditOptions ToEditOptions(PresetFile preset, string targetFile, bool noBackup = false, bool keepMtime = false, bool dryRun = false)
    {
        // Build EditOptions with cover handling: preset cover -> temp file
        string? tempCoverPath = null;
        if (preset.Cover is not null)
        {
            var bytes = Convert.FromBase64String(preset.Cover.DataBase64);
            if (bytes.Length > MaxCoverBytes)
                throw new InvalidOperationException($"Cover du preset trop volumineuse ({bytes.Length / (1024.0 * 1024):F1} MB > 10 MB).");
            // write temp file for MediaTagService.Save which expects a path
            var ext = preset.Cover.MimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            tempCoverPath = Path.Combine(Path.GetTempPath(), $"odmedia_preset_{Guid.NewGuid():N}{ext}");
            File.WriteAllBytes(tempCoverPath, bytes);
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
            CoverDescription = preset.Cover?.Description,
            NoBackup = noBackup,
            KeepMtime = keepMtime,
            DryRun = dryRun
        };
    }

    /// <summary>Cleanup temp cover file created by ToEditOptions after Save.</summary>
    public static void CleanupTempCover(EditOptions opts)
    {
        if (opts.CoverPath is not null && opts.CoverPath.Contains("odmedia_preset_"))
        {
            try { File.Delete(opts.CoverPath); } catch { /* ignore */ }
        }
    }

    public static long GetCoverByteLength(PresetCover cover)
    {
        try { return Convert.FromBase64String(cover.DataBase64).Length; } catch { return 0; }
    }
}
