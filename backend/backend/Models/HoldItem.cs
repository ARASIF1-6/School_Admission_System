using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolAdmission.Api.Models;

public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public class HoldItem
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "h_1" or "h_789"

    [Required]
    public string ClassId { get; set; } = string.Empty;

    [ForeignKey(nameof(ClassId))]
    public ClassItem Class { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string StudentId { get; set; } = string.Empty; // 17-digit birth registration number

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = HoldStatus.Held;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }
}
