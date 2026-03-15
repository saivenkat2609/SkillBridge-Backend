using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models
{
    public enum ExceptionType
    {
        Add, Block
    }
    public class AvailabilityException
    {
        [Key]
        public int AvailabilityExceptionId { get; set; }
        public string TeacherId { get; set; }
        public DateTime ExceptionDate { get; set; }
        public ExceptionType Type { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public ApplicationUser Teacher { get; set; }
    }
}
