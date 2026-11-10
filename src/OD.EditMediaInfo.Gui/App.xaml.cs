using System.Windows;

namespace OD.EditMediaInfo.Gui;

/// <summary>Starts the Windows metadata editor, optionally opening a file from the command line.</summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Length > 0)
            await window.OpenFileAsync(e.Args[0]);
    }
}
