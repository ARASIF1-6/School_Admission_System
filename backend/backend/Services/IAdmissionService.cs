using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Api.Services;

public interface IAdmissionService
{
    Task<ClassCreatedResponse> CreateClassAsync(CreateClassRequest request);
    Task<List<ClassListItemResponse>> GetAllClassesAsync();
    Task<ClassListItemResponse?> GetClassByIdAsync(string classId);
    Task<(bool Success, int StatusCode, HoldCreatedResponse? Result, string? ErrorMessage)> CreateHoldAsync(string classId, string studentId);
    Task<(bool Success, int StatusCode, HoldDetailResponse? Result, string? ErrorMessage)> GetHoldByIdAsync(string holdId);
    Task<(bool Success, int StatusCode, HoldConfirmResponse? Result, string? ErrorMessage)> ConfirmHoldAsync(string holdId, string studentId);
    Task<(bool Success, int StatusCode, string? ErrorMessage)> CancelHoldAsync(string holdId, string studentId);
    Task<List<MyHoldItemResponse>> GetMyHoldsAsync(string studentId);
}
