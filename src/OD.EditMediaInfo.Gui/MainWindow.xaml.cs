using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OD.EditMediaInfo.Gui;

/// <summary>Handles file drops, window lifetime, and shortcuts for the metadata grid.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel model;
    private bool closeApproved;
    private bool confirmingClose;

    public MainWindow()
    {
        InitializeComponent();
        DarkWindowChrome.Attach(this);
        model = new MainViewModel(new EditorDialogs(this));
        DataContext = model;
    }

    internal Task OpenFileAsync(string path) => model.OpenFileAsync(path);

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !model.IsBusy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 }
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files)
            await OpenFileAsync(files[0]);
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(grid, source) is DataGridRow &&
            model.EditFieldCommand.CanExecute(null))
            model.EditFieldCommand.Execute(null);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closeApproved || (!model.IsBusy && !confirmingClose && model.PendingCount == 0))
            return;

        e.Cancel = true;
        if (model.IsBusy || confirmingClose)
            return;

        confirmingClose = true;
        model.IsBusy = true;
        try
        {
            closeApproved = await model.ConfirmLeaveAsync();
        }
        finally
        {
            model.IsBusy = false;
            confirmingClose = false;
        }
        if (closeApproved)
            _ = Dispatcher.InvokeAsync(Close);
    }

    protected override void OnClosed(EventArgs e)
    {
        model.Dispose();
        base.OnClosed(e);
    }
}
