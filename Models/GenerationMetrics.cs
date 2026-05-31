namespace AionInstructPreview.Chat.Models;

// Per-response generation timing. Captured by AionInstructClient.GenerateAsync,
// surfaced on the Message that streamed the response, and rendered under
// each Aion Instruct Preview bubble in the transcript.
//
// Convention:
//   TtftMs           — milliseconds from prompt submit to first token delta.
//   DecodeMs         — milliseconds from first token to last token (the
//                      pure decode window; excludes prompt processing).
//   TotalMs          — milliseconds from prompt submit to final result.
//   TokenCount       — number of streaming deltas observed.
//   TokensPerSecond  — (TokenCount - 1) / DecodeMs, in seconds. Null when
//                      there's only one token (no inter-token interval to
//                      measure) or decode time is non-positive.
public sealed record GenerationMetrics(
    double TtftMs,
    double DecodeMs,
    double TotalMs,
    int TokenCount,
    double? TokensPerSecond)
{
    // Compact one-line label for the bubble caption.
    public string DisplayText
    {
        get
        {
            var tps = TokensPerSecond.HasValue
                ? $"{TokensPerSecond.Value:0.0} tok/s"
                : "—";
            return $"{TtftMs:0} ms to first token · {tps} · {TokenCount} tokens";
        }
    }
}
