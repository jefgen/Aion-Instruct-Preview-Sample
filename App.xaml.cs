using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace AionInstructPreview.Chat;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            File.AppendAllText(LogPath(), $"\n[AppDomain] {e.ExceptionObject}\n");
        };
    }

    // Stable key used to identify this app's primary instance.
    private const string SingleInstanceKey = "AionInstructPreview.Chat.SingleInstance";

    /// <summary>
    /// Custom program entry point used to redirect secondary launches to the
    /// already-running app instance before XAML startup.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RedirectedToPrimaryInstance())
        {
            // A primary instance already exists; do not start a second XAML host.
            return;
        }

        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }

    /// <summary>
    /// Returns true if this launch was handed off to an already-running primary
    /// instance (in which case this process should exit). Returns false if this
    /// process is the primary instance and should continue startup.
    /// </summary>
    private static bool RedirectedToPrimaryInstance()
    {
        try
        {
            AppActivationArguments activationArgs =
                AppInstance.GetCurrent().GetActivatedEventArgs();

            AppInstance primary = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
            if (primary.IsCurrent)
            {
                // Subscribe so redirected launches re-activate this window.
                primary.Activated += OnRedirectedActivation;
                return false;
            }

            // Forward this activation to the primary instance from a helper thread.
            RedirectActivationTo(activationArgs, primary);
            return true;
        }
        catch (Exception ex)
        {
            // If single-instancing fails for any reason, fall back to normal
            // startup rather than blocking the app from launching at all.
            File.AppendAllText(LogPath(), $"\n[SingleInstance] {ex}\n");
            return false;
        }
    }

    private static void RedirectActivationTo(AppActivationArguments args, AppInstance primary)
    {
        using var redirected = new ManualResetEvent(false);
        var thread = new Thread(() =>
        {
            primary.RedirectActivationToAsync(args).AsTask().Wait();
            redirected.Set();
        });
        thread.IsBackground = true;
        thread.Start();
        redirected.WaitOne();
    }

    private static void OnRedirectedActivation(object? sender, AppActivationArguments args)
    {
        // Marshal back onto the UI thread and bring the existing window forward.
        App? app = Current as App;
        Window? window = app?._window;
        window?.DispatcherQueue.TryEnqueue(() =>
        {
            window.Activate();
        });
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogPath(), $"\n[OnLaunched] {ex}\n");
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(LogPath(), $"\n[Xaml] {e.Exception}\n[Message] {e.Message}\n");
        }
        catch { }
    }

    private static string LogPath() =>
        Path.Combine(Path.GetTempPath(), "Aion Instruct Preview-chat-crash.log");
}
