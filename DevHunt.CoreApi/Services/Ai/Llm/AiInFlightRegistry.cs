using System.Collections.Concurrent;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Tracks live LLM streams keyed by request id so a user can cancel a
/// generation mid-flight. Scoped to the local process; works for our
/// single-replica core-api today; with multi-replica deploys this would need
/// a Redis-backed pub/sub fan-out so the cancel hits whichever replica owns
/// the stream.
/// </summary>
public interface IAiInFlightRegistry
{
    /// <summary>
    /// Registers a new in-flight request. Returns a <see cref="CancellationToken"/>
    /// that the streaming code should observe. It fires when either the
    /// caller's own <paramref name="ct"/> trips OR
    /// <see cref="TryCancel"/> is invoked for this request.
    /// </summary>
    IDisposable Register(Guid requestId, Guid userId, Guid conversationId, CancellationToken ct, out CancellationToken linkedToken);

    /// <summary>
    /// Cancels a registered request, but only if it's owned by the calling
    /// user. Prevents one user cancelling another user's generations.
    /// </summary>
    bool TryCancel(Guid requestId, Guid userId);
}

/// <summary>
/// Process-local registry linking in-flight LLM request ids to cancellable streams.
/// </summary>
public sealed class AiInFlightRegistry : IAiInFlightRegistry
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    /// <inheritdoc />
    public IDisposable Register(Guid requestId, Guid userId, Guid conversationId, CancellationToken ct, out CancellationToken linkedToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var entry = new Entry(userId, conversationId, cts);
        _entries[requestId] = entry;
        linkedToken = cts.Token;
        return new Releaser(this, requestId, cts);
    }

    /// <inheritdoc />
    public bool TryCancel(Guid requestId, Guid userId)
    {
        if (!_entries.TryGetValue(requestId, out var entry)) return false;
        // Authorization: never let user A cancel user B's stream just by
        // guessing the request id.
        if (entry.UserId != userId) return false;

        try
        {
            entry.Cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // Race with Releaser disposing: already done, nothing to cancel.
            return false;
        }
    }

    /// <summary>
    /// Stores ownership and cancellation state for one registered in-flight request.
    /// </summary>
    private sealed record Entry(Guid UserId, Guid ConversationId, CancellationTokenSource Cts);

    /// <summary>
    /// Removes a registered request and disposes its linked cancellation source when the stream finishes.
    /// </summary>
    private sealed class Releaser : IDisposable
    {
        private readonly AiInFlightRegistry _owner;
        private readonly Guid _requestId;
        private readonly CancellationTokenSource _cts;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Releaser"/> class.
        /// </summary>
        /// <param name="owner">Registry instance that owns the entry.</param>
        /// <param name="requestId">Request id to remove from the registry.</param>
        /// <param name="cts">Linked cancellation source for the request.</param>
        public Releaser(AiInFlightRegistry owner, Guid requestId, CancellationTokenSource cts)
        {
            _owner = owner;
            _requestId = requestId;
            _cts = cts;
        }

        /// <summary>Removes the request from the registry and disposes linked cancellation state.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _owner._entries.TryRemove(_requestId, out _);
            _cts.Dispose();
        }
    }
}
