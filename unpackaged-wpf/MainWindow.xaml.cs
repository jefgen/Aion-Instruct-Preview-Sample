using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AionInstructPreview.Text;

namespace AionInstructPreview.Chat.Wpf;

public partial class MainWindow : Window
{
    private LanguageModel? _model;
    private LanguageModelContext? _context;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // CreateAsync initializes the model. The first run can take several minutes
        // while the NPU context cache is prepared; later runs are faster.
        StatusText.Text = "Loading model (first run may take several minutes)...";
        try
        {
            _model = await LanguageModel.CreateAsync();
            _context = _model.CreateContext();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed to load the model.";
            MessageBox.Show(ex.Message, "Aion Instruct Preview: model load failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        StatusText.Text = "Ready. Type a message and press Enter (or Send).";
        InputBox.IsEnabled = true;
        SendButton.IsEnabled = true;
        InputBox.Focus();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SendButton.IsEnabled)
        {
            e.Handled = true;
            _ = SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_model is null || _context is null)
        {
            return;
        }

        string prompt = InputBox.Text.Trim();
        if (prompt.Length == 0)
        {
            return;
        }

        InputBox.Clear();
        SetBusy(true);
        AppendLine($"You: {prompt}");
        Append("Aion Instruct Preview: ");

        try
        {
            var op = _model.GenerateResponseAsync(_context, prompt);

            // Progress delivers token deltas on a background thread; marshal each
            // delta back to the UI thread before updating the transcript.
            op.Progress = (_, delta) => Dispatcher.Invoke(() => Append(delta));

            LanguageModelResponseResult result = await op;

            AppendLine(string.Empty);
            if (result.Status != LanguageModelResponseStatus.Complete)
            {
                AppendLine($"[status: {result.Status}]");
            }
            AppendLine(string.Empty);
        }
        catch (Exception ex)
        {
            AppendLine($"[error: {ex.Message}]");
            AppendLine(string.Empty);
        }
        finally
        {
            SetBusy(false);
            InputBox.Focus();
        }
    }

    private void SetBusy(bool busy)
    {
        InputBox.IsEnabled = !busy;
        SendButton.IsEnabled = !busy;
        StatusText.Text = busy ? "Generating..." : "Ready.";
    }

    private void Append(string text)
    {
        ConversationBox.AppendText(text);
        ConversationBox.ScrollToEnd();
    }

    private void AppendLine(string text) => Append(text + Environment.NewLine);

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Dispose the context before the model to release native resources cleanly.
        _context?.Dispose();
        _model?.Dispose();
    }
}
