using System;
using System.Windows;

namespace AionInstructPreview.Chat.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Load the framework package before any WinRT activation. See FrameworkDependency.cs.
        try
        {
            FrameworkDependency.EnsureLoaded();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Aion Instruct Preview: cannot load the framework package",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        base.OnStartup(e);
    }
}
