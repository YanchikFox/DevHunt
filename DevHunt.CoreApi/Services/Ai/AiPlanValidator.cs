using Microsoft.Extensions.Options;

namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Outcome of validating an <see cref="AiPlanDraft"/> against structural rules and <see cref="AiPlanningOptions"/> limits.
/// </summary>
/// <param name="Errors">Human-readable validation messages; empty when valid.</param>
public sealed record AiPlanValidationResult(IReadOnlyList<string> Errors)
{
    /// <summary>True when <see cref="Errors"/> is empty.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Validates AI-generated plan drafts before persistence or apply.
/// </summary>
public interface IAiPlanValidator
{
    /// <summary>
    /// Checks phase and task counts, required fields, duplicate ids/titles, dependency limits,
    /// broken references, and cycles in the dependency graph.
    /// </summary>
    /// <param name="draft">Plan draft returned by a planner strategy.</param>
    /// <returns>Aggregated validation result; may contain multiple errors.</returns>
    AiPlanValidationResult Validate(AiPlanDraft draft);
}

/// <summary>
/// Default <see cref="IAiPlanValidator"/> enforcing limits from <see cref="AiPlanningOptions"/>.
/// Uses iterative DFS to detect dependency cycles without stack overflow at max task count.
/// </summary>
public sealed class AiPlanValidator : IAiPlanValidator
{
    private readonly AiPlanningOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiPlanValidator"/> class.
    /// </summary>
    /// <param name="options">Configuration options for AI plan limits.</param>
    public AiPlanValidator(IOptions<AiPlanningOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public AiPlanValidationResult Validate(AiPlanDraft draft)
    {
        var errors = new List<string>();

        if (draft.Phases == null || draft.Phases.Count == 0)
        {
            errors.Add("Plan contains no phases.");
            return new AiPlanValidationResult(errors);
        }

        if (draft.Phases.Count > _options.MaxPhases)
        {
            errors.Add($"Plan contains {draft.Phases.Count} phases (max {_options.MaxPhases}).");
        }

        var tasks = draft.Phases.SelectMany(p => p.Tasks ?? new List<AiPlanTask>()).ToList();
        if (tasks.Count == 0)
        {
            errors.Add("Plan contains no tasks.");
            return new AiPlanValidationResult(errors);
        }

        if (tasks.Count > _options.MaxTasks)
        {
            errors.Add($"Plan contains {tasks.Count} tasks (max {_options.MaxTasks}).");
        }

        var missingIds = tasks.Where(t => string.IsNullOrWhiteSpace(t.Id)).ToList();
        if (missingIds.Count > 0)
        {
            errors.Add("Some tasks are missing ids.");
        }

        var missingTitles = tasks.Where(t => string.IsNullOrWhiteSpace(t.Title)).ToList();
        if (missingTitles.Count > 0)
        {
            errors.Add("Some tasks are missing titles.");
        }

        var duplicateIds = tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .Select(t => t.Id.Trim())
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            errors.Add($"Duplicate task ids: {string.Join(", ", duplicateIds)}");
        }

        var duplicateTitles = tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .Select(t => t.Title.Trim())
            .GroupBy(title => title, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateTitles.Count > 0)
        {
            errors.Add($"Duplicate task titles: {string.Join(", ", duplicateTitles)}");
        }

        var overDependencyLimit = tasks
            .Where(t => (t.DependsOn?.Count ?? 0) > _options.MaxDependencies)
            .Select(t => string.IsNullOrWhiteSpace(t.Id) ? "(missing id)" : t.Id.Trim())
            .ToList();
        if (overDependencyLimit.Count > 0)
        {
            errors.Add($"Tasks exceed dependency limit ({_options.MaxDependencies}): {string.Join(", ", overDependencyLimit)}");
        }

        var taskIds = new HashSet<string>(
            tasks.Where(t => !string.IsNullOrWhiteSpace(t.Id)).Select(t => t.Id.Trim()),
            StringComparer.OrdinalIgnoreCase
        );

        var brokenDependencies = tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .SelectMany(t => (t.DependsOn ?? new List<string>())
                .Where(dep => !string.IsNullOrWhiteSpace(dep))
                .Select(dep => (task: t.Id.Trim(), dep: dep.Trim())))
            .Where(x => !taskIds.Contains(x.dep))
            .Select(x => $"{x.task} -> {x.dep}")
            .ToList();
        if (brokenDependencies.Count > 0)
        {
            errors.Add($"Broken dependencies: {string.Join(", ", brokenDependencies)}");
        }

        var adjacency = tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .ToDictionary(
                t => t.Id.Trim(),
                t => (t.DependsOn ?? new List<string>())
                    .Where(dep => !string.IsNullOrWhiteSpace(dep))
                    .Select(dep => dep.Trim())
                    .ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        // I-13: Iterative DFS to detect cycles (avoids StackOverflow at MaxTasks=500)
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasCycle = false;

        foreach (var startNode in adjacency.Keys)
        {
            if (visited.Contains(startNode)) continue;

            var dfsStack = new Stack<(string node, bool entering)>();
            dfsStack.Push((startNode, true));

            while (dfsStack.Count > 0)
            {
                var (node, entering) = dfsStack.Pop();

                if (!entering)
                {
                    inStack.Remove(node);
                    continue;
                }

                if (inStack.Contains(node))
                {
                    hasCycle = true;
                    break;
                }

                if (visited.Contains(node)) continue;

                visited.Add(node);
                inStack.Add(node);
                dfsStack.Push((node, false)); // post-visit marker

                if (adjacency.TryGetValue(node, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        if (!adjacency.ContainsKey(neighbor)) continue;
                        if (inStack.Contains(neighbor))
                        {
                            hasCycle = true;
                            break;
                        }
                        if (!visited.Contains(neighbor))
                            dfsStack.Push((neighbor, true));
                    }
                }

                if (hasCycle) break;
            }

            if (hasCycle)
            {
                errors.Add("Cycle detected in task dependencies.");
                break;
            }
        }

        return new AiPlanValidationResult(errors);
    }
}
