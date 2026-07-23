using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Controllers;
using DevHunt.CoreApi.Services.Projects;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DevHunt.CoreApi.Tests.Controllers;

/// <summary>
/// Unit tests for ProjectTeamController
/// </summary>
public class ProjectTeamControllerTests
{
    private readonly Mock<IProjectTeamService> _teamServiceMock;
    private readonly ProjectTeamController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testProjectId = Guid.NewGuid();

    public ProjectTeamControllerTests()
    {
        _teamServiceMock = new Mock<IProjectTeamService>();

        _controller = new ProjectTeamController(_teamServiceMock.Object);

        // Set up User from Claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task Join_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        var request = new JoinRequest(_testProjectId, "Developer");
        _teamServiceMock
            .Setup(x => x.JoinAsync(_testProjectId, request, _testUserId))
            .ReturnsAsync(TeamResult<object>.Success(new { Message = "Joined successfully" }));

        // Act
        var result = await _controller.Join(_testProjectId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _teamServiceMock.Verify(x => x.JoinAsync(_testProjectId, request, _testUserId), Times.Once);
    }

    [Fact]
    public async Task Join_ShouldReturnNotFound_WhenProjectNotExists()
    {
        // Arrange
        var nonExistentProjectId = Guid.NewGuid();
        var request = new JoinRequest(nonExistentProjectId, "Developer");
        _teamServiceMock
            .Setup(x => x.JoinAsync(nonExistentProjectId, request, _testUserId))
            .ReturnsAsync(TeamResult<object>.Failure("Project not found", 404));

        // Act
        var result = await _controller.Join(nonExistentProjectId, request);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Join_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.Join(_testProjectId, null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Leave_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        _teamServiceMock
            .Setup(x => x.LeaveAsync(_testProjectId, _testUserId))
            .ReturnsAsync(TeamResult.Success());

        // Act
        var result = await _controller.Leave(_testProjectId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _teamServiceMock.Verify(x => x.LeaveAsync(_testProjectId, _testUserId), Times.Once);
    }

    [Fact]
    public async Task Leave_ShouldReturnNotFound_WhenNotMember()
    {
        // Arrange
        _teamServiceMock
            .Setup(x => x.LeaveAsync(_testProjectId, _testUserId))
            .ReturnsAsync(TeamResult.Failure("Not a member of this project", 404));

        // Act
        var result = await _controller.Leave(_testProjectId);

        // Assert
        AssertErrorResult(result, 404);
    }

    [Fact]
    public async Task GetMembers_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        var members = new List<PublicTeamMemberDto>
        {
            new PublicTeamMemberDto(
                Id: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                Name: "John Doe",
                Role: "Developer",
                IsLeader: false,
                Avatar: null,
                Status: "active",
                CanPublishNews: false,
                CanManageTasks: true,
                CanManageFiles: false,
                CanManageGallery: false)
        };
        _teamServiceMock
            .Setup(x => x.GetMembersAsync(_testProjectId, false, _testUserId, false, default))
            .ReturnsAsync(TeamResult<List<PublicTeamMemberDto>>.Success(members));

        // Act
        var result = await _controller.GetMembers(_testProjectId, false);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(members);
    }

    [Fact]
    public async Task RemoveMember_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        var memberToRemove = Guid.NewGuid();
        _teamServiceMock
            .Setup(x => x.RemoveMemberAsync(_testProjectId, memberToRemove, _testUserId))
            .ReturnsAsync(TeamResult.Success());

        // Act
        var result = await _controller.RemoveMember(_testProjectId, memberToRemove);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task RemoveMember_ShouldReturnForbidden_WhenNotAuthorized()
    {
        // Arrange
        var memberToRemove = Guid.NewGuid();
        _teamServiceMock
            .Setup(x => x.RemoveMemberAsync(_testProjectId, memberToRemove, _testUserId))
            .ReturnsAsync(TeamResult.Failure("Only project owner can remove members", 403));

        // Act
        var result = await _controller.RemoveMember(_testProjectId, memberToRemove);

        // Assert
        AssertErrorResult(result, 403);
    }

    private static void AssertErrorResult(IActionResult result, int expectedStatusCode)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task ChangeRole_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        var request = new ChangeRoleRequest(_testProjectId, Guid.NewGuid(), "Lead Developer");
        _teamServiceMock
            .Setup(x => x.ChangeRoleAsync(_testProjectId, request, _testUserId))
            .ReturnsAsync(TeamResult<object>.Success(new { Message = "Role changed" }));

        // Act
        var result = await _controller.ChangeRole(_testProjectId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRoles_ShouldReturnOk_WhenServiceSucceeds()
    {
        // Arrange
        var vacancies = new List<VacancyDto>
        {
            new VacancyDto("Backend Developer", 2, 1, null, false)
        };
        _teamServiceMock
            .Setup(x => x.GetRolesAsync(_testProjectId))
            .ReturnsAsync(TeamResult<List<VacancyDto>>.Success(vacancies));

        // Act
        var result = await _controller.GetRoles(_testProjectId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(vacancies);
    }
}

