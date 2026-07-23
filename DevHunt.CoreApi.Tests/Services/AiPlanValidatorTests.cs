using DevHunt.CoreApi.Services.Ai;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public class AiPlanValidatorTests
{
    [Fact]
    public void Validate_ShouldDetectCycles()
    {
        var validator = CreateValidator(new AiPlanningOptions { MaxPhases = 3, MaxTasks = 10, MaxDependencies = 5 });
        var draft = BuildDraft(new AiPlanPhase(
            "mvp",
            "MVP",
            "Phase",
            new List<string>(),
            new List<AiPlanTask>
            {
                new("task-a", "Task A", "Desc", new List<string> { "task-b" }),
                new("task-b", "Task B", "Desc", new List<string> { "task-a" })
            }
        ));

        var result = validator.Validate(draft);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Cycle detected"));
    }

    [Fact]
    public void Validate_ShouldDetectBrokenDependencies()
    {
        var validator = CreateValidator(new AiPlanningOptions { MaxPhases = 3, MaxTasks = 10, MaxDependencies = 5 });
        var draft = BuildDraft(new AiPlanPhase(
            "mvp",
            "MVP",
            "Phase",
            new List<string>(),
            new List<AiPlanTask>
            {
                new("task-a", "Task A", "Desc", new List<string> { "missing-task" })
            }
        ));

        var result = validator.Validate(draft);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Broken dependencies"));
    }

    [Fact]
    public void Validate_ShouldEnforceLimits()
    {
        var validator = CreateValidator(new AiPlanningOptions { MaxPhases = 1, MaxTasks = 1, MaxDependencies = 0 });
        var draft = BuildDraft(
            new AiPlanPhase(
                "mvp",
                "MVP",
                "Phase",
                new List<string>(),
                new List<AiPlanTask>
                {
                    new("task-a", "Task A", "Desc", new List<string> { "task-b" })
                }
            ),
            new AiPlanPhase(
                "alpha",
                "Alpha",
                "Phase",
                new List<string>(),
                new List<AiPlanTask>
                {
                    new("task-b", "Task B", "Desc", new List<string>())
                }
            )
        );

        var result = validator.Validate(draft);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("phases"));
        result.Errors.Should().Contain(error => error.Contains("tasks (max"));
        result.Errors.Should().Contain(error => error.Contains("dependency limit"));
    }

    private static AiPlanDraft BuildDraft(params AiPlanPhase[] phases)
    {
        return new AiPlanDraft("v1", "Idea", "Stack", phases.ToList());
    }

    private static AiPlanValidator CreateValidator(AiPlanningOptions options)
    {
        return new AiPlanValidator(Options.Create(options));
    }
}
