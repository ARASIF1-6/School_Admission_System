using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly IAdmissionService _service;

    public HoldsController(IAdmissionService service)
    {
        _service = service;
    }

    /// <summary>
    /// #5: GET /holds/{holdId} - Get current status of a hold
    /// </summary>
    [HttpGet("{holdId}")]
    public async Task<IActionResult> GetHoldById(string holdId)
    {
        var (success, statusCode, result, errorMessage) = await _service.GetHoldByIdAsync(holdId);

        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(errorMessage ?? "Hold not found"));
        }

        return Ok(result);
    }

    /// <summary>
    /// #6: POST /holds/{holdId}/confirm - Confirm admission / pay admission fee
    /// </summary>
    [HttpPost("{holdId}/confirm")]
    public async Task<IActionResult> ConfirmHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required"));
        }

        var (success, statusCode, result, errorMessage) = await _service.ConfirmHoldAsync(holdId, studentId);

        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(errorMessage ?? "Failed to confirm hold"));
        }

        return Ok(result);
    }

    /// <summary>
    /// #7: DELETE /holds/{holdId} - Cancel admission hold
    /// </summary>
    [HttpDelete("{holdId}")]
    public async Task<IActionResult> CancelHold(
        [FromRoute] string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required"));
        }

        var (success, statusCode, errorMessage) = await _service.CancelHoldAsync(holdId, studentId);

        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(errorMessage ?? "Failed to cancel hold"));
        }

        return NoContent(); // 204
    }
}
