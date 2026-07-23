using System;
using System.Threading.Tasks;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Tests.TestSupport;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevHunt.CoreApi.Tests.Controllers;

public class SkillsControllerTests : IDisposable
{
    private readonly DevHuntDbContext _dbContext;

    public SkillsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DevHuntDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DevHuntTestDbContext(options);
    }

    [Fact]
    public async Task SuggestSkills_ShouldReturnBadRequest_WhenQIsEmpty()
    {
        var controller = new SkillsController(_dbContext, new NullLogger<SkillsController>());

        var result = await controller.SuggestSkills("   ");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SuggestSkills_ShouldMatchAliasExact()
    {
        var js = new Skill { Id = Guid.NewGuid(), Name = "JavaScript", Category = "Frontend" };
        _dbContext.Skills.Add(js);
        _dbContext.SkillAliases.Add(new SkillAlias
        {
            Id = Guid.NewGuid(),
            SkillId = js.Id,
            Alias = "js",
            AliasNormalized = "js",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var controller = new SkillsController(_dbContext, new NullLogger<SkillsController>());

        var action = await controller.SuggestSkills("js", null, 10);
        var ok = action.Should().BeOfType<OkObjectResult>().Subject;

        ok.Value.Should().NotBeNull();
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as System.Collections.IEnumerable;
        items.Should().NotBeNull();

        var list = items!.Cast<SkillsController.SkillSuggestItemDto>().ToList();
        list.Should().ContainSingle(x => x.Name == "JavaScript" && x.Match == "alias_exact");
    }

    [Fact]
    public async Task SuggestSkills_ShouldMatchByNameContains()
    {
        var react = new Skill { Id = Guid.NewGuid(), Name = "React", Category = "Frontend" };
        _dbContext.Skills.Add(react);
        await _dbContext.SaveChangesAsync();

        var controller = new SkillsController(_dbContext, new NullLogger<SkillsController>());

        var action = await controller.SuggestSkills("ea", null, 10);
        var ok = action.Should().BeOfType<OkObjectResult>().Subject;

        ok.Value.Should().NotBeNull();
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as System.Collections.IEnumerable;
        items.Should().NotBeNull();
        items!.Cast<SkillsController.SkillSuggestItemDto>().Should().Contain(x => x.Name == "React");
    }

    [Fact]
    public async Task SuggestSkills_ShouldApplyCategoryFilter()
    {
        var react = new Skill { Id = Guid.NewGuid(), Name = "React", Category = "Frontend" };
        var redis = new Skill { Id = Guid.NewGuid(), Name = "Redis", Category = "Backend" };
        _dbContext.Skills.AddRange(react, redis);
        await _dbContext.SaveChangesAsync();

        var controller = new SkillsController(_dbContext, new NullLogger<SkillsController>());

        var action = await controller.SuggestSkills("re", "Frontend", 10);
        var ok = action.Should().BeOfType<OkObjectResult>().Subject;

        ok.Value.Should().NotBeNull();
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as System.Collections.IEnumerable;
        items.Should().NotBeNull();

        var list = items!.Cast<SkillsController.SkillSuggestItemDto>().ToList();
        list.Should().Contain(x => x.Name == "React");
        list.Should().NotContain(x => x.Name == "Redis");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
