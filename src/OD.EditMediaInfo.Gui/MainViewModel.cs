using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OD.EditMediaInfo.Services;
using OD.EditMediaInfo.ViewModels;

namespace OD.EditMediaInfo.Gui;

/// <summary>Coordinates the Windows workspace and delegates metadata operations to the shared editor.</summary>
internal sealed partial class MainViewModel(EditorDialogs dialogs) : ObservableObject, IDisposable
{
    private readonly MediaTagService media = new();
    private EditorViewModel? editor;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string status = "Open a media file or drop it here.";

    [ObservableProperty]
    private string search = "";

    [ObservableProperty]
    private bool pendingOnly;

    [ObservableProperty]
    private string? selectedSection;

    [ObservableProperty]
    private EditorField? selectedField;

    [ObservableProperty]
    private IReadOnlyList<EditorField> rows = [];

    public string FileName => editor is null ? "No file open" : Path.GetFileName(editor.Info.FilePath);
    public string FilePath => editor?.Info.FilePath ?? "";
    public string[] Sections => editor?.Sections ?? [];
    public int PendingCount => editor?.PendingCount ?? 0;
    public bool CanEditCapture => editor is not null && editor.Fields.Any(field => field.Definition is not null && !field.IsEditable);
    public bool ShowPresetCommands => editor is not null && editor.Info.Details.CanWriteTags &&
        editor.Info.Details.Kind != "Photo" &&
        editor.Fields.Any(field => field.Section == SelectedSection && field.IsEditable);
    public string? PendingArtwork => editor?.PendingCoverAction;

    partial void OnIsBusyChanged(bool value) => UpdateCommands();
    partial void OnSelectedFieldChanged(EditorField? value) => UpdateCommands();
    partial void OnSelectedSectionChanged(string? value) => RefreshRows();
    partial void OnSearchChanged(string value) => RefreshRows();
    partial void OnPendingOnlyChanged(bool value) => RefreshRows();

    private bool IsIdle() => !IsBusy;
    private bool CanSave() => IsIdle() && PendingCount > 0;
    private bool CanExport() => IsIdle() && editor is not null;
    private bool CanEditField() => IsIdle() && SelectedField?.IsEditable == true;
    private bool CanRevertField() => IsIdle() && SelectedField?.IsModified == true;
    private bool CanUnlockCapture() => IsIdle() && CanEditCapture;
    private bool CanManagePresets() => IsIdle() && ShowPresetCommands;
    private bool CanRevertArtwork() => IsIdle() && PendingArtwork is not null;

    [RelayCommand(CanExecute = nameof(CanManagePresets))]
    private void ManagePresets()
    {
        dialogs.ManagePresets(preset =>
        {
            editor!.ApplyPreset(preset);
            RefreshRows();
            Status = $"Preset staged: {preset.Name}.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanManagePresets))]
    private void CreatePresetFromFile()
    {
        if (dialogs.CreatePreset(editor) is { } name)
            Status = $"Preset saved: {name}.";
    }

    [RelayCommand(CanExecute = nameof(CanManagePresets))]
    private void CreatePreset()
    {
        if (dialogs.CreatePreset() is { } name)
            Status = $"Preset saved: {name}.";
    }

    [RelayCommand(CanExecute = nameof(CanRevertArtwork))]
    private void RevertArtwork()
    {
        editor!.RevertCover();
        RefreshRows();
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task OpenAsync()
    {
        if (dialogs.ChooseFile() is { } path)
            await OpenFileAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(MediaExportFormat format)
    {
        var info = editor!.Info;
        if (dialogs.ChooseExportFile(info.FilePath, format) is not { } path)
            return;

        IsBusy = true;
        Status = "Exporting saved inspection values…";
        try
        {
            var overwrite = File.Exists(path);
            await Task.Run(() => MediaExportService.Save(path, info, format, overwrite));
            Status = $"Report saved: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Status = "Could not export the inspection report.";
            dialogs.ShowError("Export failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task RegisterExplorerAsync()
    {
        IsBusy = true;
        Status = "Updating the Explorer context menu…";
        try
        {
            await Task.Run(MediaToolsService.InstallExplorerMenu);
            Status = "Explorer now opens supported files in this app. Look under Show more options.";
        }
        catch (Exception ex)
        {
            Status = "Could not update the Explorer context menu.";
            dialogs.ShowError("Unable to register with Explorer", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Inspects the candidate before replacing the active document and its pending edits.</summary>
    public async Task OpenFileAsync(string path)
    {
        if (IsBusy)
            return;

        EditorViewModel? candidate = null;
        try
        {
            IsBusy = true;
            Status = "Inspecting media…";
            candidate = await Task.Run(() => ReadEditor(path));
            bool reopenAfterSave = PendingCount > 0 &&
                string.Equals(candidate.Info.FilePath, FilePath, StringComparison.OrdinalIgnoreCase);

            if (!await ConfirmLeaveAsync())
            {
                if (Status == "Inspecting media…")
                    Status = "Open canceled.";
                return;
            }

            if (reopenAfterSave && PendingCount == 0)
            {
                candidate.Dispose();
                candidate = await Task.Run(() => ReadEditor(path));
            }

            SetEditor(candidate);
            candidate = null;
            Status = editor!.Info.Details.CanWriteTags ? "File opened." : "File opened for inspection only.";
        }
        catch (Exception ex)
        {
            Status = "Could not open the file.";
            dialogs.ShowError("Unable to open media", ex.Message);
        }
        finally
        {
            candidate?.Dispose();
            IsBusy = false;
        }
    }

    /// <summary>Prompts before leaving the active file and returns whether navigation may proceed.</summary>
    public async Task<bool> ConfirmLeaveAsync()
    {
        if (PendingCount == 0)
            return true;

        return dialogs.ConfirmLeave(FileName, PendingCount) switch
        {
            LeaveChoice.Save => await ReviewAndSaveAsync(),
            LeaveChoice.Discard => true,
            _ => false
        };
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await ReviewAndSaveAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ReviewAndSaveAsync()
    {
        var current = editor!;
        var changes = current.Fields.Where(field => field.IsModified).ToArray();
        if (!dialogs.ReviewChanges(FileName, changes, PendingArtwork, current.Info.Pictures.Count))
            return false;

        var options = current.ToEditOptions();
        Status = "Saving metadata…";
        try
        {
            await Task.Run(() => media.Save(options));
        }
        catch (Exception ex)
        {
            Status = "Save failed. Pending changes were retained.";
            dialogs.ShowError("Unable to save metadata", ex.Message);
            return false;
        }

        try
        {
            var updated = await Task.Run(() => ReadEditor(options.FilePath));
            if (current.Fields.Any(field => field.Section is "Capture" or "Location" && field.IsEditable))
                EnableCaptureFields(updated);
            SetEditor(updated, preserveFilters: true);
            Status = "Metadata saved.";
        }
        catch (Exception ex)
        {
            SetEditor(null);
            Status = "Metadata saved. Reopen the file to refresh its values.";
            dialogs.ShowError("Unable to refresh saved metadata", ex.Message);
        }
        return true;
    }

    private EditorViewModel ReadEditor(string path)
    {
        var info = media.Read(path);
        if ((File.GetAttributes(info.FilePath) & FileAttributes.ReadOnly) != 0)
        {
            info.Details.CanWriteTags = false;
            info.Details.CanWritePhoto = false;
            info.Details.Diagnostics.Add("Windows marks this file as read-only.");
        }
        return new EditorViewModel(info);
    }

    [RelayCommand(CanExecute = nameof(CanEditField))]
    private void EditField()
    {
        if (dialogs.EditField(SelectedField!))
            RefreshRows();
    }

    [RelayCommand(CanExecute = nameof(CanEditField))]
    private void RemoveField()
    {
        try
        {
            SelectedField!.Remove();
            RefreshRows();
        }
        catch (Exception ex)
        {
            dialogs.ShowError("Unable to remove field", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevertField))]
    private void RevertField()
    {
        SelectedField!.Revert();
        RefreshRows();
    }

    [RelayCommand(CanExecute = nameof(CanUnlockCapture))]
    private void EnableCaptureEditing()
    {
        EnableCaptureFields(editor!);
        OnPropertyChanged(nameof(CanEditCapture));
        UpdateCommands();
    }

    private static void EnableCaptureFields(EditorViewModel model)
    {
        foreach (var field in model.Fields.Where(field => field.Definition is not null && !field.IsEditable))
            field.EnableCaptureEditing();
    }

    private void SetEditor(EditorViewModel? value, bool preserveFilters = false)
    {
        var section = preserveFilters ? SelectedSection : null;
        editor?.Dispose();
        editor = value;
        SelectedField = null;
        if (!preserveFilters)
        {
            Search = "";
            PendingOnly = false;
        }
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(Sections));
        OnPropertyChanged(nameof(CanEditCapture));
        SelectedSection = Sections.Contains(section) ? section : Sections.FirstOrDefault();
        RefreshRows();
    }

    private void RefreshRows()
    {
        var selected = SelectedField;
        if (editor is not null)
        {
            editor.Search = Search;
            editor.ModifiedOnly = PendingOnly;
        }
        Rows = editor?.VisibleFields(SelectedSection ?? "") ?? [];
        SelectedField = selected is not null && Rows.Contains(selected) ? selected : null;
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(PendingArtwork));
        OnPropertyChanged(nameof(ShowPresetCommands));
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        OpenCommand.NotifyCanExecuteChanged();
        RegisterExplorerCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        EditFieldCommand.NotifyCanExecuteChanged();
        RemoveFieldCommand.NotifyCanExecuteChanged();
        RevertFieldCommand.NotifyCanExecuteChanged();
        EnableCaptureEditingCommand.NotifyCanExecuteChanged();
        ManagePresetsCommand.NotifyCanExecuteChanged();
        CreatePresetFromFileCommand.NotifyCanExecuteChanged();
        CreatePresetCommand.NotifyCanExecuteChanged();
        RevertArtworkCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Releases the active editing session.</summary>
    public void Dispose() => editor?.Dispose();
}
