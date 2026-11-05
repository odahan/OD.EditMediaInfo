using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace OD.EditMediaInfo.Services;

/// <summary>Installs the optional analyzer and manages per-user Explorer integration.</summary>
public static class MediaToolsService
{
    private const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string MenuKey = @"Software\Classes\*\shell\OD.EditMediaInfo";

    /// <summary>Gets the real executable folder even when the single-file runtime extracts bundled content.</summary>
    public static string ExecutableDirectory => GetExecutableDirectory(Environment.ProcessPath, AppContext.BaseDirectory);

    internal static string GetExecutableDirectory(string? processPath, string fallbackDirectory) =>
        processPath is not null && Path.GetFileNameWithoutExtension(processPath).Equals("odmedia", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(Path.GetFullPath(processPath))! : fallbackDirectory;

    /// <summary>Downloads and verifies the Windows essentials archive, then installs only the two executables.</summary>
    public static async Task InstallAnalyzerAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The automatic installer supports Windows x64.");
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        await InstallAnalyzerCoreAsync(ExecutableDirectory, client, progress, cancellationToken);
    }

    /// <summary>Installs into a specified folder with an injectable download client for isolated validation.</summary>
    internal static async Task InstallAnalyzerCoreAsync(string directory, HttpClient client, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(Path.Combine(directory, "ffprobe.exe")) && File.Exists(Path.Combine(directory, "ffmpeg.exe")))
        {
            progress?.Report("FFmpeg and FFprobe are already installed beside odmedia.exe.");
            return;
        }
        var probe = Path.Combine(directory, $".odmedia-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(probe, "write check", cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException("The executable folder is not writable. Move odmedia.exe to a writable folder and retry.", ex);
        }
        finally { if (File.Exists(probe)) File.Delete(probe); }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        var archivePath = Path.Combine(Path.GetTempPath(), $"odmedia-{Guid.NewGuid():N}.zip");
        var staged = new List<(string Temporary, string Target)>();
        var installed = new List<string>();
        try
        {
            progress?.Report("Downloading FFmpeg essentials from gyan.dev...");
            var checksumText = await client.GetStringAsync(DownloadUrl + ".sha256", token);
            var expected = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) throw new IOException("Invalid SHA-256 checksum from the download server.");
            using (var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(token);
                await using var target = File.Create(archivePath);
                var buffer = new byte[81920];
                long total = 0;
                long nextProgress = 10L * 1024 * 1024;
                int count;
                while ((count = await source.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > 300L * 1024 * 1024) throw new IOException("Download exceeds the 300 MB limit.");
                    await target.WriteAsync(buffer.AsMemory(0, count), token);
                    if (total >= nextProgress)
                    {
                        progress?.Report($"Downloaded {total / 1048576} MiB...");
                        nextProgress += 10L * 1024 * 1024;
                    }
                }
            }
            await using (var stream = File.OpenRead(archivePath))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Download checksum mismatch. No tools were installed; retry the download.");
            }
            progress?.Report("Checksum verified. Installing ffprobe.exe and ffmpeg.exe...");
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var name in new[] { "ffprobe.exe", "ffmpeg.exe" })
            {
                var entry = archive.Entries.SingleOrDefault(e => e.FullName.EndsWith("/bin/" + name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new IOException($"The verified archive does not contain {name}.");
                if (entry.Length > 250L * 1024 * 1024) throw new IOException("Unexpected executable size.");
                var target = Path.Combine(directory, name);
                if (File.Exists(target)) continue;
                var temporary = Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");
                staged.Add((temporary, target));
                await using var output = File.Create(temporary);
                await using var input = entry.Open();
                await input.CopyToAsync(output, token);
            }
            foreach (var item in staged)
            {
                File.Move(item.Temporary, item.Target, overwrite: false);
                installed.Add(item.Target);
            }
            var license = archive.Entries.FirstOrDefault(e => e.Name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase) || e.Name.Equals("LICENSE.txt", StringComparison.OrdinalIgnoreCase));
            if (license is not null && license.Length < 1024 * 1024)
            {
                using var reader = new StreamReader(license.Open());
                await File.WriteAllTextAsync(Path.Combine(directory, "ffmpeg-LICENSE.txt"), await reader.ReadToEndAsync(token), token);
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "ffmpeg-source.txt"),
                $"FFmpeg Windows essentials build provided by https://www.gyan.dev/ffmpeg/builds/\nDownloaded from {DownloadUrl}\nSHA-256: {expected}\nFFmpeg license and source information: https://ffmpeg.org/legal.html\nBuild source and license: https://www.gyan.dev/ffmpeg/builds/\n", token);
            progress?.Report("Media analyzer installed beside odmedia.exe. No PATH changes were made.");
        }
        catch
        {
            foreach (var path in installed) File.Delete(path);
            throw;
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
            foreach (var item in staged) if (File.Exists(item.Temporary)) File.Delete(item.Temporary);
        }
    }

    /// <summary>Registers a quoted, single-file Explorer command for the current user only.</summary>
    public static void InstallExplorerMenu()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Explorer integration requires Windows.");
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate odmedia.exe.");
        if (!Path.GetFileNameWithoutExtension(executable).Equals("odmedia", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run this command using odmedia.exe, rather than dotnet.");
        using var key = Registry.CurrentUser.CreateSubKey(MenuKey);
        key.SetValue("", "Inspect metadata with odmedia");
        key.SetValue("Icon", $"\"{executable}\",0");
        key.SetValue("MultiSelectModel", "Single");
        using var command = key.CreateSubKey("command");
        command.SetValue("", ExplorerCommand(executable));
    }

    /// <summary>Builds the Explorer command without a shell, preserving spaces and special characters in paths.</summary>
    internal static string ExplorerCommand(string executable) => $"\"{executable}\" \"%1\"";

    /// <summary>Removes only the Explorer command owned by this application.</summary>
    public static void RemoveExplorerMenu()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Explorer integration requires Windows.");
        Registry.CurrentUser.DeleteSubKeyTree(MenuKey, throwOnMissingSubKey: false);
    }
}
