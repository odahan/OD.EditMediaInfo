using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.ViewModels;

/// <summary>Owns navigation and pending edits independently of terminal rendering.</summary>
public sealed class EditorViewModel : ObservableObject, IDisposable
{
    private string search = "";
    private bool modifiedOnly;
    private string? coverPath;
    private bool removeCover;
    private CoverType coverType = CoverType.Front;
    private string coverDescription = "Front Cover";
    private readonly List<string> temporaryCovers = [];
    public MediaInfo Info { get; }
    public ObservableCollection<EditorField> Fields { get; } = [];
    public string Search { get => search; set => SetProperty(ref search, value); }
    public bool ModifiedOnly { get => modifiedOnly; set => SetProperty(ref modifiedOnly, value); }
    public bool KeepMtime { get; set; }
    public bool CreateBackup { get; set; } = true;
    public int PendingCount => Fields.Count(f => f.IsModified) + (coverPath is not null || removeCover ? 1 : 0);
    public string CoverAction => removeCover ? "Remove all artwork" : coverPath is not null ? $"Replace {coverType.ToString().ToLowerInvariant()} artwork: {coverPath}" : "Keep artwork";
    public string[] Sections => new[] { "Overview", "General", "Video", "Audio", "Image", "Capture", "Location", "Subtitles", "Chapters", "Metadata", "Images", "Color", "Advanced" }
        .Where(s => Fields.Any(f => f.Section == s)).ToArray();

    public EditorViewModel(MediaInfo info, IEnumerable<EditorField>? fields = null)
    {
        Info = info;
        if (fields is not null)
        {
            foreach (var field in fields) Add(field);
            return;
        }
        AddReadOnly("Overview", "Media type", info.Details.Kind);
        AddReadOnly("Overview", "File", info.FilePath);
        AddReadOnly("Overview", "Container", info.Details.Container);
        AddReadOnly("Overview", "File size", MediaProbeService.FormatSize(info.FileSize));
        if (info.Duration > TimeSpan.Zero) AddReadOnly("Overview", "Duration", $"{info.Duration.TotalSeconds:0.###} s ({info.Duration})");
        if (info.AudioSampleRate > 0) AddReadOnly("Audio", "Sample rate", $"{info.AudioSampleRate} Hz");
        if (info.AudioBitrate > 0) AddReadOnly("Audio", "Audio bitrate", $"{info.AudioBitrate} kb/s");
        if (info.AudioChannels > 0) AddReadOnly("Audio", "Channels", info.AudioChannels.ToString());
        if (info.AudioBitsPerSample > 0) AddReadOnly("Audio", "Bits per sample", info.AudioBitsPerSample.ToString());
        if (info.Details.CanWriteTags)
        {
            using var file = TagLib.File.Create(info.FilePath);
            foreach (var field in TagFieldCatalog.GetFields(file.Tag))
                Add(new(field.Id, field.Section, field.Label, field.Read(file.Tag), "TagLibSharp", field));
        }
        foreach (var property in info.Details.Properties)
        {
            if (property.Source == "TagLibSharp" && Fields.Any(f => f.Label == property.Name && f.Section == property.Section)) continue;
            AddReadOnly(property.Section, property.Name, property.Value, property.Source);
        }
        for (int index = 0; index < info.Pictures.Count; index++)
        {
            var picture = info.Pictures[index];
            AddReadOnly("Images", $"Artwork {index + 1}", $"{picture.Type} · {picture.MimeType} · {picture.Width} × {picture.Height} · {MediaProbeService.FormatSize(picture.DataLength)} · {picture.Description}");
        }
        foreach (var diagnostic in info.Details.Diagnostics) AddReadOnly("Overview", "Analysis notice", diagnostic, "Reader");
        if (!info.Details.CanWriteTags) AddReadOnly("Overview", "Editing", "Inspection only: this file has no supported metadata writer.");
    }

    private void Add(EditorField field)
    {
        Fields.Add(field);
        field.PropertyChanged += (_, _) => OnPropertyChanged(nameof(PendingCount));
    }

    private void AddReadOnly(string section, string label, string? value, string source = "File") =>
        Add(new($"read:{Fields.Count}", section, label, value, source));

    /// <summary>Refreshes sampled cadence results while retaining all pending metadata edits.</summary>
    public void RefreshCadenceResults()
    {
        foreach (var field in Fields.Where(f => f.Label.EndsWith("Cadence mode", StringComparison.Ordinal)).ToArray()) Fields.Remove(field);
        foreach (var field in Fields.Where(f => f.Id.StartsWith("analysis:", StringComparison.Ordinal)).ToArray()) Fields.Remove(field);
        foreach (var property in Info.Details.Properties.Where(p => p.Name.EndsWith("Cadence sample", StringComparison.Ordinal)))
            Add(new("analysis:" + property.Name, property.Section, property.Name, property.Value, property.Source));
    }

    /// <summary>Searches all sections when a query is active, retaining the pending edits across filters.</summary>
    public IReadOnlyList<EditorField> VisibleFields(string section) => Fields.Where(f =>
        (string.IsNullOrWhiteSpace(Search) ? ModifiedOnly || f.Section == section :
            $"{f.Section} {f.Label} {f.CurrentValue} {f.Replacement} {f.Source}".Contains(Search, StringComparison.OrdinalIgnoreCase)) &&
        (!ModifiedOnly || f.IsModified)).ToArray();

    /// <summary>Stages artwork without touching the media file.</summary>
    public void StageCover(string? path, bool remove = false, CoverType type = CoverType.Front, string description = "Front Cover")
    {
        if (!Info.Details.CanWriteTags || Info.Details.Kind == "Photo") throw new InvalidOperationException("Artwork editing is unavailable for this file.");
        new MediaTagService().Validate(new EditOptions { FilePath = Info.FilePath, CoverPath = path, RemoveCover = remove });
        coverPath = path;
        removeCover = remove;
        coverType = type;
        coverDescription = description;
        OnPropertyChanged(nameof(PendingCount));
    }

    /// <summary>Stages preset fields in the same review grid and owns any temporary cover until disposal.</summary>
    public void ApplyPreset(PresetFile preset)
    {
        if (!Info.Details.CanWriteTags || Info.Details.Kind == "Photo") throw new InvalidOperationException("Audio/video presets cannot be applied to this file.");
        var options = PresetService.ToEditOptions(preset, Info.FilePath);
        if (options.CoverPath is not null) temporaryCovers.Add(options.CoverPath);
        var changes = new Dictionary<string, string?>
        {
            ["Performers"] = options.Artists is null ? null : string.Join("; ", options.Artists),
            ["Album"] = options.Album,
            ["AlbumArtists"] = options.AlbumArtists is null ? null : string.Join("; ", options.AlbumArtists),
            ["Genres"] = options.Genres is null ? null : string.Join("; ", options.Genres),
            ["Year"] = options.Year?.ToString(),
            ["Composers"] = options.Composers is null ? null : string.Join("; ", options.Composers),
            ["Comment"] = options.Comment,
            ["Copyright"] = options.Copyright,
            ["Publisher"] = options.Publisher
        };
        foreach (var change in changes.Where(c => c.Value is not null))
        {
            var field = Fields.First(f => f.Id == change.Key);
            if (change.Value!.Length == 0) field.Remove(); else field.Replace(change.Value);
        }
        if (options.CoverPath is not null) StageCover(options.CoverPath, type: options.CoverType ?? CoverType.Front, description: options.CoverDescription ?? "Front Cover");
    }

    /// <summary>Builds one save request, preserving every field absent from the change dictionary.</summary>
    public EditOptions ToEditOptions() => new()
    {
        FilePath = Info.FilePath,
        FieldChanges = Fields.Where(f => f.IsModified).ToDictionary(f => f.Id, f => f.Action == "Remove" ? null : f.Replacement),
        CoverPath = coverPath,
        RemoveCover = removeCover,
        NoBackup = !CreateBackup,
        KeepMtime = KeepMtime,
        CoverType = coverType,
        CoverDescription = coverPath is null ? null : coverDescription
    };

    /// <summary>Discards staged metadata and artwork changes.</summary>
    public void RevertAll()
    {
        foreach (var field in Fields) field.Revert();
        coverPath = null;
        removeCover = false;
        OnPropertyChanged(nameof(PendingCount));
    }

    /// <summary>Deletes only cover files created for preset previews.</summary>
    public void Dispose()
    {
        foreach (var path in temporaryCovers) if (File.Exists(path)) File.Delete(path);
    }
}
