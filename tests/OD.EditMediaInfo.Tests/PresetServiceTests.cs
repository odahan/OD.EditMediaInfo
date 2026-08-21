using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.Tests;

/// <summary>
/// Tests preset naming, persistence, and cover-size protections.
/// </summary>
public sealed class PresetServiceTests : IDisposable
{
    private readonly string _presetDirectory = Path.Combine(Path.GetTempPath(), $"odmedia-presets-{Guid.NewGuid():N}");
    private readonly string? _previousPresetDirectory;

    /// <summary>
    /// Creates an isolated preset directory for the test.
    /// </summary>
    public PresetServiceTests()
    {
        _previousPresetDirectory = Environment.GetEnvironmentVariable("ODMEDIA_PRESET_DIR");
        Environment.SetEnvironmentVariable("ODMEDIA_PRESET_DIR", _presetDirectory);
    }

    /// <summary>
    /// Restores the preset directory setting and removes test data.
    /// </summary>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ODMEDIA_PRESET_DIR", _previousPresetDirectory);
        if (Directory.Exists(_presetDirectory))
            Directory.Delete(_presetDirectory, recursive: true);
    }

    /// <summary>
    /// Verifies that invalid filename characters are replaced deterministically.
    /// </summary>
    [Fact]
    public void SanitizeName_ReplacesInvalidFilenameCharacters()
    {
        var sanitized = PresetService.SanitizeName("Rock:Pop");

        Assert.Equal("Rock_Pop", sanitized);
    }

    /// <summary>
    /// Verifies that an existing preset requires an explicit overwrite request.
    /// </summary>
    [Fact]
    public void Save_RejectsExistingPresetWithoutOverwrite()
    {
        PresetService.Save(new PresetFile { Name = "My preset", Tags = new PresetTags { Album = "First" } });

        var exception = Assert.Throws<IOException>(() =>
            PresetService.Save(new PresetFile { Name = "My preset", Tags = new PresetTags { Album = "Second" } }));

        Assert.Contains("--force", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that preset cover data is rejected before decoding an oversized payload.
    /// </summary>
    [Fact]
    public void ToEditOptions_RejectsOversizedEncodedCover()
    {
        var encoded = new string('A', 14 * 1024 * 1024);
        var preset = new PresetFile
        {
            Name = "Large cover",
            Cover = new PresetCover { MimeType = "image/jpeg", DataBase64 = encoded }
        };

        Assert.Throws<InvalidOperationException>(() => PresetService.ToEditOptions(preset, "target.mp3"));
    }
}
