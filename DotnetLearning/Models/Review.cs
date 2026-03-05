using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotnetLearning.Models
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }
        public int Rating { get; set; }
        public string Content { get; set; }
        [ForeignKey("Skill")]
        public int SkillId { get; set; }
        [ForeignKey("Reviewer")]
        public string ReviewerId { get; set; }
        public ApplicationUser Reviewer { get; set; }
        [ForeignKey("Booking")]
        public int BookingId { get; set; }
        [ForeignKey("Teacher")]
        public string TeacherId { get; set; }
        public Skill Skill { get; set; }
        public Booking Booking { get; set; }
        public ApplicationUser Teacher { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
