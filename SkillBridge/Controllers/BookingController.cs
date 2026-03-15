using SkillBridge.Models;
using SkillBridge.Services;
using SkillBridge.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SkillBridge.Controllers
{
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly BookingValidator _validator;
        private readonly IEmailService _emailService;
        public record BookingDto(int SkillId, DateTime ScheduledAt, int DurationInMinutes);
        public record MyBookingsDto(int bookingId, string skillTitle, string teacherName, DateTime scheduledAt, int durationMinutes, decimal totalPrice, string status);
        public BookingController(AppDbContext context, BookingValidator validator, IEmailService emailService)
        {
            _context = context;
            _validator = validator;
            _emailService = emailService;
        }
        [HttpPost]
        [Authorize]
        [Route("api/bookings")]
        public async Task<IActionResult> CreateBooking([FromBody] BookingDto bookingDto)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var skill = await _context.Skills.FindAsync(bookingDto.SkillId);
            if (skill == null)
            {
                return NotFound();
            }
            if (skill.TeacherId == null)
            {
                return BadRequest("This skill has no assigned teacher.");
            }
            var teacherId = skill.TeacherId;
            var totalPrice = skill.PricePerHour * ((decimal)bookingDto.DurationInMinutes / 60);
            var booking = new Booking
            {
                SkillId = bookingDto.SkillId,
                ScheduledAt = bookingDto.ScheduledAt,
                StudentId = studentId,
                TeacherId = teacherId,
                TotalPrice = totalPrice,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                DurationMinutes = bookingDto.DurationInMinutes,
            };
            var errors = _validator.Validate(booking, skill);
            if (errors.Count > 0)
            {
                return BadRequest(errors);
            }
            var conflict = await _context.Bookings.AnyAsync(b =>
                  b.TeacherId == skill.TeacherId &&
                  b.ScheduledAt == booking.ScheduledAt &&
                  b.Status != BookingStatus.Cancelled);
            if (conflict)
            {
                return BadRequest("Booking conflicts with an existing one.");
            }
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            var student = await _context.Users.FindAsync(studentId);
            var teacher = await _context.Users.FindAsync(teacherId);

            _ = _emailService.SendAsync(
                student!.Email!,
                $"Booking Requested — {skill.Title}",
                EmailTemplates.BookingRequestedStudent(student.FirstName, skill.Title, booking.ScheduledAt)
            );
            _ = _emailService.SendAsync(
                teacher!.Email!,
                $"New booking request from {student.FirstName}",
                EmailTemplates.BookingRequestedTeacher(teacher.FirstName, student.FirstName, skill.Title, booking.ScheduledAt)
            );

            return Ok(booking);
        }
        [HttpGet]
        [Route("api/bookings/my")]
        [Authorize]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bookings = await _context.Bookings
                .Where(b => b.StudentId == userId)
                .Include(b => b.Skill)
                .Include(b => b.Teacher)
                .ToListAsync();
            var result = bookings.Select(b => new MyBookingsDto(
                b.BookingId,
                b.Skill.Title,
                b.Teacher.FirstName,
                b.ScheduledAt,
                b.DurationMinutes,
                b.TotalPrice,
                b.Status.ToString()
            )).ToList();
            return Ok(result);
        }
    }
}
