using System.Text;

namespace DevHunt.CoreApi.Services.Ai.Llm.Providers;

/// <summary>
/// One Server-Sent Events frame as parsed off the wire. <see cref="EventType"/>
/// is null when the frame had no <c>event:</c> field - true for plain OpenAI
/// streams; Anthropic uses named event types (<c>message_start</c>,
/// <c>content_block_delta</c>, ...).
/// </summary>
public readonly record struct SseEvent(string? EventType, string Data);

/// <summary>
/// Minimal SSE stream parser tailored for LLM providers. Reads one frame at a
/// time, joining multi-line <c>data:</c> payloads with newlines, skipping
/// comment lines (<c>:</c>) and ignoring fields we don't care about
/// (<c>id</c>, <c>retry</c>). Yields events as soon as the blank-line
/// terminator arrives so callers can stream tokens with minimal latency.
/// </summary>
internal static class SseEventReader
{
    /// <summary>
    /// Reads SSE frames from <paramref name="stream"/> until EOF or cancellation.
    /// Yields each complete event when a blank-line frame boundary is reached.
    /// </summary>
    public static async IAsyncEnumerable<SseEvent> ReadAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        string? eventType = null;
        var dataBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                // End of stream - flush any buffered data.
                if (dataBuffer.Length > 0)
                {
                    yield return new SseEvent(eventType, dataBuffer.ToString());
                }
                yield break;
            }

            if (line.Length == 0)
            {
                // Blank line = frame boundary. Emit if there's anything.
                if (dataBuffer.Length > 0)
                {
                    yield return new SseEvent(eventType, dataBuffer.ToString());
                    dataBuffer.Clear();
                    eventType = null;
                }
                continue;
            }

            if (line[0] == ':')
            {
                // Comment / heartbeat - providers like OpenRouter send these
                // periodically to keep proxies from timing out. Skip silently.
                continue;
            }

            var colonIndex = line.IndexOf(':');
            string field, value;
            if (colonIndex < 0)
            {
                field = line;
                value = string.Empty;
            }
            else
            {
                field = line[..colonIndex];
                // SSE spec: a single leading space after the colon is stripped.
                value = colonIndex + 1 < line.Length && line[colonIndex + 1] == ' '
                    ? line[(colonIndex + 2)..]
                    : line[(colonIndex + 1)..];
            }

            switch (field)
            {
                case "data":
                    if (dataBuffer.Length > 0) dataBuffer.Append('\n');
                    dataBuffer.Append(value);
                    break;
                case "event":
                    eventType = value;
                    break;
                // id / retry intentionally ignored - we don't reconnect.
            }
        }
    }
}
