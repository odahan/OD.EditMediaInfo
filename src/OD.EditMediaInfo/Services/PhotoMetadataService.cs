using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Xmp;
using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Services;

/// <summary>Reads photograph metadata, including manufacturer-specific RAW directories.</summary>
public static class PhotoMetadataService
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".png", ".tif", ".tiff", ".bmp", ".gif", ".webp",
        ".heic", ".heif", ".avif", ".dng", ".nef", ".nrw", ".cr2", ".cr3", ".crw",
        ".arw", ".sr2", ".srf", ".orf", ".rw2", ".raf", ".pef", ".raw", ".psd"
    };

    /// <summary>Gets the extensions routed to the photo metadata reader.</summary>
    internal static IEnumerable<string> RecognizedExtensions => Extensions;

    /// <summary>Determines whether a path should be inspected as a still image.</summary>
    public static bool IsPhoto(string path) => Extensions.Contains(Path.GetExtension(path));

    /// <summary>Extracts structured image, capture, location, IPTC, XMP, and ICC properties.</summary>
    public static bool Read(string path, MediaDetails details)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            details.Kind = "Photo";
            foreach (var directory in directories)
            {
                if (directory is XmpDirectory xmp)
                    foreach (var property in xmp.GetXmpProperties())
                        details.Properties.Add(new("Advanced", property.Key, property.Value, "XMP"));
                foreach (var error in directory.Errors)
                    details.Diagnostics.Add($"{directory.Name}: {error}");
                foreach (var tag in directory.Tags)
                {
                    if (tag.Name.Contains("Thumbnail Data", StringComparison.OrdinalIgnoreCase)) continue;
                    var section = directory is GpsDirectory ? "Location"
                        : directory is ExifSubIfdDirectory ? "Capture"
                        : directory is ExifIfd0Directory ? "Image"
                        : directory.Name is "JPEG" or "PNG-IHDR" or "BMP Header" or "WebP" or "JFIF" ? "Image"
                        : directory.Name.Contains("ICC", StringComparison.OrdinalIgnoreCase) ? "Color"
                        : "Advanced";
                    details.Properties.Add(new(section, tag.Name, tag.Description, directory.Name));
                }
            }

            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var ifd = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            int? width = Number(exif, ExifDirectoryBase.TagExifImageWidth) ?? Number(ifd, ExifDirectoryBase.TagImageWidth);
            int? height = Number(exif, ExifDirectoryBase.TagExifImageHeight) ?? Number(ifd, ExifDirectoryBase.TagImageHeight);
            if (width is null || height is null)
            {
                foreach (var directory in directories.Where(d => d is not ExifThumbnailDirectory))
                {
                    var w = directory.Tags.FirstOrDefault(t => t.Name == "Image Width");
                    var h = directory.Tags.FirstOrDefault(t => t.Name == "Image Height");
                    if (w is not null && h is not null && directory.TryGetInt32(w.Type, out var iw) && directory.TryGetInt32(h.Type, out var ih))
                    {
                        width = iw;
                        height = ih;
                        break;
                    }
                }
            }
            if (width > 0 && height > 0)
            {
                details.Properties.Add(new("Overview", "Resolution", $"{width} × {height} pixels", "Image header / EXIF"));
                details.Properties.Add(new("Overview", "Megapixels", (width.Value * (double)height.Value / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture), "Calculated from dimensions"));
            }
            foreach (var id in new[] { ExifDirectoryBase.TagMake, ExifDirectoryBase.TagModel })
                AddOverview(ifd, id, details);
            foreach (var id in new[] { ExifDirectoryBase.TagLensModel, ExifDirectoryBase.TagFocalLength, ExifDirectoryBase.Tag35MMFilmEquivFocalLength, ExifDirectoryBase.TagExposureTime, ExifDirectoryBase.TagFNumber, ExifDirectoryBase.TagIsoEquivalent, ExifDirectoryBase.TagDateTimeOriginal })
                AddOverview(exif, id, details);
            return directories.Count > 0;
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or ArgumentException)
        {
            details.Diagnostics.Add($"Photo metadata could not be fully read: {ex.Message}");
            return false;
        }
    }

    /// <summary>Reads image dimensions from embedded artwork without decoding its pixels.</summary>
    public static (int Width, int Height) GetDimensions(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            foreach (var directory in ImageMetadataReader.ReadMetadata(stream))
            {
                var w = directory.Tags.FirstOrDefault(t => t.Name is "Image Width" or "Exif Image Width");
                var h = directory.Tags.FirstOrDefault(t => t.Name is "Image Height" or "Exif Image Height");
                if (w is not null && h is not null && directory.TryGetInt32(w.Type, out var width) && directory.TryGetInt32(h.Type, out var height))
                    return (width, height);
            }
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or ArgumentException) { }
        return (0, 0);
    }

    private static int? Number(MetadataExtractor.Directory? directory, int id) =>
        directory is not null && directory.TryGetInt32(id, out var value) && value > 0 ? value : null;

    private static void AddOverview(MetadataExtractor.Directory? directory, int id, MediaDetails details)
    {
        if (directory?.ContainsTag(id) == true)
            details.Properties.Add(new("Overview", directory.GetTagName(id), directory.GetDescription(id), directory.Name));
    }
}
