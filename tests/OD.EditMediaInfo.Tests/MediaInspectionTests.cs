using System.Text;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;
using OD.EditMediaInfo.ViewModels;
using OD.EditMediaInfo.Views;
using Spectre.Console;

namespace OD.EditMediaInfo.Tests;

/// <summary>Exercises real metadata round trips, safe saves, stream parsing, and terminal editing state.</summary>
public sealed class MediaInspectionTests
{
    private const string Photo = "/9j/4AAQSkZJRgABAgAAAQABAAD//gAQTGF2YzYyLjI4LjEwMgD/2wBDAAgEBAQEBAUFBQUFBQYGBgYGBgYGBgYGBgYHBwcICAgHBwcGBgcHCAgICAkJCQgICAgJCQoKCgwMCwsODg4RERT/xABNAAEBAAAAAAAAAAAAAAAAAAAABwEBAQEAAAAAAAAAAAAAAAAAAAUHEAEAAAAAAAAAAAAAAAAAAAAAEQEAAAAAAAAAAAAAAAAAAAAA/8AAEQgAWgCgAwEiAAIRAAMRAP/aAAwDAQACEQMRAD8AjgDf0oAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB/9k=";

    /// <summary>Checks fractional rates, separate tracks, attached artwork, chapter timing, and preserved HDR side data.</summary>
    [Fact]
    public void Probe_PreservesTracksFractionsAndSideData()
    {
        var details = new MediaDetails();
        FFprobeService.ApplyJson("""
            {"format":{"format_name":"matroska","bit_rate":"12000000"},"streams":[
            {"index":0,"codec_type":"video","codec_name":"hevc","width":3840,"height":2160,"avg_frame_rate":"30000/1001","r_frame_rate":"30/1","nb_frames":"1800","color_transfer":"smpte2084","side_data_list":[{"side_data_type":"Mastering display metadata","max_luminance":"10000000/10000"}]},
            {"index":1,"codec_type":"audio","codec_name":"aac","sample_rate":"48000","channel_layout":"5.1","tags":{"language":"fra"},"disposition":{"default":1}},
            {"index":2,"codec_type":"subtitle","codec_name":"subrip","disposition":{"forced":1}},
            {"index":3,"codec_type":"video","codec_name":"mjpeg","disposition":{"attached_pic":1}}],
            "chapters":[{"start_time":"0","end_time":"30.5","tags":{"title":"Opening"}}]}
            """, details);
        Assert.Equal("Video", details.Kind);
        Assert.Equal(12_000_000, details.OverallBitrate);
        Assert.Equal(4, details.Streams.Count);
        Assert.Equal("artwork", details.Streams[3].Type);
        Assert.Contains(details.Properties, p => p.Name.Contains("Average frame rate") && p.Value == "29.97 fps (30000/1001)");
        Assert.Contains(details.Properties, p => p.Name.Contains("Cadence mode") && p.Value!.StartsWith("Unknown", StringComparison.Ordinal));
        Assert.Contains(details.Properties, p => p.Name.Contains("max_luminance") && p.Value == "10000000/10000");
        Assert.Contains(details.Properties, p => p.Name.Contains("language") && p.Value == "fra");
        Assert.Equal(30.5, Assert.Single(details.Chapters).EndSeconds);
        Assert.Equal("Opening", details.Chapters[0].Title);
    }

    /// <summary>Rejects absent and invalid rational rates without treating them as zero fps.</summary>
    [Theory]
    [InlineData("0/0")]
    [InlineData("25/0")]
    [InlineData("N/A")]
    [InlineData(null)]
    public void FrameRate_UnknownIsNull(string? value) => Assert.Null(FFprobeService.FrameRate(value));

    /// <summary>Ensures a single cover-art stream does not turn an audio file into video.</summary>
    [Fact]
    public void Probe_ArtworkDoesNotChangeAudioKind()
    {
        var details = new MediaDetails();
        FFprobeService.ApplyJson("""{"streams":[{"index":0,"codec_type":"audio"},{"index":1,"codec_type":"video","disposition":{"attached_pic":1}}]}""", details);
        Assert.Equal("Audio", details.Kind);
        Assert.DoesNotContain(details.Properties, p => p.Name.Contains("Resolution"));
    }

    /// <summary>Distinguishes observed variable timestamps from uniform timing in a limited sample.</summary>
    [Theory]
    [InlineData("0.08", "Uniform frame timing")]
    [InlineData("0.10", "Variable frame timing")]
    public void Cadence_ReportsOnlyObservedTiming(string finalTimestamp, string expected)
    {
        var details = new MediaDetails();
        FFprobeService.ApplyCadenceJson("{\"frames\":[{\"best_effort_timestamp_time\":\"0\"},{\"best_effort_timestamp_time\":\"0.04\"},{\"best_effort_timestamp_time\":\"" + finalTimestamp + "\"}]}", 2, details);
        Assert.StartsWith(expected, Assert.Single(details.Properties).Value!);
        Assert.Contains("sample", details.Properties[0].Value!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Leaves cadence unknown when frame timestamps are absent.</summary>
    [Fact]
    public void Cadence_MissingTimingRemainsUnknown()
    {
        var details = new MediaDetails();
        FFprobeService.ApplyCadenceJson("{\"frames\":[{},{}]}", 0, details);
        Assert.StartsWith("Unknown", Assert.Single(details.Properties).Value!);
    }

    /// <summary>Verifies keep, replace, remove, undo, and filters do not lose unrelated pending values.</summary>
    [Fact]
    public void Grid_StagesExplicitOperationsAndPreservesThemAcrossFilters()
    {
        using var native = CreatePhoto();
        using var file = TagLib.File.Create(native.Path);
        var title = new EditorField("Title", "Metadata", "Title", "Original", "TagLibSharp", TagFieldCatalog.Find(file.Tag, "Title"));
        var author = new EditorField("Image.Creator", "Metadata", "Author", "Olivier", "TagLibSharp", TagFieldCatalog.Find(file.Tag, "Image.Creator"));
        using var model = new EditorViewModel(EmptyInfo(native.Path), [title, author]);
        title.Replace("New title");
        author.Remove();
        Assert.Equal(2, model.PendingCount);
        model.Search = "author";
        Assert.Same(author, Assert.Single(model.VisibleFields("Overview")));
        title.Replace("   ");
        Assert.Equal("New title", title.Replacement);
        var edits = model.ToEditOptions();
        Assert.Equal("New title", edits.FieldChanges["Title"]);
        Assert.Null(edits.FieldChanges["Image.Creator"]);
        Assert.Null(edits.CoverDescription);
        author.Revert();
        Assert.Single(model.ToEditOptions().FieldChanges);
        model.RevertAll();
        Assert.Equal(0, model.PendingCount);
    }

    /// <summary>Rejects read-only edits and invalid capture settings before any file operation.</summary>
    [Fact]
    public void Grid_ValidatesCaptureValuesAndReadOnlyRows()
    {
        using var photo = CreatePhoto();
        using var file = TagLib.File.Create(photo.Path);
        Assert.Equal(0.008, TagFieldCatalog.Find(file.Tag, "Image.ExposureTime").Parse("1/125"));
        Assert.Throws<ArgumentException>(() => TagFieldCatalog.Find(file.Tag, "Image.FNumber").Parse("0"));
        Assert.Throws<ArgumentException>(() => TagFieldCatalog.Find(file.Tag, "Image.Latitude").Parse("91"));
        Assert.Throws<ArgumentException>(() => TagFieldCatalog.Find(file.Tag, "Image.ISOSpeedRatings").Parse("1.5"));
        Assert.Throws<InvalidOperationException>(() => new EditorField("x", "Image", "Resolution", "160×90", "Header").Remove());
        var capture = new EditorField("Image.ExposureTime", "Capture", "Exposure", "1/125", "EXIF", TagFieldCatalog.Find(file.Tag, "Image.ExposureTime"));
        Assert.False(capture.IsEditable);
        Assert.Throws<InvalidOperationException>(() => capture.Replace("1/250"));
        capture.EnableCaptureEditing();
        capture.Replace("1/250");
        Assert.True(capture.IsModified);
    }

    /// <summary>Reads dimensions and capture fields from a JPEG, then verifies grouped writes preserve other metadata.</summary>
    [Fact]
    public void Photo_RoundTripsCaptureAndDescriptionWithBackup()
    {
        using var photo = CreatePhoto();
        var original = File.ReadAllBytes(photo.Path);
        var service = new MediaTagService();
        service.Save(new EditOptions
        {
            FilePath = photo.Path,
            FieldChanges = new Dictionary<string, string?>
            {
                ["Title"] = "Sunrise",
                ["Image.Creator"] = "Olivier",
                ["Image.FNumber"] = "2.8",
                ["Image.ExposureTime"] = "1/125",
                ["Image.FocalLength"] = "85",
                ["Image.ISOSpeedRatings"] = "400",
                ["Image.Keywords"] = "travel; mountain"
            }
        });
        Assert.Equal(original, File.ReadAllBytes(photo.Path + ".bak"));
        var info = service.Read(photo.Path);
        Assert.Equal("Photo", info.Details.Kind);
        Assert.True(info.Details.CanWritePhoto);
        Assert.Contains(info.Details.Properties, p => p.Name == "Resolution" && p.Value == "160 × 90 pixels");
        Assert.Contains(info.Details.Properties, p => p.Name == "F-Number");
        Assert.Contains(info.Details.Properties, p => p.Source == "XMP" && p.Value == "Sunrise");
        using (var file = TagLib.File.Create(photo.Path))
        {
            var tag = Assert.IsAssignableFrom<TagLib.Image.ImageTag>(file.Tag);
            Assert.Equal(0.008, tag.ExposureTime!.Value, 5);
            Assert.Equal(2.8, tag.FNumber!.Value, 5);
            Assert.Equal("Olivier", tag.Creator);
        }
        service.Save(new EditOptions { FilePath = photo.Path, FieldChanges = new Dictionary<string, string?> { ["Title"] = null } });
        using var reread = TagLib.File.Create(photo.Path);
        Assert.True(string.IsNullOrEmpty(reread.Tag.Title));
        Assert.Equal("Olivier", ((TagLib.Image.ImageTag)reread.Tag).Creator);
        Assert.Equal(original, File.ReadAllBytes(photo.Path + ".bak"));
    }

    /// <summary>Checks failed persistence leaves the original, backup state, and directory untouched.</summary>
    [Fact]
    public void Save_UnsupportedFieldPreservesOriginal()
    {
        using var photo = CreatePhoto();
        var before = File.ReadAllBytes(photo.Path);
        Assert.Throws<ArgumentException>(() => new MediaTagService().Save(new EditOptions { FilePath = photo.Path, FieldChanges = new Dictionary<string, string?> { ["NotAField"] = "x" } }));
        Assert.Equal(before, File.ReadAllBytes(photo.Path));
        Assert.False(File.Exists(photo.Path + ".bak"));
    }

    /// <summary>Verifies artwork dimensions are extracted from the encoded image header.</summary>
    [Fact]
    public void Artwork_HasRealDimensions()
    {
        using var photo = CreatePhoto();
        Assert.Equal((160, 90), PhotoMetadataService.GetDimensions(File.ReadAllBytes(photo.Path)));
        Assert.Equal((0, 0), PhotoMetadataService.GetDimensions([1, 2, 3]));
    }

    /// <summary>Checks a dense grid fits a terminal page and escapes metadata markup and control sequences.</summary>
    [Theory]
    [InlineData(80)]
    [InlineData(120)]
    public void Grid_RenderFitsViewport(int width)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = width;
        using var model = new EditorViewModel(EmptyInfo("sample.jpg"), Enumerable.Range(0, 30).Select(i => new EditorField($"{i}", "Overview", $"Property {i}", "[red]value[/]\u001b[2J" + new string('x', 200), "EXIF")));
        TerminalEditorView.Render(console, model, "Overview", 0, 8, "Ready");
        var lines = writer.ToString().Replace("\r", "").Split('\n');
        Assert.True(lines.Length <= 23, $"Rendered {lines.Length} lines: {writer}");
        Assert.All(lines, line => Assert.True(line.Length <= width, $"Line exceeds {width}: {line}"));
        Assert.DoesNotContain('\u001b', writer.ToString());
        Assert.Contains("Read-only", writer.ToString());
        Assert.DoesNotContain("Property 8", writer.ToString());
    }

    /// <summary>Ensures legacy audio tag editing and modification-time preservation remain supported.</summary>
    [Fact]
    public void Audio_LegacySavePreservesTimeAndBackup()
    {
        using var temporary = new TemporaryMedia(".wav");
        using (var writer = new BinaryWriter(File.Create(temporary.Path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 16000);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(16000); writer.Write(new byte[16000]);
        }
        var date = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(temporary.Path, date);
        var service = new MediaTagService();
        service.Save(new EditOptions { FilePath = temporary.Path, Title = "Test", KeepMtime = true });
        using var file = TagLib.File.Create(temporary.Path);
        Assert.Equal("Test", file.Tag.Title);
        Assert.Equal(8000, file.Properties.AudioSampleRate);
        Assert.Equal(date, File.GetLastWriteTimeUtc(temporary.Path));
        Assert.True(File.Exists(temporary.Path + ".bak"));
    }

    private static MediaInfo EmptyInfo(string path) => new(path, "JPEG", "", TimeSpan.Zero, 0, 0, 0, null, 300, "", [], "", [], [], 0, 0, 0, 0, 0, "", [], "", "", []);

    private static TemporaryMedia CreatePhoto()
    {
        var temporary = new TemporaryMedia(".jpg");
        File.WriteAllBytes(temporary.Path, Convert.FromBase64String(Photo));
        return temporary;
    }

    private sealed class TemporaryMedia(string extension) : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"odmedia-test-{Guid.NewGuid():N}{extension}");
        public void Dispose()
        {
            File.Delete(Path);
            File.Delete(Path + ".bak");
        }
    }
}
