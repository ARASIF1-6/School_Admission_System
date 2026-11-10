using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

// Request to create a class: POST /classes
public class CreateClassRequest
{
    [Required(ErrorMessage = "Class name is required")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "Total seats must be greater than 0")]
    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; set; }
}

// Response from POST /classes
public class ClassCreatedResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; set; }
}

// Response for GET /classes and GET /classes/{classId}
public class ClassListItemResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; set; }

    [JsonPropertyName("heldSeats")]
    public int HeldSeats { get; set; }

    [JsonPropertyName("confirmedSeats")]
    public int ConfirmedSeats { get; set; }

    [JsonPropertyName("availableSeats")]
    public int AvailableSeats { get; set; }
}

// Response for POST /classes/{classId}/holds
public class HoldCreatedResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("studentId")]
    public string StudentId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = string.Empty;
}

// Response for GET /holds/{holdId}
public class HoldDetailResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = string.Empty;
}

// Response for POST /holds/{holdId}/confirm
public class HoldConfirmResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

// Response for GET /me/holds
public class MyHoldItemResponse
{
    [JsonPropertyName("holdId")]
    public string HoldId { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = string.Empty;
}

// Error response
public class ErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    public ErrorResponse() { }
    public ErrorResponse(string message) { Message = message; }
}
