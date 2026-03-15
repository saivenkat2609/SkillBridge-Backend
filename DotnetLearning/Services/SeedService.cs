using DotnetLearning.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotnetLearning.Services
{
    public static class SeedService
    {
        public static async Task<List<string>> RunAsync(UserManager<ApplicationUser> userManager, AppDbContext db)
        {
            var log = new List<string>();

            // --- USERS ---
            var teacher1 = await GetOrCreateUser(userManager, "alice@skillbridge.com", "Alice", "Johnson", "Test@1234", "Teacher", log);
            var teacher2 = await GetOrCreateUser(userManager, "bob@skillbridge.com", "Bob", "Smith", "Test@1234", "Teacher", log);
            var student1 = await GetOrCreateUser(userManager, "charlie@skillbridge.com", "Charlie", "Brown", "Test@1234", "Student", log);
            var student2 = await GetOrCreateUser(userManager, "diana@skillbridge.com", "Diana", "Prince", "Test@1234", "Student", log);

            // --- TEACHER PROFILES ---
            await GetOrCreateTeacherProfile(db, teacher1, "Full-stack developer and educator with 8 years teaching React, ASP.NET Core, and cloud deployments.", 800, log);
            await GetOrCreateTeacherProfile(db, teacher2, "Data scientist and designer passionate about Python, visual branding, and financial modelling.", 750, log);

            // --- SKILLS ---
            var reactSkill   = await GetOrCreateSkill(db, "React for Beginners",          "Learn React from scratch — components, hooks, state, and real projects.", 800,  teacher1, 1, 4.8, 24, log);
            var aspnetSkill  = await GetOrCreateSkill(db, "ASP.NET Core API Development", "Build production-ready REST APIs. Covers EF Core, Identity, and Azure deployment.", 1000, teacher1, 1, 4.5, 18, log);
            var pythonSkill  = await GetOrCreateSkill(db, "Python Data Science",          "Master Python with Pandas, NumPy, and Matplotlib. Great for beginners.", 750,  teacher2, 1, 4.9, 41, log);
            var jsSkill      = await GetOrCreateSkill(db, "JavaScript & TypeScript",      "Deep dive into modern JS and TS — async/await, generics, best practices.", 700,  teacher2, 1, 4.3, 15, log);
            var figmaSkill   = await GetOrCreateSkill(db, "Figma UI/UX Design",           "Design beautiful interfaces in Figma — wireframes to high-fidelity prototypes.", 600,  teacher1, 2, 4.7, 32, log);
            var brandSkill   = await GetOrCreateSkill(db, "Brand Identity Design",        "Create professional brand identities — logos, colour theory, typography.", 900,  teacher2, 2, 4.6, 11, log);
            var seoSkill     = await GetOrCreateSkill(db, "SEO Fundamentals",             "Rank higher on Google — on-page SEO, keyword research, backlinks, content.", 500,  teacher1, 3, 4.2,  9, log);
            var socialSkill  = await GetOrCreateSkill(db, "Social Media Marketing",       "Grow your brand on Instagram, LinkedIn, and TikTok with proven strategies.", 550,  teacher2, 3, 4.4, 20, log);
            var excelSkill   = await GetOrCreateSkill(db, "Excel for Business",           "Master Excel formulas, pivot tables, and dashboards for productivity.", 450,  teacher1, 4, 4.1,  7, log);
            var financeSkill = await GetOrCreateSkill(db, "Financial Modelling",          "Build professional financial models — valuation, DCF, sensitivity analysis.", 1200, teacher2, 4, 4.9, 28, log);
            var speakSkill   = await GetOrCreateSkill(db, "Public Speaking Coaching",     "Overcome fear of public speaking and deliver confident presentations.", 650,  teacher1, 5, 5.0, 14, log);
            var prodSkill    = await GetOrCreateSkill(db, "Productivity Systems",         "Build a personal productivity system using GTD, Notion, and time-blocking.", 400,  teacher2, 5, 4.6, 19, log);

            // --- TEACHER AVAILABILITY ---
            await GetOrCreateAvailability(db, teacher1, log);
            await GetOrCreateAvailability(db, teacher2, log);

            // --- BOOKINGS (skip if already seeded) ---
            if (await db.Bookings.AnyAsync())
            {
                log.Add("Bookings already exist — skipped");
                return log;
            }

            var now = DateTime.UtcNow;

            // Charlie — upcoming (Confirmed + Pending)
            var b1 = new Booking { SkillId = reactSkill.SkillId,   StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(3).Date.AddHours(10),  DurationMinutes = 60,  TotalPrice = 800,  Status = BookingStatus.Confirmed, CreatedAt = now.AddDays(-2)  };
            var b2 = new Booking { SkillId = pythonSkill.SkillId,  StudentId = student1.Id, TeacherId = teacher2.Id, ScheduledAt = now.AddDays(7).Date.AddHours(14),  DurationMinutes = 90,  TotalPrice = 1125, Status = BookingStatus.Pending,   CreatedAt = now.AddDays(-1)  };
            var b3 = new Booking { SkillId = financeSkill.SkillId, StudentId = student1.Id, TeacherId = teacher2.Id, ScheduledAt = now.AddDays(14).Date.AddHours(11), DurationMinutes = 60,  TotalPrice = 1200, Status = BookingStatus.Confirmed, CreatedAt = now.AddDays(-3)  };

            // Diana — upcoming
            var b4 = new Booking { SkillId = figmaSkill.SkillId,   StudentId = student2.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(5).Date.AddHours(11),  DurationMinutes = 60,  TotalPrice = 600,  Status = BookingStatus.Confirmed, CreatedAt = now.AddDays(-3)  };
            var b5 = new Booking { SkillId = jsSkill.SkillId,      StudentId = student2.Id, TeacherId = teacher2.Id, ScheduledAt = now.AddDays(10).Date.AddHours(9),  DurationMinutes = 120, TotalPrice = 1400, Status = BookingStatus.Pending,   CreatedAt = now.AddDays(-1)  };

            // Charlie — completed (past sessions)
            var b6 = new Booking { SkillId = reactSkill.SkillId,  StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(-10).Date.AddHours(10), DurationMinutes = 60,  TotalPrice = 800,  Status = BookingStatus.Completed, CreatedAt = now.AddDays(-15) };
            var b7 = new Booking { SkillId = aspnetSkill.SkillId, StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(-20).Date.AddHours(14), DurationMinutes = 60,  TotalPrice = 1000, Status = BookingStatus.Completed, CreatedAt = now.AddDays(-25) };
            var b8 = new Booking { SkillId = speakSkill.SkillId,  StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(-30).Date.AddHours(9),  DurationMinutes = 60,  TotalPrice = 650,  Status = BookingStatus.Completed, CreatedAt = now.AddDays(-35) };

            // Diana — completed
            var b9  = new Booking { SkillId = pythonSkill.SkillId, StudentId = student2.Id, TeacherId = teacher2.Id, ScheduledAt = now.AddDays(-5).Date.AddHours(9),   DurationMinutes = 120, TotalPrice = 1500, Status = BookingStatus.Completed, CreatedAt = now.AddDays(-8)  };
            var b10 = new Booking { SkillId = brandSkill.SkillId,  StudentId = student2.Id, TeacherId = teacher2.Id, ScheduledAt = now.AddDays(-15).Date.AddHours(11), DurationMinutes = 60,  TotalPrice = 900,  Status = BookingStatus.Completed, CreatedAt = now.AddDays(-18) };
            var b11 = new Booking { SkillId = figmaSkill.SkillId,  StudentId = student2.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(-25).Date.AddHours(10), DurationMinutes = 90,  TotalPrice = 900,  Status = BookingStatus.Completed, CreatedAt = now.AddDays(-28) };

            // Cancelled
            var b12 = new Booking { SkillId = figmaSkill.SkillId, StudentId = student1.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(-2).Date.AddHours(15), DurationMinutes = 60, TotalPrice = 600, Status = BookingStatus.Cancelled, CreatedAt = now.AddDays(-7) };
            var b13 = new Booking { SkillId = seoSkill.SkillId,   StudentId = student2.Id, TeacherId = teacher1.Id, ScheduledAt = now.AddDays(-1).Date.AddHours(11), DurationMinutes = 60, TotalPrice = 500, Status = BookingStatus.Cancelled, CreatedAt = now.AddDays(-5) };

            db.Bookings.AddRange(b1, b2, b3, b4, b5, b6, b7, b8, b9, b10, b11, b12, b13);
            await db.SaveChangesAsync();
            log.Add("Created 13 bookings");

            // --- REVIEWS (one per completed booking, referencing exact booking IDs) ---
            var reviews = new List<Review>
            {
                new() { SkillId = reactSkill.SkillId,  ReviewerId = student1.Id, TeacherId = teacher1.Id, BookingId = b6.BookingId,  Rating = 5, Content = "Alice is an amazing teacher! She explained React hooks so clearly. I finally understand useEffect. Highly recommend!", CreatedAt = now.AddDays(-9)  },
                new() { SkillId = aspnetSkill.SkillId, ReviewerId = student1.Id, TeacherId = teacher1.Id, BookingId = b7.BookingId,  Rating = 5, Content = "Brilliant session on ASP.NET Core! Alice makes complex topics simple. Learned more in 1 hour than weeks of YouTube.", CreatedAt = now.AddDays(-19) },
                new() { SkillId = speakSkill.SkillId,  ReviewerId = student1.Id, TeacherId = teacher1.Id, BookingId = b8.BookingId,  Rating = 4, Content = "Great public speaking tips. Alice gave me practical exercises I could use immediately. My next presentation went much better!", CreatedAt = now.AddDays(-29) },
                new() { SkillId = pythonSkill.SkillId, ReviewerId = student2.Id, TeacherId = teacher2.Id, BookingId = b9.BookingId,  Rating = 4, Content = "Bob's Python session was really helpful. Great examples and very patient with questions. Would definitely book again.", CreatedAt = now.AddDays(-4)  },
                new() { SkillId = brandSkill.SkillId,  ReviewerId = student2.Id, TeacherId = teacher2.Id, BookingId = b10.BookingId, Rating = 5, Content = "Incredible session on brand design! Bob walked me through logo creation step by step. My client loved the result.", CreatedAt = now.AddDays(-14) },
                new() { SkillId = figmaSkill.SkillId,  ReviewerId = student2.Id, TeacherId = teacher1.Id, BookingId = b11.BookingId, Rating = 5, Content = "Alice's Figma course is top notch. She taught me how to build a full design system from scratch. 10/10 would recommend!", CreatedAt = now.AddDays(-24) },
            };
            db.Reviews.AddRange(reviews);
            await db.SaveChangesAsync();
            log.Add($"Created {reviews.Count} reviews");

            return log;
        }

        // Force re-seed: clears bookings + reviews then calls RunAsync
        public static async Task<List<string>> ReRunAsync(UserManager<ApplicationUser> userManager, AppDbContext db)
        {
            var log = new List<string>();

            var reviews = await db.Reviews.ToListAsync();
            if (reviews.Any()) { db.Reviews.RemoveRange(reviews); await db.SaveChangesAsync(); log.Add($"Cleared {reviews.Count} reviews"); }

            var bookings = await db.Bookings.ToListAsync();
            if (bookings.Any()) { db.Bookings.RemoveRange(bookings); await db.SaveChangesAsync(); log.Add($"Cleared {bookings.Count} bookings"); }

            var availabilities = await db.TeacherAvailabilities.ToListAsync();
            if (availabilities.Any()) { db.TeacherAvailabilities.RemoveRange(availabilities); await db.SaveChangesAsync(); log.Add($"Cleared {availabilities.Count} availability slots"); }

            log.AddRange(await RunAsync(userManager, db));
            return log;
        }

        private static async Task GetOrCreateAvailability(AppDbContext db, ApplicationUser teacher, List<string> log)
        {
            var exists = await db.TeacherAvailabilities.AnyAsync(a => a.TeacherId == teacher.Id);
            if (exists) { log.Add($"Availability for {teacher.Email} already exists — skipped"); return; }

            // Alice: Mon, Wed, Fri  9am–5pm
            // Bob:   Tue, Thu, Sat 10am–6pm
            var days = teacher.Email == "alice@skillbridge.com"
                ? new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }
                : new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };

            var start = teacher.Email == "alice@skillbridge.com" ? TimeSpan.FromHours(9) : TimeSpan.FromHours(10);
            var end   = teacher.Email == "alice@skillbridge.com" ? TimeSpan.FromHours(17) : TimeSpan.FromHours(18);

            foreach (var day in days)
            {
                db.TeacherAvailabilities.Add(new TeacherAvailability
                {
                    TeacherId = teacher.Id,
                    DayOfWeek = day,
                    StartTime = start,
                    EndTime   = end
                });
            }
            await db.SaveChangesAsync();
            log.Add($"Created availability for {teacher.Email} ({string.Join(", ", days)})");
        }

        private static async Task GetOrCreateTeacherProfile(AppDbContext db, ApplicationUser teacher, string bio, decimal hourlyRate, List<string> log)
        {
            var exists = await db.TeacherProfiles.AnyAsync(p => p.ApplicationUserId == teacher.Id);
            if (exists) { log.Add($"TeacherProfile for {teacher.Email} already exists — skipped"); return; }

            db.TeacherProfiles.Add(new TeacherProfile
            {
                ApplicationUserId = teacher.Id,
                Bio = bio,
                HourlyRate = hourlyRate
            });
            await db.SaveChangesAsync();
            log.Add($"Created TeacherProfile for {teacher.Email}");
        }

        private static async Task<Skill> GetOrCreateSkill(AppDbContext db, string title, string description, int price, ApplicationUser teacher, int categoryId, double rating, int numReviews, List<string> log)
        {
            var existing = await db.Skills.FirstOrDefaultAsync(s => s.Title == title);
            if (existing != null) { log.Add($"Skill '{title}' already exists — skipped"); return existing; }

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
            db.Skills.Add(skill);
            await db.SaveChangesAsync();
            log.Add($"Created skill '{title}' (ID {skill.SkillId})");
            return skill;
        }

        private static async Task<ApplicationUser> GetOrCreateUser(UserManager<ApplicationUser> userManager, string email, string firstName, string lastName, string password, string role, List<string> log)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null) { log.Add($"User {email} already exists — skipped"); return existing; }

            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                IsOnboardingComplete = true
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new Exception($"Failed to create {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(user, role);
            log.Add($"Created {role} user: {email}");
            return user;
        }
    }
}
