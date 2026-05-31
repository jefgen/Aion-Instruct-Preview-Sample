namespace AionInstructPreview.Chat.Models;

// State machine for the Aion Instruct Preview language model from the app's point of view.
// Drives every UI affordance: loading overlay, input enable/disable, send
// button gating, error banner.
public enum ModelState
{
    Loading,    // initial; LanguageModel.CreateAsync in flight.
    Ready,      // model loaded, no in-flight prompt — accept input.
    Generating, // GenerateResponseAsync in flight — disable Send.
    Error,      // CreateAsync failed; terminal for this session.
}

public enum MessageRole
{
    User,
    Aion,
}

public enum MessageStatus
{
    Streaming,
    Complete,
    Error,
}
