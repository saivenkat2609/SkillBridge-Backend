using SkillBridge.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SkillBridge.Controllers
{
    [ApiController]
    public class TeachersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public record TeacherDto(string Bio, decimal HourlyRate);
        public record TimeDto(TimeSpan StartTime, TimeSpan EndTime, bool IsBooked);
        public record DayAvailabilityDto(DateTime Date, List<TimeDto> Slots);
        public record GetTeacherStats(List<Skill> TeacherSkills,List<BookingDto> UpcomingBookings, List<BookingDto> CompletedBookings,
            decimal TotalEarnings, double AverageRating);
        public record BookingDto(int BookingId, string SkillTitle, string LearnerName,
      DateTime ScheduledAt, int DurationMinutes, decimal TotalPrice, string Status);
        public TeachersController(AppDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [Route("api/teachers/profile")]
        public async Task<IActionResult> createTeacherProfile([FromBody] TeacherDto teacherDetails)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var teacherProfile = await _context.TeacherProfiles
            .FirstOrDefaultAsync(p => p.ApplicationUserId == userId);

            if (teacherProfile != null)
            {
                teacherProfile.Bio = teacherDetails.Bio;
                teacherProfile.HourlyRate = teacherDetails.HourlyRate;
            }
            else
            {
                var teacher = new TeacherProfile
                {
                    Bio = teacherDetails.Bio,
                    HourlyRate = teacherDetails.HourlyRate,
                    ApplicationUserId = userId,
                };
                _context.TeacherProfiles.Add(teacher);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpGet]
        [Authorize]
        [Route("api/teachers/{teacherId}/availability")]
        public async Task<IActionResult> getTeacherAvailability(string teacherId, DateTime fromDate, int durationInMinutes)
        {
            var fromDateUtc = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
            var toDate = new DateTime(fromDateUtc.Year, fromDateUtc.Month,
                            DateTime.DaysInMonth(fromDateUtc.Year, fromDateUtc.Month),
                            23, 59, 59, DateTimeKind.Utc);
            var existingBookings = await _context.Bookings
                .Where(b => b.TeacherId == teacherId &&
                  b.ScheduledAt >= fromDateUtc &&
                  b.ScheduledAt <= toDate &&
                  b.Status != BookingStatus.Cancelled)
                .ToListAsync();
            List<DayAvailabilityDto> availabilities = new List<DayAvailabilityDto>();
            var regularAvailability = await _context.TeacherAvailabilities.Where(a => a.TeacherId == teacherId).ToListAsync();
            var exceptions = await _context.AvailabilityExceptions.Where(e => e.TeacherId == teacherId && (e.ExceptionDate <= toDate && e.ExceptionDate >= fromDate)).ToListAsync();
            var duration = TimeSpan.FromMinutes(durationInMinutes);
            for (DateTime currentDay = fromDateUtc; currentDay <= toDate; currentDay = currentDay.AddDays(1))
            {
                List<TimeDto> dailyTimeSlots = new List<TimeDto>();
                var dayOfWeek = currentDay.DayOfWeek;
                var dayAvailability = regularAvailability.FirstOrDefault(a => a.DayOfWeek == dayOfWeek);
                if (dayAvailability != null)
                {
                    var current = dayAvailability.StartTime;
                    while (current + duration <= dayAvailability.EndTime)
                    {
                        var slotDateTime = DateTime.SpecifyKind(currentDay.Date + current, DateTimeKind.Utc);
                        var isBooked = existingBookings.Any(b => b.ScheduledAt == slotDateTime);

                        dailyTimeSlots.Add(new TimeDto(current, current + duration, isBooked));
                        current = current + duration;
                    }
                }
                var currentDayException = exceptions.FirstOrDefault(e => e.ExceptionDate.Date == currentDay.Date);
                if (currentDayException != null)
                {
                    var current = currentDayException.StartTime;
                    while (current + duration <= currentDayException.EndTime)
                    {
                        if (currentDayException.Type == ExceptionType.Block)
                        {
                            dailyTimeSlots.RemoveAll(s => s.StartTime == current && s.EndTime == current + duration);
                        }
                        else
                        {
                            var slotDateTime = DateTime.SpecifyKind(currentDay.Date + current, DateTimeKind.Utc);
                            var isBooked = existingBookings.Any(b => b.ScheduledAt == slotDateTime);
                            dailyTimeSlots.Add(new TimeDto(current, current + duration, isBooked));
                        }
                        current = current + duration;
                    }
                }

                availabilities.Add(new DayAvailabilityDto(currentDay, dailyTimeSlots));
            }
            return Ok(availabilities);
        }
        public record AvailabilityDto(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime);
        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [Route("api/teachers/availability")]
        public async Task<IActionResult> setTeacherAvailability([FromBody] List<AvailabilityDto> availabilityDtos)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingAvailability = await _context.TeacherAvailabilities
                .Where(a => a.TeacherId == userId)
                .ToListAsync();
            _context.TeacherAvailabilities.RemoveRange(existingAvailability);
            foreach (var availability in availabilityDtos)
            {
                _context.TeacherAvailabilities.Add(new TeacherAvailability
                {
                    TeacherId = userId,
                    DayOfWeek = availability.DayOfWeek,
                    StartTime = availability.StartTime,
                    EndTime = availability.EndTime
                });
            }
            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpGet]
        [Authorize(Roles = "Teacher")]
        [Route("/api/teachers/stats")]
        public async Task<IActionResult> getTeacherStats()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var teacherSkills = await _context.Skills.Where(a => a.TeacherId == currentUserId).ToListAsync();
            var bookings = await _context.Bookings.Where(a => a.TeacherId == currentUserId).Include(b => b.Skill)
      .Include(b => b.Student).ToListAsync();
            var upcomingBookings = bookings.Where(a => a.ScheduledAt > DateTime.UtcNow).Select(b => new BookingDto(b.BookingId, b.Skill.Title, b.Student.UserName,
                                  b.ScheduledAt, b.DurationMinutes, b.TotalPrice, b.Status.ToString())).ToList();
            var completedBookings = bookings.Where(a => a.ScheduledAt < DateTime.UtcNow && a.Status!=BookingStatus.Cancelled).Select(b => new BookingDto(b.BookingId, b.Skill.Title, b.Student.UserName,
                                  b.ScheduledAt, b.DurationMinutes, b.TotalPrice, b.Status.ToString())).ToList();
            var totalEarnings = bookings.Sum(a => a.TotalPrice);
            var averageRating = teacherSkills.Count > 0 ? teacherSkills.Average(a => a.Rating) : 0.0;
            return Ok(new GetTeacherStats(
                TeacherSkills: teacherSkills,
                UpcomingBookings: upcomingBookings,
                CompletedBookings: completedBookings,
                TotalEarnings: totalEarnings,
                AverageRating: averageRating
            ));
        }

    }
}
