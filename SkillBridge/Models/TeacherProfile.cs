using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillBridge.Models
{
    public class TeacherProfile
    {
        public string? Bio { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? HourlyRate { get; set; }
        [Key]
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }
}
