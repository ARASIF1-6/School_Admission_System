using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly IAdmissionService _service;

    public MeController(IAdmissionService service)
    {
        _service = service;
    }

    /// <summary>
    /// #8: GET /me/holds - Get all holds for the authenticated student (via X-Student-Id)
    /// </summary>
    [HttpGet("holds")]
    public async Task<IActionResult> GetMyHolds(
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required"));
        }

        var list = await _service.GetMyHoldsAsync(studentId);
        return Ok(list);
    }
}
