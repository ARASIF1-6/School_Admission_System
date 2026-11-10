using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.Models;

public class ClassItem
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "c_1" or "c_123"

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HoldItem> Holds { get; set; } = new List<HoldItem>();
}
