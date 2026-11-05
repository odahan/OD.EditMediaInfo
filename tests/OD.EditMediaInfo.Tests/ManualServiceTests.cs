using System.Globalization;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.Tests;

/// <summary>Validates embedded offline manuals and extraction independently of browser launching.</summary>
public sealed class ManualServiceTests
{
    /// <summary>Chooses the explicit language or a supported system UI default.</summary>
    [Theory]
    [InlineData(null, "fr-FR", "fr")]
    [InlineData(null, "en-US", "en")]
    [InlineData(null, "de-DE", "en")]
    [InlineData(" EN ", "fr-FR", "en")]
    [InlineData("fr", "en-US", "fr")]
    public void Language_ResolvesSupportedDefaults(string? explicitLanguage, string culture, string expected) =>
        Assert.Equal(expected, ManualService.ResolveLanguage(explicitLanguage, CultureInfo.GetCultureInfo(culture)));

    /// <summary>Extracts both languages in UTF-8 with offline links and an explicit dark theme.</summary>
    [Fact]
    public void Extract_ContainsBothEmbeddedDarkManuals()
    {
        using var folder = new TemporaryFolder();
        Assert.Equal(Path.Combine(folder.Path, "odmedia-manual-fr.html"), ManualService.ExtractTo(folder.Path, "fr"));
        foreach (var language in new[] { "en", "fr" })
        {
            var html = File.ReadAllText(Path.Combine(folder.Path, $"odmedia-manual-{language}.html"));
            Assert.StartsWith("<!doctype html>", html);
            Assert.Contains($"<html lang=\"{language}\">", html);
            Assert.Contains("charset=\"utf-8\"", html);
            Assert.Contains("color-scheme:dark", html);
            Assert.Contains($"href=\"odmedia-manual-{(language == "en" ? "fr" : "en")}.html\"", html);
            Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("url(", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("odmedia manual --lang", html);
        }
        Assert.Contains("métadonnées", File.ReadAllText(Path.Combine(folder.Path, "odmedia-manual-fr.html")));
        Assert.Equal(2, Directory.GetFiles(folder.Path).Length);
    }

    /// <summary>Preserves identical files and refreshes outdated manuals without touching adjacent files.</summary>
    [Fact]
    public void Extract_RefreshesOnlyOwnedManualFiles()
    {
        using var folder = new TemporaryFolder();
        var path = ManualService.ExtractTo(folder.Path, "en");
        var written = File.GetLastWriteTimeUtc(path);
        ManualService.ExtractTo(folder.Path, "en");
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        File.WriteAllText(path, "old manual");
        File.WriteAllText(Path.Combine(folder.Path, "notes.html"), "user notes");
        ManualService.ExtractTo(folder.Path, "en");
        Assert.StartsWith("<!doctype html>", File.ReadAllText(path));
        Assert.Equal("user notes", File.ReadAllText(Path.Combine(folder.Path, "notes.html")));
        Assert.DoesNotContain(Directory.GetFiles(folder.Path), p => p.EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>Rejects unsupported languages before writing any files.</summary>
    [Fact]
    public void Extract_RejectsUnsupportedLanguage()
    {
        using var folder = new TemporaryFolder();
        Assert.Throws<ArgumentException>(() => ManualService.ExtractTo(folder.Path, "../de"));
        Assert.Empty(Directory.GetFiles(folder.Path));
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"odmedia-manual-{Guid.NewGuid():N}");
        public TemporaryFolder() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
