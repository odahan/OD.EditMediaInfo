namespace OD.EditMediaInfo.Services;

/// <summary>Formats media sizes for display.</summary>
public static class MediaSizeFormatter
{
    /// <summary>Formats file and artwork sizes using explicit binary units.</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = bytes;
        int index = 0;
        while (size >= 1024 && index < units.Length - 1) { size /= 1024; index++; }
        return $"{size:0.##} {units[index]} ({bytes} bytes)";
    }
}
