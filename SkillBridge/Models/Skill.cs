using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillBridge.Models
{
    public class Skill
    {
        [Key]
        [Required]
        public int SkillId { get; set; }

        [Required]
        public string Title { get; set; }
        public string Description { get; set; }
        public int PricePerHour { get; set; }
        public string? TeacherId { get; set; }
        public string TeacherName { get; set; }
        public double Rating { get; set; }
        [Required]
        public int CategoryId { get; set; }
        public string? ImageUrl { get; set; }
        public SkillCategory Category { get; set; }
        public int NumberOfReviews { get; set; }
        [Column(TypeName = "datetime2")]
        public DateTime? CreatedAt { get; set; }
        public bool? IsActive { get; set; }
    }
}
