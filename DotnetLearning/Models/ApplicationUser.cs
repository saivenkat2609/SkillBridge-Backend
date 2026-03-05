using Microsoft.AspNetCore.Identity;

namespace DotnetLearning.Models
{
    public class ApplicationUser:IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public TeacherProfile? TeacherProfile { get; set; }
    }
}
