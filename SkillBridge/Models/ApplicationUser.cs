using Microsoft.AspNetCore.Identity;

namespace SkillBridge.Models
{
    public class ApplicationUser:IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public TeacherProfile? TeacherProfile { get; set; }
        public bool IsOnboardingComplete { get; set; } = true;

    }
}
