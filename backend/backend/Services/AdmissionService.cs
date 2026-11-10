using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class AdmissionService : IAdmissionService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ClassLocks = new();

    public AdmissionService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private int GetHoldDurationSeconds()
    {
        var envVal = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");
        if (int.TryParse(envVal, out var sec) && sec > 0)
        {
            return sec;
        }

        var configVal = _config["HOLD_DURATION_SECONDS"];
        if (int.TryParse(configVal, out var secConfig) && secConfig > 0)
        {
            return secConfig;
        }

        return 600; // default 600 seconds (10 minutes)
    }

    private static SemaphoreSlim GetLockForClass(string classId)
    {
        return ClassLocks.GetOrAdd(classId, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<ClassCreatedResponse> CreateClassAsync(CreateClassRequest request)
    {
        var id = $"c_{Guid.NewGuid():N}"[..10];
        var entity = new ClassItem
        {
            Id = id,
            Name = request.Name.Trim(),
            TotalSeats = request.TotalSeats,
            CreatedAt = DateTime.UtcNow
        };

        _db.Classes.Add(entity);
        await _db.SaveChangesAsync();

        return new ClassCreatedResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            TotalSeats = entity.TotalSeats
        };
    }

    public async Task<List<ClassListItemResponse>> GetAllClassesAsync()
    {
        var now = DateTime.UtcNow;
        var classes = await _db.Classes
            .Include(c => c.Holds)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return classes.Select(c =>
        {
            var confirmed = c.Holds.Count(h => h.Status == HoldStatus.Confirmed);
            var held = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now);
            var available = Math.Max(0, c.TotalSeats - (confirmed + held));

            return new ClassListItemResponse
            {
                Id = c.Id,
                Name = c.Name,
                TotalSeats = c.TotalSeats,
                HeldSeats = held,
                ConfirmedSeats = confirmed,
                AvailableSeats = available
            };
        }).ToList();
    }

    public async Task<ClassListItemResponse?> GetClassByIdAsync(string classId)
    {
        var now = DateTime.UtcNow;
        var c = await _db.Classes
            .Include(x => x.Holds)
            .FirstOrDefaultAsync(x => x.Id == classId);

        if (c == null) return null;

        var confirmed = c.Holds.Count(h => h.Status == HoldStatus.Confirmed);
        var held = c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now);
        var available = Math.Max(0, c.TotalSeats - (confirmed + held));

        return new ClassListItemResponse
        {
            Id = c.Id,
            Name = c.Name,
            TotalSeats = c.TotalSeats,
            HeldSeats = held,
            ConfirmedSeats = confirmed,
            AvailableSeats = available
        };
    }

    public async Task<(bool Success, int StatusCode, HoldCreatedResponse? Result, string? ErrorMessage)> CreateHoldAsync(string classId, string studentId)
    {
        // Validation: 17-digit birth registration number
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Length != 17 || !studentId.All(char.IsDigit))
        {
            return (false, 400, null, "X-Student-Id must be a valid 17-digit numeric birth registration number");
        }

        var classLock = GetLockForClass(classId);
        await classLock.WaitAsync();

        try
        {
            var isRelational = _db.Database.IsRelational();
            using var transaction = isRelational
                ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
                : null;

            // In relational DB (PostgreSQL), lock the class row to serialize holds across multiple processes/nodes
            if (isRelational && _db.Database.ProviderName != null && _db.Database.ProviderName.Contains("Npgsql"))
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT \"Id\" FROM \"Classes\" WHERE \"Id\" = {0} FOR UPDATE", classId);
            }

            var classEntity = await _db.Classes
                .Include(c => c.Holds)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (classEntity == null)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return (false, 404, null, $"Class with ID '{classId}' not found");
            }

            var now = DateTime.UtcNow;

            // Rule: "একজন শিক্ষার্থী একটা class-এ একই সময়ে একটার বেশি active আবেদন বা নিশ্চিত ভর্তি রাখতে পারবে না"
            var hasActiveOrConfirmed = classEntity.Holds.Any(h =>
                h.StudentId == studentId &&
                (h.Status == HoldStatus.Confirmed ||
                 (h.Status == HoldStatus.Held && h.ExpiresAt > now)));

            if (hasActiveOrConfirmed)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return (false, 409, null, "Student already has an active hold or confirmed admission for this class");
            }

            // Rule: "কোনো অবস্থাতেই একটা class-এ held + confirmed সংখ্যা totalSeats-এর বেশি হবে না"
            var confirmedCount = classEntity.Holds.Count(h => h.Status == HoldStatus.Confirmed);
            var activeHeldCount = classEntity.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now);

            if (confirmedCount + activeHeldCount >= classEntity.TotalSeats)
            {
                if (transaction != null) await transaction.RollbackAsync();
                return (false, 409, null, "No seats available for this class");
            }

            var duration = GetHoldDurationSeconds();
            var expiresAt = now.AddSeconds(duration);
            var holdId = $"h_{Guid.NewGuid():N}"[..10];

            var newHold = new HoldItem
            {
                Id = holdId,
                ClassId = classId,
                StudentId = studentId,
                Status = HoldStatus.Held,
                CreatedAt = now,
                ExpiresAt = expiresAt
            };

            _db.Holds.Add(newHold);
            await _db.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            var response = new HoldCreatedResponse
            {
                HoldId = newHold.Id,
                ClassId = newHold.ClassId,
                StudentId = newHold.StudentId,
                Status = newHold.Status,
                ExpiresAt = newHold.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return (true, 201, response, null);
        }
        finally
        {
            classLock.Release();
        }
    }

    public async Task<(bool Success, int StatusCode, HoldDetailResponse? Result, string? ErrorMessage)> GetHoldByIdAsync(string holdId)
    {
        var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId);
        if (hold == null)
        {
            return (false, 404, null, $"Hold with ID '{holdId}' not found");
        }

        var now = DateTime.UtcNow;
        var displayStatus = hold.Status;
        if (hold.Status == HoldStatus.Held && hold.ExpiresAt <= now)
        {
            displayStatus = HoldStatus.Expired;
            hold.Status = HoldStatus.Expired;
            await _db.SaveChangesAsync();
        }

        var response = new HoldDetailResponse
        {
            HoldId = hold.Id,
            ClassId = hold.ClassId,
            Status = displayStatus,
            ExpiresAt = hold.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        return (true, 200, response, null);
    }

    public async Task<(bool Success, int StatusCode, HoldConfirmResponse? Result, string? ErrorMessage)> ConfirmHoldAsync(string holdId, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return (false, 400, null, "X-Student-Id header is required");
        }

        var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId);
        if (hold == null)
        {
            return (false, 404, null, $"Hold with ID '{holdId}' not found");
        }

        // Rule: "শুধু নিজের HELD আবেদন confirm করা যাবে"
        if (hold.StudentId != studentId)
        {
            return (false, 403, null, "Cannot confirm a hold that does not belong to you");
        }

        // Rule: "একই আবেদনে confirm একাধিকবার এলেও confirmedSeats একবারই বাড়বে" (Idempotency)
        if (hold.Status == HoldStatus.Confirmed)
        {
            return (true, 200, new HoldConfirmResponse
            {
                HoldId = hold.Id,
                Status = HoldStatus.Confirmed
            }, null);
        }

        if (hold.Status == HoldStatus.Cancelled)
        {
            return (false, 400, null, "Hold has already been cancelled");
        }

        var now = DateTime.UtcNow;
        // Rule: "মেয়াদ না পেরোনো HELD আবেদন confirm করা যাবে"
        if (hold.ExpiresAt <= now)
        {
            hold.Status = HoldStatus.Expired;
            await _db.SaveChangesAsync();
            return (false, 400, null, "Hold has expired and cannot be confirmed");
        }

        hold.Status = HoldStatus.Confirmed;
        hold.ConfirmedAt = now;
        await _db.SaveChangesAsync();

        return (true, 200, new HoldConfirmResponse
        {
            HoldId = hold.Id,
            Status = HoldStatus.Confirmed
        }, null);
    }

    public async Task<(bool Success, int StatusCode, string? ErrorMessage)> CancelHoldAsync(string holdId, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return (false, 400, "X-Student-Id header is required");
        }

        var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId);
        if (hold == null)
        {
            return (false, 404, $"Hold with ID '{holdId}' not found");
        }

        if (hold.StudentId != studentId)
        {
            return (false, 403, "Cannot cancel a hold that does not belong to you");
        }

        hold.Status = HoldStatus.Cancelled;
        await _db.SaveChangesAsync();

        return (true, 204, null);
    }

    public async Task<List<MyHoldItemResponse>> GetMyHoldsAsync(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return new List<MyHoldItemResponse>();
        }

        var now = DateTime.UtcNow;
        var holds = await _db.Holds
            .Include(h => h.Class)
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        bool hasChanges = false;
        var list = new List<MyHoldItemResponse>();

        foreach (var hold in holds)
        {
            var status = hold.Status;
            if (hold.Status == HoldStatus.Held && hold.ExpiresAt <= now)
            {
                status = HoldStatus.Expired;
                hold.Status = HoldStatus.Expired;
                hasChanges = true;
            }

            list.Add(new MyHoldItemResponse
            {
                HoldId = hold.Id,
                ClassId = hold.ClassId,
                ClassName = hold.Class?.Name ?? hold.ClassId,
                Status = status,
                ExpiresAt = hold.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        if (hasChanges)
        {
            await _db.SaveChangesAsync();
        }

        return list;
    }
}
