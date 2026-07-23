namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Resolves versioned <see cref="IAiPlannerStrategy"/> implementations registered in DI.
/// Used by <see cref="AiPlanningService"/> and <see cref="AiPlanDraftService"/>.
/// </summary>
public interface IAiStrategyRouter
{
    /// <summary>
    /// Returns the strategy for <paramref name="version"/>, defaulting to <c>v1</c> when null or blank.
    /// </summary>
    /// <param name="version">Requested strategy version from the API.</param>
    /// <returns>Matching planner strategy.</returns>
    /// <exception cref="AiStrategyNotFoundException">When no strategy is registered for the version.</exception>
    IAiPlannerStrategy Resolve(string? version);

    /// <summary>Strategy version strings available at runtime.</summary>
    IReadOnlyCollection<string> AvailableVersions { get; }
}

/// <summary>
/// Indexes all <see cref="IAiPlannerStrategy"/> instances by <see cref="IAiPlannerStrategy.Version"/>.
/// Throws at startup if two strategies share the same version string.
/// </summary>
public sealed class AiStrategyRouter : IAiStrategyRouter
{
    private const string DefaultVersion = "v1";
    private readonly Dictionary<string, IAiPlannerStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiStrategyRouter"/> class.
    /// </summary>
    /// <param name="strategies">All planner strategies registered in DI.</param>
    public AiStrategyRouter(IEnumerable<IAiPlannerStrategy> strategies)
    {
        _strategies = new Dictionary<string, IAiPlannerStrategy>(StringComparer.OrdinalIgnoreCase);
        foreach (var strategy in strategies)
        {
            if (!_strategies.TryAdd(strategy.Version, strategy))
            {
                throw new InvalidOperationException($"Duplicate AI planner strategy version '{strategy.Version}'.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> AvailableVersions => _strategies.Keys.ToList();

    /// <inheritdoc />
    public IAiPlannerStrategy Resolve(string? version)
    {
        var resolvedVersion = string.IsNullOrWhiteSpace(version) ? DefaultVersion : version.Trim();
        if (_strategies.TryGetValue(resolvedVersion, out var strategy))
        {
            return strategy;
        }

        throw new AiStrategyNotFoundException(resolvedVersion, AvailableVersions);
    }
}

/// <summary>
/// Thrown when <see cref="IAiStrategyRouter.Resolve"/> cannot find a strategy for the requested version.
/// </summary>
public sealed class AiStrategyNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AiStrategyNotFoundException"/> class.
    /// </summary>
    /// <param name="version">Unknown version string from the caller.</param>
    /// <param name="availableVersions">Versions registered at runtime.</param>
    public AiStrategyNotFoundException(string version, IReadOnlyCollection<string> availableVersions)
        : base($"Unknown AI strategy version '{version}'. Supported versions: {string.Join(", ", availableVersions)}")
    {
        Version = version;
        AvailableVersions = availableVersions;
    }

    /// <summary>Requested version that was not found.</summary>
    public string Version { get; }

    /// <summary>Supported strategy versions for error messaging.</summary>
    public IReadOnlyCollection<string> AvailableVersions { get; }
}
