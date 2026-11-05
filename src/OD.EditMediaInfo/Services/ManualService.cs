using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OD.EditMediaInfo.Services;

/// <summary>Extracts the embedded offline manuals beside the real executable and opens the selected language.</summary>
public static class ManualService
{
    /// <summary>Resolves an explicit language or selects French/English from the operating system UI culture.</summary>
    public static string ResolveLanguage(string? language) => ResolveLanguage(language, CultureInfo.CurrentUICulture);

    internal static string ResolveLanguage(string? language, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(language)) return culture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
        var normalized = language.Trim().ToLowerInvariant();
        return normalized is "en" or "fr" ? normalized : throw new ArgumentException("Supported manual languages: en, fr.");
    }

    /// <summary>Extracts both language files so the manual's language links work offline.</summary>
    public static string Extract(string? language = null) => ExtractTo(MediaToolsService.ExecutableDirectory, ResolveLanguage(language));

    internal static string ExtractTo(string directory, string language)
    {
        language = ResolveLanguage(language);
        try
        {
            foreach (var code in new[] { "en", "fr" })
            {
                using var resource = typeof(ManualService).Assembly.GetManifestResourceStream($"OD.EditMediaInfo.Manual.{code}.html")
                    ?? throw new InvalidOperationException($"The embedded {code} manual is missing.");
                using var reader = new StreamReader(resource, Encoding.UTF8);
                var content = reader.ReadToEnd();
                var path = Path.Combine(directory, $"odmedia-manual-{code}.html");
                if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == content) continue;
                var temporary = Path.Combine(directory, $".odmedia-manual-{code}-{Guid.NewGuid():N}.tmp");
                try
                {
                    File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    File.Move(temporary, path, overwrite: true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new IOException($"Cannot extract the manual beside odmedia.exe in \"{directory}\". Move the executable to a writable folder and retry. {ex.Message}", ex);
        }
        return Path.Combine(directory, $"odmedia-manual-{language}.html");
    }

    /// <summary>Opens an extracted local HTML file using its default Windows application.</summary>
    public static void Open(string path)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    }
}
