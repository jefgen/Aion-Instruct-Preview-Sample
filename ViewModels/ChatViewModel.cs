using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using AionInstructPreview.Chat.Models;
using global::AionInstructPreview.Text;

namespace AionInstructPreview.Chat.ViewModels;

// Owns: the conversation transcript (ObservableCollection<Message>),
// the Aion Instruct Preview model client (loaded asynchronously in the ctor), the model
// state machine, and the Send + NewConversation commands.
//
// Threading: captures the UI-thread DispatcherQueue at construction time;
// every UI-bound state change funnels through TryEnqueue. The Aion Instruct Preview
// Progress callbacks fire on a background thread and are marshalled back to the UI thread.
public sealed class ChatViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private AionInstructClient? _aionClient;

    private ModelState _state = ModelState.Loading;
    private string _promptText = string.Empty;
    private string? _errorMessage;
    private bool _isContextFull;
    private bool _disposed;

    public ChatViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "ChatViewModel must be constructed on a UI thread that has a DispatcherQueue.");

        Messages = new ObservableCollection<Message>();
        SendCommand = new RelayCommand(_ => _ = SendAsync(), _ => CanSend);
        NewConversationCommand = new RelayCommand(
            _ => StartNewConversation(),
            _ => CanStartNewConversation);

        _ = LoadModelAsync();
    }

    public ObservableCollection<Message> Messages { get; }

    public ICommand SendCommand { get; }
    public ICommand NewConversationCommand { get; }

    public string PromptText
    {
        get => _promptText;
        set
        {
            if (_promptText == value) return;
            _promptText = value;
            Raise();
            // SendEnabled depends on PromptText.
            Raise(nameof(SendEnabled));
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ModelState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            Raise();
            Raise(nameof(LoadingVisibility));
            Raise(nameof(ErrorVisibility));
            Raise(nameof(ChatVisibility));
            Raise(nameof(InputEnabled));
            Raise(nameof(SendEnabled));
            Raise(nameof(CanStartNewConversation));
            Raise(nameof(NewConversationButtonEnabled));
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NewConversationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            Raise();
        }
    }

    // True when the latest GenerateResponseAsync returned
    // PromptLargerThanContext. Drives the "context full" banner + the
    // primary positioning of the New conversation affordance.
    public bool IsContextFull
    {
        get => _isContextFull;
        private set
        {
            if (_isContextFull == value) return;
            _isContextFull = value;
            Raise();
            Raise(nameof(ContextFullBannerVisibility));
            Raise(nameof(InputEnabled));
            Raise(nameof(SendEnabled));
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // UI-facing bindings. Visibility-typed properties avoid the need for a
    // BoolToVisibilityConverter in XAML.
    public Visibility LoadingVisibility =>
        _state == ModelState.Loading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility =>
        _state == ModelState.Error ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChatVisibility =>
        (_state == ModelState.Ready || _state == ModelState.Generating)
            ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContextFullBannerVisibility =>
        _isContextFull ? Visibility.Visible : Visibility.Collapsed;

    // Input + Send are disabled when the conversation is full — the user
    // must start a new conversation before sending another prompt. The
    // banner explains why.
    public bool InputEnabled => _state == ModelState.Ready && !_isContextFull;
    public bool SendEnabled => InputEnabled && !string.IsNullOrWhiteSpace(_promptText);
    public bool CanSend => SendEnabled;

    // New conversation is available whenever the model is loaded, except during generation.
    public bool CanStartNewConversation =>
        _aionClient is not null && _state != ModelState.Loading && _state != ModelState.Generating;
    public bool NewConversationButtonEnabled => CanStartNewConversation;

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task LoadModelAsync()
    {
        try
        {
            _aionClient = await AionInstructClient.CreateAsync().ConfigureAwait(true);
            State = ModelState.Ready;
            Raise(nameof(CanStartNewConversation));
            Raise(nameof(NewConversationButtonEnabled));
            (NewConversationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage =
                $"Aion Instruct Preview couldn't load.{Environment.NewLine}{ex.Message}{Environment.NewLine}" +
                "Make sure the Aion Instruct Preview framework MSIX is installed -- run Bootstrap.ps1, or scripts\\Diagnose-AionInstructPreview.ps1 to check every prerequisite.";
            State = ModelState.Error;
        }
    }

    public async Task SendAsync()
    {
        if (!CanSend) return;
        if (_aionClient == null) return;

        var prompt = _promptText.Trim();
        if (prompt.Length == 0) return;

        // Snapshot + clear before the await so the input box empties immediately.
        PromptText = string.Empty;

        Messages.Add(new Message(MessageRole.User, prompt, MessageStatus.Complete));
        var aionMessage = new Message(MessageRole.Aion, string.Empty, MessageStatus.Streaming);
        Messages.Add(aionMessage);

        State = ModelState.Generating;

        try
        {
            var generation = await _aionClient.GenerateAsync(
                prompt,
                onToken: token =>
                {
                    // Progress fires on a background thread; marshal to UI.
                    _dispatcher.TryEnqueue(() => aionMessage.Text += token);
                }).ConfigureAwait(true);

            var result = generation.Response;
            aionMessage.Metrics = generation.Metrics;

            switch (result.Status)
            {
                case LanguageModelResponseStatus.Complete:
                    // Use the final accumulated text if it differs from the streamed text.
                    if (aionMessage.Text != result.Text)
                    {
                        aionMessage.Text = result.Text;
                    }
                    aionMessage.Status = MessageStatus.Complete;
                    break;

                case LanguageModelResponseStatus.PromptLargerThanContext:
                    // The conversation has filled Aion Instruct Preview's context window.
                    // Surface this as its own UX, distinct from Error — the
                    // user can recover with "New conversation". Any partial
                    // text Aion Instruct Preview produced before overflow is preserved.
                    aionMessage.StatusDetail =
                        "This conversation reached Aion Instruct Preview's context limit. " +
                        "Start a new conversation to keep chatting.";
                    aionMessage.Status = MessageStatus.Error;
                    IsContextFull = true;
                    break;

                default:
                    aionMessage.StatusDetail = $"Aion Instruct Preview returned Status={result.Status}.";
                    aionMessage.Status = MessageStatus.Error;
                    break;
            }
        }
        catch (Exception ex)
        {
            aionMessage.StatusDetail = ex.Message;
            aionMessage.Status = MessageStatus.Error;
        }
        finally
        {
            State = ModelState.Ready;
        }
    }

    // Discard the in-process LanguageModelContext (and its accumulated
    // conversation history) and open a fresh one. Clears the transcript
    // too so the UX matches the model state.
    public void StartNewConversation()
    {
        if (!CanStartNewConversation) return;
        if (_aionClient == null) return;

        _aionClient.StartNewConversation();
        Messages.Clear();
        IsContextFull = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _aionClient?.Dispose();
        _aionClient = null;
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Minimal ICommand impl so we don't pull in CommunityToolkit.Mvvm.
internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
