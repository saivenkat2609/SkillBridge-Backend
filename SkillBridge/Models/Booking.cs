using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillBridge.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        public int SkillId { get; set; }
        public string StudentId { get; set; }
        public string TeacherId { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public Skill Skill { get; set; }
        [ForeignKey("StudentId")]
        public ApplicationUser Student { get; set; }
        [ForeignKey("TeacherId")]
        public ApplicationUser Teacher { get; set; }
    }
}
