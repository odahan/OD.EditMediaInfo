using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.Tests;

/// <summary>
/// Tests validation performed before a media file is modified.
/// </summary>
public sealed class MediaTagServiceTests
{
    /// <summary>
    /// Verifies that mutually exclusive cover options are rejected.
    /// </summary>
    [Fact]
    public void Validate_RejectsCoverAndRemoveCoverTogether()
    {
        var mediaPath = CreateTemporaryFile(".mp3");
        var coverPath = CreateTemporaryFile(".jpg");

        try
        {
            var options = new EditOptions
            {
                FilePath = mediaPath,
                CoverPath = coverPath,
                RemoveCover = true
            };

            var exception = Assert.Throws<ArgumentException>(() => new MediaTagService().Validate(options));

            Assert.Contains("--cover", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(mediaPath);
            File.Delete(coverPath);
        }
    }

    /// <summary>
    /// Verifies that a dry-run can validate a missing cover before any write occurs.
    /// </summary>
    [Fact]
    public void Validate_RejectsMissingCoverFile()
    {
        var mediaPath = CreateTemporaryFile(".mp3");
        var missingCover = Path.Combine(Path.GetTempPath(), $"missing-cover-{Guid.NewGuid():N}.jpg");

        try
        {
            var options = new EditOptions { FilePath = mediaPath, CoverPath = missingCover, DryRun = true };

            Assert.Throws<FileNotFoundException>(() => new MediaTagService().Validate(options));
        }
        finally
        {
            File.Delete(mediaPath);
        }
    }

    private static string CreateTemporaryFile(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"odmedia-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, []);
        return path;
    }
}
