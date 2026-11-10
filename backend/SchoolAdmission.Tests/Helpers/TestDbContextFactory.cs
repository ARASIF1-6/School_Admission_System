using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        // Always ensure c_test_1 and c_test_2 exist for tests
        if (!db.Classes.Any(c => c.Id == "c_test_1"))
        {
            db.Classes.AddRange(
                new ClassItem
                {
                    Id = "c_test_1",
                    Name = "Class 6 - Morning Shift",
                    TotalSeats = 10,
                    CreatedAt = DateTime.UtcNow
                },
                new ClassItem
                {
                    Id = "c_test_2",
                    Name = "Class 6 - Day Shift",
                    TotalSeats = 15,
                    CreatedAt = DateTime.UtcNow
                }
            );
            db.SaveChanges();
        }

        return db;
    }

    public static AppDbContext CreateWithClasses(string? dbName = null)
    {
        return Create(dbName);
    }
}
