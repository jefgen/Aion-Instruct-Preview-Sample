using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using AionInstructPreview.Chat.Models;
using AionInstructPreview.Chat.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace AionInstructPreview.Chat;

public sealed partial class MainWindow : Window
{
    public ChatViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new ChatViewModel();
        this.InitializeComponent();
        this.Title = "Aion Instruct Preview Chat";

        // Mica backdrop: translucent material that picks up the desktop wallpaper tint.
        this.SystemBackdrop = new MicaBackdrop();

        // Custom title bar: hide the system chrome, hand the AppTitleBar
        // grid to the window so dragging works and caption buttons sit
        // over our content correctly.
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        // Listen to handled Enter key events so plain Enter sends the prompt,
        // while Shift+Enter can still insert a newline.
        PromptBox.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler(PromptBox_KeyDown),
            handledEventsToo: true);

        ViewModel.Messages.CollectionChanged += OnMessagesChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        this.Closed += (_, _) =>
        {
            ViewModel.Messages.CollectionChanged -= OnMessagesChanged;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.Dispose();
        };
    }

    // Return focus to the prompt box whenever input becomes enabled again --
    // most notably right after a response finishes streaming (Generating ->
    // Ready), so the user can keep typing without re-clicking the box. Also
    // covers the initial model-ready transition. Marshalled through the
    // dispatcher so the IsEnabled binding has applied before we call Focus
    // (Focus is a no-op on a still-disabled control).
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.InputEnabled) && ViewModel.InputEnabled)
        {
            DispatcherQueue.TryEnqueue(() => PromptBox.Focus(FocusState.Programmatic));
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (Message m in e.NewItems)
            {
                m.PropertyChanged += OnMessageChanged;
            }
        }
        ScrollToBottom();
    }

    private void OnMessageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Message.Text))
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TranscriptScroller?.ChangeView(null, TranscriptScroller.ScrollableHeight, null, disableAnimation: false);
        });
    }

    private void PromptBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        // Shift+Enter inserts a newline (the TextBox handles it natively
        // because AcceptsReturn=True). Plain Enter submits.
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        if ((shift & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            return;
        }

        if (ViewModel.SendEnabled)
        {
            e.Handled = true;
            ViewModel.SendCommand.Execute(null);
        }
    }
}
