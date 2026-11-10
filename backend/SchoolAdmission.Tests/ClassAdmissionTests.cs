using Microsoft.Extensions.Configuration;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;
using SchoolAdmission.Tests.Helpers;
using Xunit;

namespace SchoolAdmission.Tests;

public class ClassAdmissionTests
{
    private readonly IConfiguration _config;

    public ClassAdmissionTests()
    {
        _config = new ConfigurationBuilder().Build();
    }

    [Fact]
    public async Task CreateClass_ShouldReturnCreatedClass_WithValidIdAndSeats()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AdmissionService(db, _config);

        var request = new CreateClassRequest
        {
            Name = "Class 8 - Morning Shift",
            TotalSeats = 50
        };

        // Act
        var result = await service.CreateClassAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("c_", result.Id);
        Assert.Equal("Class 8 - Morning Shift", result.Name);
        Assert.Equal(50, result.TotalSeats);
    }

    [Fact]
    public async Task GetAllClasses_ShouldCalculateAvailableSeatsAccurately()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateWithClasses();
        var service = new AdmissionService(db, _config);

        // Act
        var classes = await service.GetAllClassesAsync();

        // Assert
        Assert.NotEmpty(classes);
        var cls = classes.First();
        Assert.Equal(cls.TotalSeats, cls.AvailableSeats + cls.HeldSeats + cls.ConfirmedSeats);
    }

    [Fact]
    public async Task GetClassById_NonExistent_ShouldReturnNull()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var service = new AdmissionService(db, _config);

        // Act
        var result = await service.GetClassByIdAsync("non_existent_id");

        // Assert
        Assert.Null(result);
    }
}
