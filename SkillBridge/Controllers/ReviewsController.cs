using SkillBridge.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SkillBridge.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public record ReviewDto(int BookingId, string Content, int Rating);
        public ReviewsController(AppDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] ReviewDto review)
        {
            var booking = _context.Bookings.FirstOrDefault(b => b.BookingId == review.BookingId);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (booking == null || booking.Status != BookingStatus.Completed || (review.Rating<1 || review.Rating>5) ||(string.IsNullOrWhiteSpace(review.Content) || review.Content.Length<10 || review.Content.Length>500))
            {
                return BadRequest();
            }
            else if (booking.StudentId != userId || userId == booking.TeacherId)
            {
                return Forbid();
            }
            var ratingAlreadyDone = _context.Reviews.Any(a => a.BookingId == review.BookingId);
            if (ratingAlreadyDone )
            {
                return BadRequest();

            }
            var newReview = new Review
            {
                BookingId = review.BookingId,
                Content = review.Content,
                Rating = review.Rating,
                SkillId = booking.SkillId,
                ReviewerId = booking.StudentId,
                TeacherId = booking.TeacherId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reviews.Add(newReview);
            await _context.SaveChangesAsync();

            // Recalculate skill's aggregate rating now that a new review exists
            var skill = await _context.Skills.FirstOrDefaultAsync(s => s.SkillId == booking.SkillId);
            var allRatings = await _context.Reviews
                .Where(r => r.SkillId == booking.SkillId)
                .Select(r => r.Rating)
                .ToListAsync();
            skill.Rating = allRatings.Average();
            skill.NumberOfReviews = allRatings.Count;
            await _context.SaveChangesAsync();

            return Ok(newReview);
        }

        [HttpGet]
        public async Task<IActionResult> GetReviewsForSkill([FromQuery] int skillId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.SkillId == skillId)
                .ToListAsync();
            return Ok(reviews);
        }
    }
}
