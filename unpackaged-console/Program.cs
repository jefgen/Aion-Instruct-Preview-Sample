using System;
using System.Threading.Tasks;
using AionInstructPreview.Text;
using AionInstructPreview.Chat.ConsoleApp;

// Unpackaged console harness for the Aion Instruct Preview SDK.
//
// Streams one prompt response to stdout so the sample can be run or captured
// from a terminal.

string prompt = args.Length > 0
    ? string.Join(' ', args)
    : "In one short sentence, what is Aion Instruct Preview?";

// Take the runtime dependency on the installed framework package (no MSIX
// identity for an unpackaged app), same as the WPF sample.
FrameworkDependency.EnsureLoaded();

// Write model-load status to stderr so stdout remains dedicated to the streamed reply.
Console.Error.WriteLine("[Aion Instruct Preview-console] Loading model (first run may take several minutes)...");

LanguageModel model;
try
{
    model = await LanguageModel.CreateAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[Aion Instruct Preview-console] CreateAsync failed: {ex.Message}");
    return 1;
}

Console.WriteLine("[Aion Instruct Preview-console] Model ready.");
var context = model.CreateContext();

Console.WriteLine($"You: {prompt}");
Console.Write("Aion Instruct Preview: ");

var op = model.GenerateResponseAsync(context, prompt);
// Stream per-token deltas directly to stdout.
op.Progress = (_, delta) => Console.Write(delta);

var result = await op;
Console.WriteLine();
Console.WriteLine($"[Aion Instruct Preview-console] status: {result.Status}");

// Dispose the context before the model because the context is owned by the model session.
context.Dispose();
model.Dispose();
return 0;
