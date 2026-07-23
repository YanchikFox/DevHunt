using System;
using System.Threading.Tasks;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing project issues (R11).
/// Extracted from ProjectsController to adhere to Single Responsibility Principle.
/// Routes: api/projects/{projectId}/issues/*
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectIssuesController : ControllerBase
{
    private readonly IProjectIssuesService _issuesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectIssuesController"/> class.
    /// </summary>
    /// <param name="issuesService">Service that applies project issue creation, listing, cancellation, and update rules.</param>
    public ProjectIssuesController(IProjectIssuesService issuesService)
    {
        _issuesService = issuesService;
    }

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Converts a successful issue service payload to <see cref="OkObjectResult"/> or 201, preserving service errors.
    /// </summary>
    private IActionResult MapResult<T>(IssueResult<T> result)
    {
        if (result.IsSuccess)
        {
             if (result.StatusCode == 201)
                return StatusCode(201, result.Data);
            return Ok(result.Data);
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Converts a successful no-payload issue service result to <see cref="NoContentResult"/> or preserves service errors.
    /// </summary>
    private IActionResult MapResult(IssueResult result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Creates an issue in the project after requiring a request body and returns a created response on service success.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPost("{projectId:guid}/issues")]
    [Authorize]
    public async Task<IActionResult> CreateIssue(Guid projectId, [FromBody] CreateIssueRequest request)
    {
        if (request == null) return BadRequest("Request payload is required");
        var result = await _issuesService.CreateIssueAsync(projectId, request, GetRequiredUserId());

        if (result.IsSuccess)
        {
            var data = (dynamic)result.Data!;
            return CreatedAtAction(nameof(GetProjectIssues), new { projectId }, data);
        }
        return MapResult(result);
    }

    /// <summary>
    /// Gets project issues visible to the authenticated caller according to the issue service.
    /// </summary>
    [HttpGet("{projectId:guid}/issues")]
    [Authorize]
    public async Task<IActionResult> GetProjectIssues(Guid projectId)
    {
        var result = await _issuesService.GetProjectIssuesAsync(projectId, GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Cancels an open project issue through reporter-only service rules.
    /// </summary>
    [HttpPost("{projectId:guid}/issues/{issueId:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelIssue(Guid projectId, Guid issueId, [FromBody] CancelIssueRequest? request = null)
    {
        var result = await _issuesService.CancelIssueAsync(projectId, issueId, request, GetRequiredUserId());
        return MapResult(result);
    }

    /// <summary>
    /// Updates a project issue after requiring a request body and passes super-admin context to the service.
    /// </summary>
    [ServiceFilter(typeof(ProfanityFilter))]
    [HttpPut("{projectId:guid}/issues/{issueId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateIssue(Guid projectId, Guid issueId, [FromBody] UpdateIssueRequest request)
    {
        if (request == null) return BadRequest("Request payload is required");
        // Only superadmin can update issues on any project; admin/curator limited to own projects
        var context = new UpdateIssueContext(projectId, issueId, request, GetRequiredUserId(), SecurityHelpers.IsSuperAdmin(User));
        var result = await _issuesService.UpdateIssueAsync(context);
        return MapResult(result);
    }
}
