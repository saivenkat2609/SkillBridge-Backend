using DotnetLearning.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotnetLearning.Controllers
{
    [ApiController]
    [Route("/api/seed")]
    public class SeedController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _db;

        public SeedController(UserManager<ApplicationUser> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Seed()
        {
            var log = new List<string>();

            // --- USERS ---
            var teacher1 = await GetOrCreateUser("alice@skillswap.com", "Alice", "Johnson", "Test@1234", "Teacher", log);
            var teacher2 = await GetOrCreateUser("bob@skillswap.com", "Bob", "Smith", "Test@1234", "Teacher", log);
            var student1 = await GetOrCreateUser("charlie@skillswap.com", "Charlie", "Brown", "Test@1234", "Student", log);
            var student2 = await GetOrCreateUser("diana@skillswap.com", "Diana", "Prince", "Test@1234", "Student", log);

            // --- SKILLS ---
            var reactSkill   = await GetOrCreateSkill("React for Beginners",          "Learn React from scratch — components, hooks, state, and real projects.", 800,  teacher1, 1, 4.8, 24, log);
            var aspnetSkill  = await GetOrCreateSkill("ASP.NET Core API Development", "Build production-ready REST APIs. Covers EF Core, Identity, and Azure deployment.", 1000, teacher1, 1, 4.5, 18, log);
            var pythonSkill  = await GetOrCreateSkill("Python Data Science",           "Master Python with Pandas, NumPy, and Matplotlib. Great for beginners.", 750,  teacher2, 1, 4.9, 41, log);
            var jsSkill      = await GetOrCreateSkill("JavaScript & TypeScript",       "Deep dive into modern JS and TS — async/await, generics, best practices.", 700,  teacher2, 1, 4.3, 15, log);
            var figmaSkill   = await GetOrCreateSkill("Figma UI/UX Design",            "Design beautiful interfaces in Figma — wireframes to high-fidelity prototypes.", 600,  teacher1, 2, 4.7, 32, log);
            var brandSkill   = await GetOrCreateSkill("Brand Identity Design",         "Create professional brand identities — logos, colour theory, typography.", 900,  teacher2, 2, 4.6, 11, log);
            var seoSkill     = await GetOrCreateSkill("SEO Fundamentals",              "Rank higher on Google — on-page SEO, keyword research, backlinks, content.", 500,  teacher1, 3, 4.2,  9, log);
            var socialSkill  = await GetOrCreateSkill("Social Media Marketing",        "Grow your brand on Instagram, LinkedIn, and TikTok with proven strategies.", 550,  teacher2, 3, 4.4, 20, log);
            var excelSkill   = await GetOrCreateSkill("Excel for Business",            "Master Excel formulas, pivot tables, and dashboards for productivity.", 450,  teacher1, 4, 4.1,  7, log);
            var financeSkill = await GetOrCreateSkill("Financial Modelling",           "Build professional financial models — valuation, DCF, sensitivity analysis.", 1200, teacher2, 4, 4.9, 28, log);
            var speakSkill   = await GetOrCreateSkill("Public Speaking Coaching",      "Overcome fear of public speaking and deliver confident presentations.", 650,  teacher1, 5, 5.0, 14, log);
            var prodSkill    = await GetOrCreateSkill("Productivity Systems",          "Build a personal productivity system using GTD, Notion, and time-blocking.", 400,  teacher2, 5, 4.6, 19, log);

            // --- BOOKINGS ---
            if (!await _db.Bookings.AnyAsync())
            {
                var bookings = new List<Booking>
                {
                    new() { SkillId = reactSkill.SkillId,   StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(10),  DurationMinutes = 60,  TotalPrice = 800,  Status = BookingStatus.Confirmed,  CreatedAt = DateTime.UtcNow.AddDays(-2)  },
                    new() { SkillId = pythonSkill.SkillId,  StudentId = student1.Id, TeacherId = teacher2.Id, ScheduledAt = DateTime.UtcNow.AddDays(7).Date.AddHours(14),  DurationMinutes = 90,  TotalPrice = 1125, Status = BookingStatus.Pending,    CreatedAt = DateTime.UtcNow.AddDays(-1)  },
                    new() { SkillId = figmaSkill.SkillId,   StudentId = student2.Id, TeacherId = teacher1.Id, ScheduledAt = DateTime.UtcNow.AddDays(5).Date.AddHours(11),  DurationMinutes = 60,  TotalPrice = 600,  Status = BookingStatus.Confirmed,  CreatedAt = DateTime.UtcNow.AddDays(-3)  },
                    new() { SkillId = jsSkill.SkillId,      StudentId = student2.Id, TeacherId = teacher2.Id, ScheduledAt = DateTime.UtcNow.AddDays(10).Date.AddHours(9),  DurationMinutes = 120, TotalPrice = 1400, Status = BookingStatus.Pending,    CreatedAt = DateTime.UtcNow.AddDays(-1)  },
                    new() { SkillId = reactSkill.SkillId,   StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = DateTime.UtcNow.AddDays(-10).Date.AddHours(10), DurationMinutes = 60,  TotalPrice = 800,  Status = BookingStatus.Completed,  CreatedAt = DateTime.UtcNow.AddDays(-15) },
                    new() { SkillId = pythonSkill.SkillId,  StudentId = student2.Id, TeacherId = teacher2.Id, ScheduledAt = DateTime.UtcNow.AddDays(-5).Date.AddHours(9),  DurationMinutes = 120, TotalPrice = 1500, Status = BookingStatus.Completed,  CreatedAt = DateTime.UtcNow.AddDays(-8)  },
                    new() { SkillId = aspnetSkill.SkillId,  StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = DateTime.UtcNow.AddDays(-20).Date.AddHours(14), DurationMinutes = 60,  TotalPrice = 1000, Status = BookingStatus.Completed,  CreatedAt = DateTime.UtcNow.AddDays(-25) },
                    new() { SkillId = figmaSkill.SkillId,   StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = DateTime.UtcNow.AddDays(-2).Date.AddHours(15), DurationMinutes = 60,  TotalPrice = 600,  Status = BookingStatus.Cancelled,  CreatedAt = DateTime.UtcNow.AddDays(-7)  },
                    new() { SkillId = seoSkill.SkillId,     StudentId = student2.Id, TeacherId = teacher1.Id, ScheduledAt = DateTime.UtcNow.AddDays(-1).Date.AddHours(11), DurationMinutes = 60,  TotalPrice = 500,  Status = BookingStatus.Cancelled,  CreatedAt = DateTime.UtcNow.AddDays(-5)  },
                };
                _db.Bookings.AddRange(bookings);
                await _db.SaveChangesAsync();
                log.Add($"Created {bookings.Count} bookings");
            }
            else
            {
                log.Add("Bookings already exist — skipped");
            }

            // --- REVIEWS ---
            if (!await _db.Reviews.AnyAsync())
            {
                var completedBookings = await _db.Bookings
                    .Where(b => b.Status == BookingStatus.Completed)
                    .ToListAsync();

                var reviews = new List<Review>();
                if (completedBookings.Count >= 3)
                {
                    reviews.AddRange(new[]
                    {
                        new Review { SkillId = reactSkill.SkillId,  ReviewerId = student1.Id, TeacherId = teacher1.Id, BookingId = completedBookings[0].BookingId, Rating = 5, Content = "Alice is an amazing teacher! She explained React hooks so clearly. I finally understand useEffect. Highly recommend!", CreatedAt = DateTime.UtcNow.AddDays(-9) },
                        new Review { SkillId = pythonSkill.SkillId, ReviewerId = student2.Id, TeacherId = teacher2.Id, BookingId = completedBookings[1].BookingId, Rating = 4, Content = "Bob's Python session was really helpful. Great examples and very patient with questions. Would book again.", CreatedAt = DateTime.UtcNow.AddDays(-4) },
                        new Review { SkillId = aspnetSkill.SkillId, ReviewerId = student1.Id, TeacherId = teacher1.Id, BookingId = completedBookings[2].BookingId, Rating = 5, Content = "Brilliant session on ASP.NET Core! Alice has a knack for making complex topics simple. Learned more in 1 hour than weeks of YouTube.", CreatedAt = DateTime.UtcNow.AddDays(-19) },
                    });
                    _db.Reviews.AddRange(reviews);
                    await _db.SaveChangesAsync();
                    log.Add($"Created {reviews.Count} reviews");
                }
                else
                {
                    log.Add($"Not enough completed bookings for reviews (found {completedBookings.Count})");
                }
            }
            else
            {
                log.Add("Reviews already exist — skipped");
            }

            return Ok(new
            {
                message = "Seed complete!",
                log,
                accounts = new object[]
                {
                    new { email = "alice@skillswap.com",   password = "Test@1234", role = "Teacher" },
                    new { email = "bob@skillswap.com",     password = "Test@1234", role = "Teacher" },
                    new { email = "charlie@skillswap.com", password = "Test@1234", role = "Student" },
                    new { email = "diana@skillswap.com",   password = "Test@1234", role = "Student" },
                }
            });
        }

        private async Task<Skill> GetOrCreateSkill(string title, string description, int price, ApplicationUser teacher, int categoryId, double rating, int numReviews, List<string> log)
        {
            var existing = await _db.Skills.FirstOrDefaultAsync(s => s.Title == title);
            if (existing != null)
            {
                log.Add($"Skill '{title}' already exists — skipped");
                return existing;
            }
            var skill = new Skill
            {
                Title = title,
                Description = description,
                PricePerHour = price,
                TeacherId = teacher.Id,
                TeacherName = $"{teacher.FirstName} {teacher.LastName}",
                CategoryId = categoryId,
                Rating = rating,
                NumberOfReviews = numReviews,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(10, 90)),
                IsActive = true
            };
            _db.Skills.Add(skill);
            await _db.SaveChangesAsync();
            log.Add($"Created skill '{title}' (ID {skill.SkillId})");
            return skill;
        }

        private async Task<ApplicationUser> GetOrCreateUser(string email, string firstName, string lastName, string password, string role, List<string> log)
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                log.Add($"User {email} already exists — skipped");
                return existing;
            }
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                IsOnboardingComplete = true
            };
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new Exception($"Failed to create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            await _userManager.AddToRoleAsync(user, role);
            log.Add($"Created {role} user: {email}");
            return user;
        }
    }
}
