using System.Globalization;
using System.Reflection;

namespace OD.EditMediaInfo.Services;

/// <summary>Describes a supported metadata field, its parser, and its native property.</summary>
public sealed record TagField(string Id, string Label, string Section, PropertyInfo Property, double? Minimum = null, double? Maximum = null)
{
    /// <summary>Reads the editable representation using culture-independent units.</summary>
    public string Read(TagLib.Tag tag)
    {
        var value = Property.GetValue(tag);
        return value switch
        {
            null => "",
            string[] items => string.Join("; ", items),
            double number when double.IsNaN(number) => "",
            double number when Id == "Image.ExposureTime" && number is > 0 and < 1 => $"1/{(1 / number).ToString("0.######", CultureInfo.InvariantCulture)}",
            DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            uint number when number == 0 => "",
            IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
    }

    /// <summary>Parses a replacement or a deliberate removal, rejecting invalid numeric and date values.</summary>
    public object? Parse(string? text)
    {
        var type = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        if (text is null)
        {
            if (Nullable.GetUnderlyingType(Property.PropertyType) is not null) return null;
            if (type == typeof(string)) return "";
            if (type == typeof(string[])) return Array.Empty<string>();
            if (type == typeof(double)) return double.NaN;
            if (type == typeof(uint)) return 0u;
            throw new ArgumentException("This field cannot be removed.");
        }
        if (type == typeof(string)) return text;
        if (type == typeof(string[])) return text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (type == typeof(DateTime))
        {
            if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
            throw new ArgumentException("Use yyyy-MM-dd HH:mm:ss (camera local time, without an inferred timezone).");
        }
        double number;
        if (type == typeof(uint))
        {
            if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)) throw new ArgumentException("Enter a non-negative whole number.");
            number = integer;
        }
        else
        {
            var fraction = text.Split('/');
            if (!double.TryParse(fraction[0], NumberStyles.Float, CultureInfo.InvariantCulture, out number)) throw new ArgumentException("Enter a number using a decimal point, or a fraction such as 1/125.");
            if (fraction.Length > 1)
            {
                if (fraction.Length != 2 || !double.TryParse(fraction[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) || denominator == 0) throw new ArgumentException("Invalid fraction.");
                number /= denominator;
            }
        }
        if (!double.IsFinite(number) || Minimum.HasValue && number < Minimum || Maximum.HasValue && number > Maximum)
            throw new ArgumentException($"Value must be between {Minimum?.ToString(CultureInfo.InvariantCulture) ?? "−∞"} and {Maximum?.ToString(CultureInfo.InvariantCulture) ?? "+∞"}.");
        if (type == typeof(uint)) return (uint)number;
        return number;
    }

    /// <summary>Applies a validated value to the native tag.</summary>
    public void Write(TagLib.Tag tag, string? value) => Property.SetValue(tag, Parse(value));
}

/// <summary>Defines an explicit allowlist of writable fields; unsupported formats are never inferred writable.</summary>
public static class TagFieldCatalog
{
    private static TagField Tag(string name, string label, double? min = null, double? max = null) =>
        new(name, label, "Metadata", typeof(TagLib.Tag).GetProperty(name)!, min, max);
    private static TagField Photo(string name, string label, string section = "Capture", double? min = null, double? max = null) =>
        new("Image." + name, label, section, typeof(TagLib.Image.ImageTag).GetProperty(name)!, min, max);

    private static readonly TagField[] Common =
    [
        Tag("Title", "Title"), Tag("Performers", "Artist(s)"), Tag("Album", "Album"), Tag("AlbumArtists", "Album artist(s)"),
        Tag("Genres", "Genre(s)"), Tag("Year", "Year", 0, 9999), Tag("Track", "Track", 0), Tag("TrackCount", "Track count", 0),
        Tag("Disc", "Disc", 0), Tag("DiscCount", "Disc count", 0), Tag("Comment", "Comment"), Tag("Composers", "Composer(s)"),
        Tag("Copyright", "Copyright"), Tag("Publisher", "Publisher"), Tag("Grouping", "Grouping"), Tag("Conductor", "Conductor"),
        Tag("BeatsPerMinute", "Tempo (BPM)", 0), Tag("ReplayGainTrackGain", "ReplayGain track gain (dB)"),
        Tag("ReplayGainTrackPeak", "ReplayGain track peak", 0), Tag("ReplayGainAlbumGain", "ReplayGain album gain (dB)"),
        Tag("ReplayGainAlbumPeak", "ReplayGain album peak", 0)
    ];

    private static readonly TagField[] PhotoFields =
    [
        Tag("Title", "Title"), Tag("Comment", "Description"), Tag("Copyright", "Copyright"),
        Photo("Creator", "Author", "Metadata"), Photo("Keywords", "Keywords (; separated)", "Metadata"),
        Photo("Rating", "Rating (0–5)", "Metadata", 0, 5), Photo("DateTime", "Date taken (yyyy-MM-dd HH:mm:ss)"),
        Photo("Make", "Camera manufacturer"), Photo("Model", "Camera model"), Photo("Software", "Software", "Metadata"),
        Photo("ExposureTime", "Exposure time (s or 1/125)", min: double.Epsilon), Photo("FNumber", "Aperture (f-number)", min: double.Epsilon),
        Photo("ISOSpeedRatings", "ISO", min: 1), Photo("FocalLength", "Focal length (mm)", min: double.Epsilon),
        Photo("FocalLengthIn35mmFilm", "35 mm equivalent focal length (mm)", min: 1),
        Photo("Latitude", "Latitude (degrees)", "Location", -90, 90), Photo("Longitude", "Longitude (degrees)", "Location", -180, 180),
        Photo("Altitude", "Altitude (m)", "Location")
    ];

    /// <summary>Gets the fields appropriate to a native tag type.</summary>
    public static IReadOnlyList<TagField> GetFields(TagLib.Tag tag) => tag is TagLib.Image.ImageTag ? PhotoFields : Common;

    /// <summary>Looks up a field and rejects writes outside the appropriate allowlist.</summary>
    public static TagField Find(TagLib.Tag tag, string id) => GetFields(tag).FirstOrDefault(f => f.Id == id)
        ?? throw new ArgumentException($"Unsupported metadata field: {id}");
}
