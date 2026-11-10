using CommunityToolkit.Mvvm.ComponentModel;
using OD.EditMediaInfo.Services;

namespace OD.EditMediaInfo.ViewModels;

/// <summary>Represents one grid row and an explicit keep, replace, or remove operation.</summary>
public sealed class EditorField : ObservableObject
{
    private string? replacement;
    private string action = "Keep";
    private bool captureEditingEnabled;
    public string Id { get; }
    public string Section { get; }
    public string Label { get; }
    public string CurrentValue { get; }
    public string Source { get; }
    public TagField? Definition { get; }
    public bool IsEditable => Definition is not null && (Section is not ("Capture" or "Location") || captureEditingEnabled);
    public string Action { get => action; private set => SetProperty(ref action, value); }
    public string? Replacement { get => replacement; private set => SetProperty(ref replacement, value); }
    public bool IsModified => Action != "Keep";

    public EditorField(string id, string section, string label, string? currentValue, string source, TagField? definition = null)
    {
        Id = id;
        Section = section;
        Label = label;
        CurrentValue = currentValue ?? "";
        Source = source;
        Definition = definition;
    }

    /// <summary>Stages a valid replacement; empty input preserves the current pending operation.</summary>
    public void Replace(string value)
    {
        if (!IsEditable) throw new InvalidOperationException("This property is read-only.");
        if (string.IsNullOrWhiteSpace(value)) return;
        Definition!.Parse(value);
        if (value == CurrentValue) { Revert(); return; }
        Replacement = value;
        Action = "Replace";
        OnPropertyChanged(nameof(IsModified));
    }

    /// <summary>Stages an explicit removal rather than treating whitespace as a deletion command.</summary>
    public void Remove()
    {
        if (!IsEditable) throw new InvalidOperationException("This property is read-only.");
        Definition!.Parse(null);
        Replacement = null;
        Action = string.IsNullOrEmpty(CurrentValue) ? "Keep" : "Remove";
        OnPropertyChanged(nameof(IsModified));
    }

    /// <summary>Discards the pending operation for this field.</summary>
    public void Revert()
    {
        Replacement = null;
        Action = "Keep";
        OnPropertyChanged(nameof(IsModified));
    }

    /// <summary>Explicitly enables editing of supported capture and GPS fields for this session.</summary>
    public void EnableCaptureEditing()
    {
        captureEditingEnabled = true;
        OnPropertyChanged(nameof(IsEditable));
    }
}
