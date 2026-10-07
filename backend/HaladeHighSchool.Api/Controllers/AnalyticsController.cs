using HaladeHighSchool.Api.DTOs;
using HaladeHighSchool.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaladeHighSchool.Api.Controllers;

/// <summary>Role-scoped analytics for administrators and teachers.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class AnalyticsController(IAnalyticsService analytics) : PortalControllerBase
{
    /// <summary>Returns school enrolment by grade and the published-results pass/fail totals.</summary>
    [HttpGet("admin/analytics/overview")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<AdminAnalyticsOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminAnalyticsOverviewResponse>> GetAdminOverview(
        CancellationToken cancellationToken)
    {
        var overview = await analytics.GetAdminOverviewAsync(cancellationToken);
        return Ok(overview);
    }

    /// <summary>Returns published-results pass/fail totals for the signed-in teacher's assignments.</summary>
    [HttpGet("teacher/analytics/performance")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType<TeacherAnalyticsPerformanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherAnalyticsPerformanceResponse>> GetTeacherPerformance(
        CancellationToken cancellationToken)
    {
        var performance = await analytics.GetTeacherPerformanceAsync(
            User.GetUserId(),
            cancellationToken);

        return performance is null
            ? NotFoundProblem("The signed-in account is not linked to an active teacher profile.")
            : Ok(performance);
    }
}
