using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using OD.EditMediaInfo.Models;
using OD.EditMediaInfo.Services;
using OD.EditMediaInfo.ViewModels;

namespace OD.EditMediaInfo.Gui;

internal sealed partial class EditorDialogs
{
    private sealed record PresetField(
        string Id, string Label, Func<PresetTags, string?> Read, Action<PresetTags, string> Write,
        Action<PresetTags, PresetTags> Copy);

    private static readonly PresetField[] PresetFields =
    [
        ListField("Performers", "Artists", tags => tags.Artists, (tags, value) => tags.Artists = value),
        TextField("Album", "Album", tags => tags.Album, (tags, value) => tags.Album = value),
        ListField("AlbumArtists", "Album artists", tags => tags.AlbumArtists, (tags, value) => tags.AlbumArtists = value),
        ListField("Genres", "Genres", tags => tags.Genres, (tags, value) => tags.Genres = value),
        DefineField<uint?>("Year", "Year", tags => tags.Year, (tags, value) => tags.Year = value,
            value => value?.ToString(CultureInfo.InvariantCulture), value => ParseYear(value)),
        ListField("Composers", "Composers", tags => tags.Composers, (tags, value) => tags.Composers = value),
        TextField("Comment", "Comment", tags => tags.Comment, (tags, value) => tags.Comment = value),
        TextField("Copyright", "Copyright", tags => tags.Copyright, (tags, value) => tags.Copyright = value),
        TextField("Publisher", "Publisher", tags => tags.Publisher, (tags, value) => tags.Publisher = value)
    ];

    private static PresetField DefineField<T>(string id, string label, Func<PresetTags, T> read,
        Action<PresetTags, T> write, Func<T, string?> format, Func<string, T> parse) =>
        new(id, label, tags => format(read(tags)), (tags, text) => write(tags, parse(text)),
            (source, target) => write(target, read(source)));

    private static PresetField TextField(string id, string label, Func<PresetTags, string?> read, Action<PresetTags, string?> write) =>
        DefineField(id, label, read, write, value => value, value => value);

    private static PresetField ListField(string id, string label, Func<PresetTags, string[]?> read, Action<PresetTags, string[]?> write) =>
        DefineField(id, label, read, write, Join, Split);

    /// <summary>Lists shared presets and previews, stages, or deletes the selected preset.</summary>
    public void ManagePresets(Action<PresetFile> stage)
    {
        var dialog = CreateDialog("Presets", 760);
        var layout = new DockPanel { Height = 330 };
        var names = new ListBox { Width = 220, Margin = new Thickness(0, 0, 16, 0) };
        AutomationProperties.SetName(names, "Preset names");
        DockPanel.SetDock(names, Dock.Left);
        layout.Children.Add(names);
        var choices = new StackPanel();
        var status = Message("");
        status.Margin = new Thickness(0, 0, 0, 8);
        var details = new DockPanel();
        DockPanel.SetDock(status, Dock.Top);
        details.Children.Add(status);
        details.Children.Add(new ScrollViewer { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        layout.Children.Add(details);

        PresetFile? selected = null;
        var selectedFields = new List<(PresetField Field, CheckBox Include)>();
        CheckBox? includeCover = null;
        var apply = AddButton(dialog, "_Stage preset", isDefault: true, action: () =>
        {
            try
            {
                var filtered = new PresetFile
                {
                    Name = selected!.Name,
                    Cover = includeCover?.IsChecked == true ? selected.Cover : null
                };
                foreach (var (field, include) in selectedFields)
                {
                    if (include.IsChecked == true)
                        field.Copy(selected.Tags, filtered.Tags);
                }
                stage(filtered);
                dialog.DialogResult = true;
            }
            catch (Exception ex)
            {
                status.Text = $"Could not stage this preset. {ex.Message}";
            }
        });
        var delete = AddButton(dialog, "_Delete…");
        delete.Click += (_, _) =>
        {
            if (names.SelectedItem is not string name ||
                !ConfirmPresetAction(dialog, "Delete preset", $"Delete the preset \"{name}\"?", "_Delete"))
                return;
            try
            {
                PresetService.Delete(name);
                LoadNames();
            }
            catch (Exception ex)
            {
                status.Text = $"Could not delete the preset. {ex.Message}";
            }
        };
        AddButton(dialog, "_Close", isCancel: true);

        names.SelectionChanged += (_, _) => LoadSelected();
        LoadNames();
        SetContent(dialog, layout);
        dialog.ShowDialog();

        void LoadNames()
        {
            try
            {
                names.ItemsSource = PresetService.List();
                if (names.Items.Count == 0)
                    LoadSelected();
                else
                    names.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                names.ItemsSource = null;
                LoadSelected();
                status.Text = $"Could not list presets. {ex.Message}";
            }
        }

        void LoadSelected()
        {
            selected = null;
            selectedFields.Clear();
            includeCover = null;
            choices.Children.Clear();
            apply.IsEnabled = false;
            delete.IsEnabled = names.SelectedItem is string;
            if (names.SelectedItem is not string name)
            {
                status.Text = "No presets found.";
                return;
            }
            try
            {
                selected = PresetService.Load(name);
                foreach (var field in PresetFields)
                {
                    if (field.Read(selected.Tags) is { } value)
                        selectedFields.Add((field, AddChoice(field.Label, value.Length == 0 ? "(remove)" : value)));
                }
                if (selected.Cover is { } cover)
                    includeCover = AddChoice("Artwork", $"{cover.Type} · {cover.MimeType} · {PresetService.GetCoverByteLength(cover) / 1024} KiB");
                UpdateApply();
                status.Text = apply.IsEnabled ? "Choose what to apply." : "This preset has no changes.";
            }
            catch (Exception ex)
            {
                status.Text = $"Could not read \"{name}\". {ex.Message}";
            }
        }

        CheckBox AddChoice(string label, string value)
        {
            var choice = new CheckBox { Content = Message($"{label}: {value}"), IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
            AutomationProperties.SetName(choice, label);
            AutomationProperties.SetHelpText(choice, value);
            choice.Checked += (_, _) => UpdateApply();
            choice.Unchecked += (_, _) => UpdateApply();
            choices.Children.Add(choice);
            return choice;
        }

        void UpdateApply() => apply.IsEnabled = includeCover?.IsChecked == true ||
            selectedFields.Any(choice => choice.Include.IsChecked == true);
    }

    /// <summary>Creates a preset from entered values or values saved in the current media file.</summary>
    public string? CreatePreset(EditorViewModel? source = null)
    {
        var dialog = CreateDialog(source is null ? "Create preset" : "Create preset from this file", 610);
        var form = new StackPanel();
        var name = AddPresetInput(form, "Preset name");
        form.Children.Add(Message("Choose what to save. Separate multiple artists, genres, or composers with semicolons."));
        var inputs = PresetFields.Select(field =>
        {
            var saved = source?.Fields.FirstOrDefault(row => row.Id == field.Id)?.CurrentValue;
            var include = new CheckBox { Content = field.Label, IsChecked = !string.IsNullOrWhiteSpace(saved), Margin = new Thickness(0, 6, 0, 2) };
            var input = new TextBox { Text = saved ?? "", Margin = new Thickness(22, 0, 0, 4) };
            AutomationProperties.SetLabeledBy(input, include);
            input.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = include });
            form.Children.Add(include);
            form.Children.Add(input);
            return (Field: field, Include: include, Input: input);
        }).ToArray();

        var includeArtwork = new CheckBox { Content = "Include artwork", Margin = new Thickness(0, 6, 0, 0) };
        var artwork = new ComboBox { Margin = new Thickness(0, 6, 0, 6) };
        if (source?.Info.Pictures.Count > 0)
            artwork.Items.Add("First embedded image");
        artwork.Items.Add("Image file");
        artwork.SelectedIndex = 0;
        AutomationProperties.SetName(artwork, "Artwork source");
        form.Children.Add(includeArtwork);
        var artworkOptions = new StackPanel { Margin = new Thickness(22, 0, 0, 0) };
        artworkOptions.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(CheckBox.IsChecked))
        {
            Source = includeArtwork,
            Converter = new BooleanToVisibilityConverter()
        });
        artworkOptions.Children.Add(artwork);
        form.Children.Add(artworkOptions);

        string? coverPath = null;
        var coverLabel = Message("");
        var browse = new Button { Content = "_Choose image…", HorizontalAlignment = HorizontalAlignment.Left };
        browse.Click += (_, _) =>
        {
            var picker = new OpenFileDialog
            {
                Title = "Choose preset artwork",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp"
            };
            if (picker.ShowDialog(dialog) == true)
            {
                coverPath = picker.FileName;
                coverLabel.Text = Path.GetFileName(coverPath);
            }
        };
        var imagePicker = new StackPanel { Visibility = artwork.SelectedItem is "Image file" ? Visibility.Visible : Visibility.Collapsed };
        imagePicker.Children.Add(browse);
        imagePicker.Children.Add(coverLabel);
        artwork.SelectionChanged += (_, _) =>
            imagePicker.Visibility = artwork.SelectedItem is "Image file" ? Visibility.Visible : Visibility.Collapsed;
        artworkOptions.Children.Add(imagePicker);

        var error = Message("");
        error.Foreground = (Brush)owner.FindResource("ErrorBrush");
        error.Margin = new Thickness(0, 8, 0, 0);
        var layout = new DockPanel();
        DockPanel.SetDock(error, Dock.Bottom);
        layout.Children.Add(error);
        layout.Children.Add(new ScrollViewer { Content = form, MaxHeight = 490, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        string? savedName = null;
        AddButton(dialog, "_Save preset", isDefault: true, action: () =>
        {
            try
            {
                var preset = new PresetFile
                {
                    Name = PresetService.SanitizeName(name.Text),
                    SourceFile = source?.Info.FilePath
                };
                foreach (var (field, include, input) in inputs)
                {
                    if (include.IsChecked != true)
                        continue;
                    if (string.IsNullOrWhiteSpace(input.Text))
                        throw new InvalidOperationException($"Enter a value for {field.Label} or clear its checkbox.");
                    field.Write(preset.Tags, input.Text.Trim());
                }
                preset.Cover = (includeArtwork.IsChecked == true ? artwork.SelectedItem : null) switch
                {
                    "First embedded image" => PresetService.CoverFromMediaInfo(source!.Info)
                        ?? throw new InvalidOperationException("The embedded image could not be read."),
                    "Image file" => PresetService.CoverFromFile(coverPath
                        ?? throw new InvalidOperationException("Choose an image file for the preset.")),
                    _ => null
                };
                if (preset.Tags.IsEmpty && preset.Cover is null)
                    throw new InvalidOperationException("Enter at least one tag or include artwork.");
                var overwrite = PresetService.Exists(preset.Name);
                if (overwrite && !ConfirmPresetAction(dialog, "Replace preset", $"Replace the preset \"{preset.Name}\"?", "_Replace"))
                    return;

                PresetService.Save(preset, overwrite);
                savedName = preset.Name;
                dialog.DialogResult = true;
            }
            catch (Exception ex)
            {
                error.Text = ex.Message;
            }
        });
        AddButton(dialog, "_Cancel", isCancel: true);
        dialog.Loaded += (_, _) => name.Focus();
        SetContent(dialog, layout);
        return dialog.ShowDialog() == true ? savedName : null;
    }

    private bool ConfirmPresetAction(Window parent, string title, string prompt, string label)
    {
        var dialog = CreateDialog(title, 450, parent);
        AddButton(dialog, label, action: () => dialog.DialogResult = true);
        AddButton(dialog, "_Cancel", isCancel: true, isDefault: true);
        SetContent(dialog, Message(prompt));
        return dialog.ShowDialog() == true;
    }

    private static TextBox AddPresetInput(Panel form, string label, string? value = null)
    {
        var input = new TextBox { Text = value ?? "", Margin = new Thickness(0, 0, 0, 4) };
        form.Children.Add(new Label { Content = label, Target = input, Padding = new Thickness(0, 4, 0, 2) });
        form.Children.Add(input);
        return input;
    }

    private static string? Join(string[]? values) => values is null ? null : string.Join("; ", values);
    private static string[] Split(string value)
    {
        var items = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return items.Length > 0 ? items : throw new ArgumentException("Enter at least one value before saving this field.");
    }

    private static uint ParseYear(string value) =>
        uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint year) && year <= 9999
            ? year
            : throw new ArgumentException("Enter a year from 0 to 9999.");
}
