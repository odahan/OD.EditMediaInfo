using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using OD.EditMediaInfo.Services;
using OD.EditMediaInfo.ViewModels;

namespace OD.EditMediaInfo.Gui;

internal enum LeaveChoice { Cancel, Save, Discard }

/// <summary>Shows owned dialogs for file selection, field editing, and save decisions.</summary>
internal sealed partial class EditorDialogs(Window owner)
{
    public string? ChooseFile()
    {
        var picker = new OpenFileDialog
        {
            Title = "Open media",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true
        };
        return picker.ShowDialog(owner) == true ? picker.FileName : null;
    }

    /// <summary>Prompts for the location of a report in the selected format.</summary>
    public string? ChooseExportFile(string mediaPath, MediaExportFormat format)
    {
        var extension = MediaExportService.GetExtension(format);
        var label = format.ToString().ToUpperInvariant();
        var picker = new SaveFileDialog
        {
            Title = $"Export {label} inspection",
            FileName = Path.GetFileName(mediaPath) + extension,
            InitialDirectory = Path.GetDirectoryName(mediaPath),
            DefaultExt = extension,
            Filter = $"{label} report (*{extension})|*{extension}",
            AddExtension = true,
            OverwritePrompt = true
        };
        return picker.ShowDialog(owner) == true ? picker.FileName : null;
    }

    public bool EditField(EditorField field)
    {
        var dialog = CreateDialog($"Edit {field.Label}", 540);
        var value = new TextBox
        {
            Text = field.Replacement ?? (field.Action == "Remove" ? "" : field.CurrentValue),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 110,
            Margin = new Thickness(0, 8, 0, 8)
        };
        var error = Message("");
        error.Foreground = (Brush)owner.FindResource("ErrorBrush");
        var content = new StackPanel();
        content.Children.Add(Message("Enter a new value. Use Remove value in the main window to delete it."));
        content.Children.Add(value);
        content.Children.Add(error);
        dialog.Loaded += (_, _) =>
        {
            value.Focus();
            value.SelectAll();
        };
        AddButton(dialog, "_Apply", isDefault: true, action: () =>
        {
            try
            {
                field.Replace(value.Text);
                dialog.DialogResult = true;
            }
            catch (Exception ex)
            {
                error.Text = ex.Message;
            }
        });
        AddButton(dialog, "_Cancel", isCancel: true);
        SetContent(dialog, content);
        return dialog.ShowDialog() == true;
    }

    public bool ReviewChanges(string fileName, IReadOnlyList<EditorField> changes, string? pendingArtwork, int pictureCount)
    {
        var dialog = CreateDialog("Review metadata changes", 820);
        var content = new DockPanel();
        var description = Message(fileName);
        description.Margin = new Thickness(0, 0, 0, 12);
        DockPanel.SetDock(description, Dock.Top);
        content.Children.Add(description);
        var rows = changes.Select(field => new ReviewRow(
            $"{field.Section} / {field.Label}", field.CurrentValue,
            field.Action == "Remove" ? "(removed)" : field.Replacement)).ToList();
        if (pendingArtwork is not null)
            rows.Add(new ReviewRow("Images / Artwork", $"{pictureCount} embedded image(s)", pendingArtwork));
        var grid = new DataGrid
        {
            Style = (Style)owner.FindResource("MetadataGrid"),
            ItemsSource = rows,
            MaxHeight = 340,
            MinHeight = 95,
            Margin = new Thickness(0, 0, 0, 2)
        };
        AddReviewColumn(grid, "Field", "Field", 1, "GridValue");
        AddReviewColumn(grid, "Before", "Before", 1.5, "GridValue");
        AddReviewColumn(grid, "After", "After", 1.5, "PendingValue");
        content.Children.Add(grid);
        AddButton(dialog, "_Save", isDefault: true, action: () => dialog.DialogResult = true);
        AddButton(dialog, "_Cancel", isCancel: true);
        SetContent(dialog, content);
        return dialog.ShowDialog() == true;
    }

    public LeaveChoice ConfirmLeave(string fileName, int pendingCount)
    {
        var dialog = CreateDialog("Unsaved changes", 460);
        var choice = LeaveChoice.Cancel;
        AddButton(dialog, "_Save", action: () => { choice = LeaveChoice.Save; dialog.DialogResult = true; });
        AddButton(dialog, "_Discard", action: () => { choice = LeaveChoice.Discard; dialog.DialogResult = true; });
        AddButton(dialog, "_Cancel", isCancel: true, isDefault: true);
        SetContent(dialog, Message($"Save the {pendingCount} pending changes to {fileName}?"));
        dialog.ShowDialog();
        return choice;
    }

    public void ShowError(string title, string message)
    {
        var dialog = CreateDialog(title, 600);
        AddButton(dialog, "_OK", isDefault: true, isCancel: true, action: () => dialog.DialogResult = true);
        SetContent(dialog, new ScrollViewer { Content = Message(message), MaxHeight = 320 });
        dialog.ShowDialog();
    }

    private static TextBlock Message(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private Window CreateDialog(string title, double width, Window? parent = null)
    {
        var layout = new DockPanel { Margin = new Thickness(20) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);

        var dialog = new Window
        {
            Owner = parent ?? owner,
            Title = title,
            Icon = owner.Icon,
            Width = width,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)owner.FindResource("WindowBrush"),
            Foreground = (Brush)owner.FindResource("TextBrush"),
            Content = layout
        };
        DarkWindowChrome.Attach(dialog);
        return dialog;
    }

    private static void SetContent(Window dialog, UIElement content) =>
        ((DockPanel)dialog.Content).Children.Add(content);

    private static Button AddButton(Window dialog, string label, bool isDefault = false, bool isCancel = false, Action? action = null)
    {
        var button = new Button
        {
            Content = label,
            IsDefault = isDefault,
            IsCancel = isCancel,
            MinWidth = 84,
            Margin = new Thickness(8, 0, 0, 0)
        };
        if (action is not null)
            button.Click += (_, _) => action();
        var buttons = (StackPanel)((DockPanel)dialog.Content).Children[0];
        buttons.Children.Add(button);
        return button;
    }

    private void AddReviewColumn(DataGrid grid, string heading, string binding, double width, string style)
    {
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = heading,
            Binding = new Binding(binding),
            Width = new DataGridLength(width, DataGridLengthUnitType.Star),
            ElementStyle = (Style)owner.FindResource(style)
        });
    }

    private sealed record ReviewRow(string Field, string Before, string? After);
}
