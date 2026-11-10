using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ClassItem> Classes => Set<ClassItem>();
    public DbSet<HoldItem> Holds => Set<HoldItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClassItem>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.TotalSeats).IsRequired();
        });

        modelBuilder.Entity<HoldItem>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.StudentId).IsRequired().HasMaxLength(50);
            entity.Property(h => h.Status).IsRequired().HasMaxLength(20);
            entity.Property(h => h.ExpiresAt).IsRequired();

            entity.HasOne(h => h.Class)
                  .WithMany(c => c.Holds)
                  .HasForeignKey(h => h.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => new { h.ClassId, h.Status });
            entity.HasIndex(h => h.StudentId);
        });

        // Seed initial example classes mentioned in the specification
        modelBuilder.Entity<ClassItem>().HasData(
            new ClassItem
            {
                Id = "c_1",
                Name = "Class 6 - Morning Shift",
                TotalSeats = 120,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ClassItem
            {
                Id = "c_2",
                Name = "Class 6 - Day Shift",
                TotalSeats = 120,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ClassItem
            {
                Id = "c_3",
                Name = "Class 7 - Morning Shift",
                TotalSeats = 80,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
