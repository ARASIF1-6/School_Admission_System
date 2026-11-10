using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly IAdmissionService _service;

    public ClassesController(IAdmissionService service)
    {
        _service = service;
    }

    /// <summary>
    /// #1: POST /classes - Create a new class
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ErrorResponse("Class name is required"));
        }

        if (request.TotalSeats <= 0)
        {
            return BadRequest(new ErrorResponse("totalSeats must be greater than 0"));
        }

        var result = await _service.CreateClassAsync(request);
        return StatusCode(201, result);
    }

    /// <summary>
    /// #2: GET /classes - List all classes with seat statistics
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllClasses()
    {
        var list = await _service.GetAllClassesAsync();
        return Ok(list);
    }

    /// <summary>
    /// #3: GET /classes/{classId} - Get seat status for a specific class
    /// </summary>
    [HttpGet("{classId}")]
    public async Task<IActionResult> GetClassById(string classId)
    {
        var result = await _service.GetClassByIdAsync(classId);
        if (result == null)
        {
            return NotFound(new ErrorResponse($"Class with ID '{classId}' not found"));
        }

        return Ok(result);
    }

    /// <summary>
    /// #4: POST /classes/{classId}/holds - Apply / hold a seat
    /// </summary>
    [HttpPost("{classId}/holds")]
    public async Task<IActionResult> CreateHold(
        [FromRoute] string classId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required"));
        }

        var (success, statusCode, result, errorMessage) = await _service.CreateHoldAsync(classId, studentId);

        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(errorMessage ?? "Failed to create hold"));
        }

        return StatusCode(201, result);
    }
}
