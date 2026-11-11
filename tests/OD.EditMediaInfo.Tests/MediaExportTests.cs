using System.Globalization;
using System.Text.Json;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.Tests;

/// <summary>Verifies the shared report formats and protects the source media from accidental overwrites.</summary>
public sealed class MediaExportTests
{
    /// <summary>Preserves the complete report and the legacy CLI JSON contract.</summary>
    [Fact]
    public void Json_PreservesBothInspectionSchemas()
    {
        var info = CreateInfo("track.mp3");
        using var report = JsonDocument.Parse(WriteReport(info, MediaExportFormat.Json));
        Assert.Equal(info.FilePath, report.RootElement.GetProperty("FilePath").GetString());
        Assert.Equal("ffprobe", report.RootElement.GetProperty("Details").GetProperty("Diagnostics")[0].GetString());

        using var output = new StringWriter();
        MediaExportService.WriteLegacyInfoJson(output, info);
        using var legacy = JsonDocument.Parse(output.ToString());
        Assert.Equal(info.FilePath, legacy.RootElement.GetProperty("file").GetString());
        Assert.Equal(128, legacy.RootElement.GetProperty("bitrate").GetInt32());
        Assert.Equal(3.5, legacy.RootElement.GetProperty("durationSeconds").GetDouble());
    }

    /// <summary>Quotes punctuation and line breaks while treating formula-like metadata as text.</summary>
    [Fact]
    public void Csv_EscapesMetadataAndPreservesSourceColumns()
    {
        var report = WriteReport(CreateInfo("track.mp3"), MediaExportFormat.Csv);
        Assert.StartsWith("Section,Field,Value,Source\r\n", report);
        Assert.Contains("\"Metadata\",\"Title\",\"Track, \"\"one\"\"\nLive\",\"TagLibSharp\"\r\n", report);
        Assert.Contains("\"Metadata\",\"Comment\",\"'  =1+1\",\"TagLibSharp\"\r\n", report);
        Assert.Contains("\"Advanced\",\"Probe value\",\"-0.25\",\"FFprobe\"\r\n", report);
    }

    /// <summary>Produces a single dark HTML report with safely encoded media metadata.</summary>
    [Fact]
    public void Html_EncodesUntrustedValuesAndIncludesInlineStyles()
    {
        var report = WriteReport(CreateInfo("track.mp3") with { Title = "<script>alert(1)</script>" }, MediaExportFormat.Html);
        Assert.Contains("<!doctype html>", report);
        Assert.Contains("color-scheme: dark", report);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", report);
        Assert.DoesNotContain("<script", report);
        Assert.Contains("href=\"#section-", report);
        Assert.Contains("Probe value", report);
    }

    /// <summary>Rejects the media path and protects existing reports unless replacement is requested.</summary>
    [Fact]
    public void Save_PreservesMediaAndExistingReports()
    {
        var folder = Directory.CreateTempSubdirectory("odmedia-export-test-");
        try
        {
            var media = Path.Combine(folder.FullName, "track.mp3");
            var report = Path.Combine(folder.FullName, "track.html");
            File.WriteAllText(media, "original");
            File.WriteAllText(report, "previous");
            var info = CreateInfo(media);
            Assert.Throws<IOException>(() => MediaExportService.Save(media, info, MediaExportFormat.Html, overwrite: true));
            Assert.Throws<IOException>(() => MediaExportService.Save(report, info, MediaExportFormat.Html));
            Assert.Equal("original", File.ReadAllText(media));
            Assert.Equal("previous", File.ReadAllText(report));

            MediaExportService.Save(report, info, MediaExportFormat.Html, overwrite: true);
            Assert.StartsWith("<!doctype html>", File.ReadAllText(report));
            Assert.Equal(2, folder.GetFiles().Length);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static string WriteReport(MediaInfo info, MediaExportFormat format)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        MediaExportService.Write(output, info, format);
        return output.ToString();
    }

    private static MediaInfo CreateInfo(string path)
    {
        var details = new MediaDetails { Kind = "Audio", Container = "MP3" };
        details.Properties.Add(new("Advanced", "Probe value", "-0.25", "FFprobe"));
        details.Diagnostics.Add("ffprobe");
        return new MediaInfo(path, "MP3", "MP3", TimeSpan.FromSeconds(3.5), 128, 44100, 2, null, 4096,
            "Track, \"one\"\nLive", ["Artist"], "", [], [], 0, 0, 0, 0, 0, "  =1+1", [], "", "", [])
        {
            Details = details
        };
    }
}
