using Microsoft.EntityFrameworkCore;

namespace DotnetLearning.Models
{
    public static class ModelBuilderExtensions
    {
        public static void Seed(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SkillCategory>().HasData(new() { CategoryId = 1, Name = "Programming", Description = "Learn programming languages and frameworks", IconUrl = "https://example.com/icons/programming.png" },
                new() { CategoryId = 2, Name = "Design", Description = "Learn design principles and tools", IconUrl = "https://example.com/icons/design.png" },
                new() { CategoryId = 3, Name = "Marketing", Description = "Learn marketing strategies and techniques", IconUrl = "https://example.com/icons/marketing.png" },
                new() { CategoryId = 4, Name = "Business", Description = "Learn business skills and strategies", IconUrl = "https://example.com/icons/business.png" },
                new() { CategoryId = 5, Name = "Personal Development", Description = "Learn personal development skills and techniques", IconUrl = "https://example.com/icons/personal-development.png" });
            
            modelBuilder.Entity<Skill>().HasData(new() { SkillId = 1, Title = "C#", Description = "Learn C# for Beginners", NumberOfReviews = 347, PricePerHour = 243, TeacherName = "john", Rating = 2.5, CategoryId = 1,TeacherId="teacher1" },
                new() { SkillId = 2, Title = "ASP.NET Core", Description = "Learn ASP.NET Core", NumberOfReviews = 200, PricePerHour = 200, TeacherName = "Doe", Rating = 3.25, CategoryId = 1 , TeacherId="teacher1"},
                new() { SkillId = 3, Title = "Entity Framework", Description = "Learn Entity Framework", NumberOfReviews = 150, PricePerHour = 180, TeacherName = "Doe", Rating = 4, CategoryId = 1 ,TeacherId="teacher2"},
                new() { SkillId = 4, Title = "JavaScript", Description = "Learn JavaScript", NumberOfReviews = 400, PricePerHour = 220, TeacherName = "Doe", Rating = 5, CategoryId = 1 ,TeacherId="teacher3"},
                new() { SkillId = 5, Title = "Python", Description = "Learn Python", NumberOfReviews = 500, PricePerHour = 260, TeacherName = "Doe", Rating = 4.25, CategoryId = 1 ,TeacherId="teacher3"},
                new() { SkillId = 6, Title = "Java", Description = "Learn Java", NumberOfReviews = 300, PricePerHour = 240, TeacherName = "Doe", Rating = 1.57, CategoryId = 1,TeacherId="teacher4" });
        }
    }
}
