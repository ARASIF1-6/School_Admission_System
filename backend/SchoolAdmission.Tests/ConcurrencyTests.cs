using Microsoft.Extensions.Configuration;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Services;
using SchoolAdmission.Tests.Helpers;
using Xunit;

namespace SchoolAdmission.Tests;

public class ConcurrencyTests
{
    private readonly IConfiguration _config;

    public ConcurrencyTests()
    {
        _config = new ConfigurationBuilder().Build();
    }

    [Fact]
    public async Task ConcurrentSeatHold_50StudentsCompetingFor5Seats_Exactly5SucceedAnd45ReceiveConflict()
    {
        // Arrange
        var dbName = "ConcurrencyTestDb_" + Guid.NewGuid();
        using var setupDb = TestDbContextFactory.Create(dbName);

        const int totalSeats = 5;
        const int totalApplicants = 50;

        var testClass = new ClassItem
        {
            Id = "c_concurrent_test",
            Name = "Class 6 - Highly Competitive Shift",
            TotalSeats = totalSeats,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Classes.Add(testClass);
        await setupDb.SaveChangesAsync();

        // Generate 50 distinct 17-digit student IDs
        var studentIds = Enumerable.Range(1, totalApplicants)
            .Select(i => $"2015269{i:D10}")
            .ToList();

        // Act: Execute 50 concurrent requests simultaneously using parallel tasks
        // Each task gets its own DbContext instance (mimicking concurrent HTTP requests)
        var tasks = studentIds.Select(async studentId =>
        {
            using var requestDb = TestDbContextFactory.Create(dbName);
            var service = new AdmissionService(requestDb, _config);
            return await service.CreateHoldAsync(testClass.Id, studentId);
        });

        var results = await Task.WhenAll(tasks);

        // Assert:
        var successfulHolds = results.Where(r => r.Success && r.StatusCode == 201).ToList();
        var conflictHolds = results.Where(r => !r.Success && r.StatusCode == 409).ToList();

        // 1. Exactly 5 holds must succeed
        Assert.Equal(totalSeats, successfulHolds.Count);

        // 2. Exactly 45 holds must be rejected with 409 Conflict (No seats available)
        Assert.Equal(totalApplicants - totalSeats, conflictHolds.Count);

        // 3. Verify in database: exactly 5 active holds exist for this class
        using var verificationDb = TestDbContextFactory.Create(dbName);
        var verificationService = new AdmissionService(verificationDb, _config);
        var classInfo = await verificationService.GetClassByIdAsync(testClass.Id);

        Assert.NotNull(classInfo);
        Assert.Equal(totalSeats, classInfo.HeldSeats);
        Assert.Equal(0, classInfo.AvailableSeats);
        Assert.Equal(totalSeats, classInfo.HeldSeats + classInfo.ConfirmedSeats);

        // Rule 1: Held + Confirmed must NEVER exceed TotalSeats
        Assert.True(classInfo.HeldSeats + classInfo.ConfirmedSeats <= classInfo.TotalSeats,
            "Held + Confirmed must never exceed totalSeats under any concurrent load!");
    }

    [Fact]
    public async Task ConcurrentSeatHold_100StudentsCompetingFor20Seats_Exactly20Succeed()
    {
        // Arrange
        var dbName = "ConcurrencyTestDb_100_" + Guid.NewGuid();
        using var setupDb = TestDbContextFactory.Create(dbName);

        const int totalSeats = 20;
        const int totalApplicants = 100;

        var testClass = new ClassItem
        {
            Id = "c_concurrent_100",
            Name = "Class 1 - Popular Shift",
            TotalSeats = totalSeats,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Classes.Add(testClass);
        await setupDb.SaveChangesAsync();

        var studentIds = Enumerable.Range(1, totalApplicants)
            .Select(i => $"2016370{i:D10}")
            .ToList();

        // Act: 100 simultaneous requests
        var tasks = studentIds.Select(async studentId =>
        {
            using var requestDb = TestDbContextFactory.Create(dbName);
            var service = new AdmissionService(requestDb, _config);
            return await service.CreateHoldAsync(testClass.Id, studentId);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        var successful = results.Count(r => r.Success && r.StatusCode == 201);
        var conflicts = results.Count(r => !r.Success && r.StatusCode == 409);

        Assert.Equal(totalSeats, successful);
        Assert.Equal(totalApplicants - totalSeats, conflicts);

        using var verificationDb = TestDbContextFactory.Create(dbName);
        var verificationService = new AdmissionService(verificationDb, _config);
        var classInfo = await verificationService.GetClassByIdAsync(testClass.Id);

        Assert.NotNull(classInfo);
        Assert.Equal(totalSeats, classInfo.HeldSeats);
        Assert.Equal(0, classInfo.AvailableSeats);
    }
}
