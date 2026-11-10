using Microsoft.Extensions.Configuration;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Services;
using SchoolAdmission.Tests.Helpers;
using Xunit;

namespace SchoolAdmission.Tests;

public class HoldServiceTests
{
    private readonly IConfiguration _config;

    public HoldServiceTests()
    {
        _config = new ConfigurationBuilder().Build();
    }

    [Fact]
    public async Task CreateHold_ValidStudentId_ShouldSucceedWithHeldStatus()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);
        var studentId = "20152691234567890"; // 17 digits

        // Act
        var (success, statusCode, result, errorMessage) = await service.CreateHoldAsync("c_test_1", studentId);

        // Assert
        Assert.True(success);
        Assert.Equal(201, statusCode);
        Assert.NotNull(result);
        Assert.StartsWith("h_", result.HoldId);
        Assert.Equal("c_test_1", result.ClassId);
        Assert.Equal(studentId, result.StudentId);
        Assert.Equal("HELD", result.Status);
        Assert.NotEmpty(result.ExpiresAt);
    }

    [Fact]
    public async Task CreateHold_InvalidStudentId_ShouldReturnBadRequest()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);

        // Less than 17 digits
        var (success1, statusCode1, _, _) = await service.CreateHoldAsync("c_test_1", "12345");
        Assert.False(success1);
        Assert.Equal(400, statusCode1);

        // Non-numeric
        var (success2, statusCode2, _, _) = await service.CreateHoldAsync("c_test_1", "2015269123456789A");
        Assert.False(success2);
        Assert.Equal(400, statusCode2);
    }

    [Fact]
    public async Task CreateHold_DuplicateActiveHoldSameStudent_ShouldReturnConflict()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);
        var studentId = "20152691234567890";

        // First hold succeeds
        var (success1, _, _, _) = await service.CreateHoldAsync("c_test_1", studentId);
        Assert.True(success1);

        // Second hold for the same student and same class must be rejected (Rule 3)
        var (success2, statusCode2, _, errorMessage) = await service.CreateHoldAsync("c_test_1", studentId);
        Assert.False(success2);
        Assert.Equal(409, statusCode2);
        Assert.Contains("already has an active hold", errorMessage);
    }

    [Fact]
    public async Task ConfirmHold_ValidHeldHold_ShouldChangeStatusToConfirmed()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);
        var studentId = "20152691234567890";

        var (success, _, hold, _) = await service.CreateHoldAsync("c_test_1", studentId);
        Assert.True(success);

        // Act - Confirm
        var (cSuccess, cStatus, cResult, _) = await service.ConfirmHoldAsync(hold!.HoldId, studentId);

        // Assert
        Assert.True(cSuccess);
        Assert.Equal(200, cStatus);
        Assert.NotNull(cResult);
        Assert.Equal("CONFIRMED", cResult.Status);
    }

    [Fact]
    public async Task ConfirmHold_MultipleTimes_IsIdempotent()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);
        var studentId = "20152691234567890";

        var (_, _, hold, _) = await service.CreateHoldAsync("c_test_1", studentId);
        
        // Confirm first time
        var (firstSuccess, _, firstResult, _) = await service.ConfirmHoldAsync(hold!.HoldId, studentId);
        Assert.True(firstSuccess);
        Assert.Equal("CONFIRMED", firstResult!.Status);

        // Confirm second time (Rule 5: must succeed and not double count)
        var (secondSuccess, secondStatus, secondResult, _) = await service.ConfirmHoldAsync(hold.HoldId, studentId);
        Assert.True(secondSuccess);
        Assert.Equal(200, secondStatus);
        Assert.Equal("CONFIRMED", secondResult!.Status);

        // Verify confirmedSeats count in class is exactly 1
        var classInfo = await service.GetClassByIdAsync("c_test_1");
        Assert.Equal(1, classInfo!.ConfirmedSeats);
    }

    [Fact]
    public async Task ExpiredHold_ReturnsSeatToAvailable_WithoutApiCall()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var cls = new ClassItem { Id = "c_exp_test", Name = "Class 9 - Day", TotalSeats = 2 };
        db.Classes.Add(cls);
        db.SaveChanges();

        var service = new AdmissionService(db, _config);

        // Add an already-expired hold
        var expiredHold = new HoldItem
        {
            Id = "h_expired",
            ClassId = cls.Id,
            StudentId = "11111111111111111",
            Status = HoldStatus.Held,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10) // Expired 10 mins ago
        };
        db.Holds.Add(expiredHold);
        db.SaveChanges();

        // Act - Check available seats
        var classInfo = await service.GetClassByIdAsync(cls.Id);

        // Assert - Expired hold MUST NOT count towards heldSeats (Rule 2)
        Assert.NotNull(classInfo);
        Assert.Equal(0, classInfo.HeldSeats);
        Assert.Equal(2, classInfo.AvailableSeats);
    }

    [Fact]
    public async Task CancelHold_ShouldSetStatusToCancelled_AndFreeSeat()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);
        var studentId = "20152691234567890";

        var (_, _, hold, _) = await service.CreateHoldAsync("c_test_1", studentId);

        // Act - Cancel hold
        var (success, statusCode, _) = await service.CancelHoldAsync(hold!.HoldId, studentId);

        // Assert
        Assert.True(success);
        Assert.Equal(204, statusCode);

        // Verify status
        var (getSuccess, _, detail, _) = await service.GetHoldByIdAsync(hold.HoldId);
        Assert.True(getSuccess);
        Assert.Equal("CANCELLED", detail!.Status);

        // Verify seat is freed
        var classInfo = await service.GetClassByIdAsync("c_test_1");
        Assert.Equal(0, classInfo!.HeldSeats);
        Assert.Equal(10, classInfo.AvailableSeats);
    }

    [Fact]
    public async Task GetMyHolds_ShouldReturnOnlyStudentHolds()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);
        var student1 = "10000000000000001";
        var student2 = "10000000000000002";

        await service.CreateHoldAsync("c_test_1", student1);
        await service.CreateHoldAsync("c_test_2", student1);
        await service.CreateHoldAsync("c_test_1", student2);

        // Act
        var student1Holds = await service.GetMyHoldsAsync(student1);

        // Assert
        Assert.Equal(2, student1Holds.Count);
        Assert.All(student1Holds, h => Assert.False(string.IsNullOrEmpty(h.ClassName)));
    }
}
