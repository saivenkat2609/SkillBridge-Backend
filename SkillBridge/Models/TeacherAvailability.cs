
using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models
{
    public class TeacherAvailability
    {
        [Key]
        public int AvailabilityId { get; set; }
        public string TeacherId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public ApplicationUser Teacher { get; set; }
    }
}
