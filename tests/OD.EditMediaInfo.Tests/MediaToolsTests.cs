using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.Tests;

/// <summary>Validates the optional installer without downloading or executing software or changing the registry.</summary>
public sealed class MediaToolsTests
{
    /// <summary>Installs only the expected files and preserves an existing analyzer executable.</summary>
    [Fact]
    public async Task Installer_VerifiesArchiveAndPreservesExistingTools()
    {
        using var folder = new TemporaryFolder();
        await File.WriteAllTextAsync(Path.Combine(folder.Path, "ffmpeg.exe"), "existing");
        var archive = CreateArchive();
        using var client = new HttpClient(new DownloadHandler(archive, Convert.ToHexString(SHA256.HashData(archive))));
        await MediaToolsService.InstallAnalyzerCoreAsync(folder.Path, client);
        Assert.Equal("probe", await File.ReadAllTextAsync(Path.Combine(folder.Path, "ffprobe.exe")));
        Assert.Equal("existing", await File.ReadAllTextAsync(Path.Combine(folder.Path, "ffmpeg.exe")));
        Assert.True(File.Exists(Path.Combine(folder.Path, "ffmpeg-source.txt")));
        Assert.True(File.Exists(Path.Combine(folder.Path, "ffmpeg-LICENSE.txt")));
        Assert.False(File.Exists(Path.Combine(folder.Path, "ffplay.exe")));
        Assert.DoesNotContain(Directory.GetFiles(folder.Path), p => p.EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>Rejects checksum mismatches and leaves the target folder empty.</summary>
    [Fact]
    public async Task Installer_RejectsMismatchedChecksum()
    {
        using var folder = new TemporaryFolder();
        using var client = new HttpClient(new DownloadHandler(CreateArchive(), new string('0', 64)));
        await Assert.ThrowsAsync<IOException>(() => MediaToolsService.InstallAnalyzerCoreAsync(folder.Path, client));
        Assert.Empty(Directory.GetFiles(folder.Path));
    }

    /// <summary>Requires both tools before committing any extracted executable.</summary>
    [Fact]
    public async Task Installer_MissingEntryDoesNotPartiallyInstall()
    {
        using var folder = new TemporaryFolder();
        var archive = CreateArchive(includeFFmpeg: false);
        using var client = new HttpClient(new DownloadHandler(archive, Convert.ToHexString(SHA256.HashData(archive))));
        await Assert.ThrowsAsync<IOException>(() => MediaToolsService.InstallAnalyzerCoreAsync(folder.Path, client));
        Assert.Empty(Directory.GetFiles(folder.Path));
    }

    /// <summary>Preserves quoted executable and file paths without introducing cmd.exe expansion.</summary>
    [Fact]
    public void ExplorerCommand_QuotesBothPaths()
    {
        Assert.Equal("\"E:\\Media tools & photos\\odmedia.exe\" \"%1\"", MediaToolsService.ExplorerCommand(@"E:\Media tools & photos\odmedia.exe"));
    }

    /// <summary>Targets the actual executable directory rather than a single-file extraction directory.</summary>
    [Fact]
    public void SingleFile_UsesExecutableLocationForInstalledTools()
    {
        Assert.Equal(@"E:\Media tools", MediaToolsService.GetExecutableDirectory(@"E:\Media tools\odmedia.exe", @"C:\Temp\.net\odmedia\hash"));
        Assert.Equal(@"E:\tests", MediaToolsService.GetExecutableDirectory(@"C:\Program Files\dotnet\dotnet.exe", @"E:\tests"));
    }

    private static byte[] CreateArchive(bool includeFFmpeg = true)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string contents)
            {
                using var writer = new StreamWriter(archive.CreateEntry("ffmpeg-release/" + name).Open());
                writer.Write(contents);
            }
            Add("bin/ffprobe.exe", "probe");
            if (includeFFmpeg) Add("bin/ffmpeg.exe", "ffmpeg");
            Add("bin/ffplay.exe", "not installed");
            Add("LICENSE", "Test license");
        }
        return stream.ToArray();
    }

    private sealed class DownloadHandler(byte[] archive, string checksum) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("www.gyan.dev", request.RequestUri!.Host);
            HttpContent content = request.RequestUri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                ? new StringContent(checksum + "  ffmpeg.zip") : new ByteArrayContent(archive);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"odmedia-tools-{Guid.NewGuid():N}");
        public TemporaryFolder() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
