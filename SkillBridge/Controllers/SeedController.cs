using SkillBridge.Models;
using SkillBridge.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SkillBridge.Controllers
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

        // POST /api/seed — skips anything already seeded
        [HttpPost]
        public async Task<IActionResult> Seed()
        {
            var log = await SeedService.RunAsync(_userManager, _db);
            return Ok(new { message = "Seed complete!", log, accounts = SeedAccounts() });
        }

        // POST /api/seed/reset — clears bookings + reviews then re-seeds
        [HttpPost("reset")]
        public async Task<IActionResult> Reset()
        {
            var log = await SeedService.ReRunAsync(_userManager, _db);
            return Ok(new { message = "Reset + re-seed complete!", log, accounts = SeedAccounts() });
        }

        private static object[] SeedAccounts() => new object[]
        {
            new { email = "alice@skillswap.com",   password = "Test@1234", role = "Teacher" },
            new { email = "bob@skillswap.com",     password = "Test@1234", role = "Teacher" },
            new { email = "charlie@skillswap.com", password = "Test@1234", role = "Student" },
            new { email = "diana@skillswap.com",   password = "Test@1234", role = "Student" },
        };
    }
}
