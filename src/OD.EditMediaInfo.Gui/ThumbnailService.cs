using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OD.EditMediaInfo.Models;

namespace OD.EditMediaInfo.Gui;

/// <summary>Identifies an image in the current inspection, with a fallback when Windows cannot decode it.</summary>
internal sealed record MediaThumbnail(string Label, BitmapSource? Image);

/// <summary>Decodes bounded, frozen previews that can be passed from a worker thread to WPF.</summary>
internal static class ThumbnailService
{
    private const int PreviewSize = 320;
    private const int MaxPresetCoverCharacters = ((10 * 1024 * 1024 + 2) / 3) * 4;

    /// <summary>Builds a photo preview and previews of the embedded artwork in inspection order.</summary>
    public static IReadOnlyList<MediaThumbnail> ReadSaved(MediaInfo info)
    {
        var thumbnails = new List<MediaThumbnail>();
        if (info.Details.Kind == "Photo")
            thumbnails.Add(new("Photo", ReadFile(info.FilePath)));

        if (info.Pictures.Count == 0)
            return thumbnails;

        var firstArtwork = thumbnails.Count;
        try
        {
            using var file = TagLib.File.Create(info.FilePath, TagLib.ReadStyle.None);
            var pictures = file.Tag.Pictures;
            for (int index = 0; index < info.Pictures.Count; index++)
            {
                var picture = info.Pictures[index];
                var label = string.IsNullOrWhiteSpace(picture.Description) ? picture.Type : picture.Description;
                var image = index < pictures.Length ? ReadBytes(pictures[index].Data?.Data) : null;
                thumbnails.Add(new($"Artwork {index + 1} · {label}", image));
            }
        }
        catch (Exception ex) when (ex is TagLib.UnsupportedFormatException or TagLib.CorruptFileException || IsPreviewError(ex))
        {
            for (int index = thumbnails.Count - firstArtwork; index < info.Pictures.Count; index++)
                thumbnails.Add(new($"Artwork {index + 1} · {info.Pictures[index].Type}", null));
        }

        return thumbnails;
    }

    /// <summary>Decodes an image file while releasing its handle before returning.</summary>
    public static BitmapSource? ReadFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Decode(stream);
        }
        catch (Exception ex) when (IsPreviewError(ex))
        {
            return null;
        }
    }

    /// <summary>Decodes artwork carried by a shared preset.</summary>
    public static BitmapSource? ReadPresetCover(PresetCover cover)
    {
        if (cover.DataBase64 is not { Length: > 0 and <= MaxPresetCoverCharacters } data)
            return null;

        try
        {
            return ReadBytes(Convert.FromBase64String(data));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static BitmapSource? ReadBytes(byte[]? data)
    {
        if (data is not { Length: > 0 })
            return null;

        try
        {
            using var stream = new MemoryStream(data, writable: false);
            return Decode(stream);
        }
        catch (Exception ex) when (IsPreviewError(ex))
        {
            return null;
        }
    }

    private static BitmapSource Decode(Stream stream)
    {
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        var orientation = ReadOrientation(frame);
        stream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (width >= height && width > PreviewSize)
            bitmap.DecodePixelWidth = PreviewSize;
        else if (height > PreviewSize)
            bitmap.DecodePixelHeight = PreviewSize;
        bitmap.EndInit();
        bitmap.Freeze();

        var transform = orientation switch
        {
            2 => new Matrix(-1, 0, 0, 1, 0, 0),
            3 => new Matrix(-1, 0, 0, -1, 0, 0),
            4 => new Matrix(1, 0, 0, -1, 0, 0),
            5 => new Matrix(0, 1, 1, 0, 0, 0),
            6 => new Matrix(0, 1, -1, 0, 0, 0),
            7 => new Matrix(0, -1, -1, 0, 0, 0),
            8 => new Matrix(0, -1, 1, 0, 0, 0),
            _ => Matrix.Identity
        };
        if (transform.IsIdentity)
            return bitmap;

        var oriented = new TransformedBitmap(bitmap, new MatrixTransform(transform));
        oriented.Freeze();
        return oriented;
    }

    private static ushort ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata)
            {
                string query = frame.Decoder is JpegBitmapDecoder ? "/app1/ifd/{ushort=274}" : "/ifd/{ushort=274}";
                if (metadata.GetQuery(query) is ushort orientation)
                    return orientation;
            }
        }
        catch (Exception ex) when (IsPreviewError(ex)) { }

        return 1;
    }

    private static bool IsPreviewError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException or
            ArgumentException or InvalidOperationException or COMException;
}
