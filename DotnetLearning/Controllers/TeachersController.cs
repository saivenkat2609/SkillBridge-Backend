using DotnetLearning.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DotnetLearning.Controllers
{
    [ApiController]
    public class TeachersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public record TeacherDto(string Bio, decimal HourlyRate);
        public record TimeDto(TimeSpan StartTime, TimeSpan EndTime, bool IsBooked);
        public record DayAvailabilityDto(DateTime Date, List<TimeDto> Slots);
        public TeachersController(AppDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("api/teachers/profile")]
        [Authorize(Roles = "Teacher")]
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
        [Route("api/teachers/{teacherId}/availability")]
        public async Task<IActionResult> getTeacherAvailability(string teacherId, DateTime fromDate, int durationInMinutes)
        {
            var toDate = new DateTime(fromDate.Year, fromDate.Month,
                            DateTime.DaysInMonth(fromDate.Year, fromDate.Month));
            var existingBookings = await _context.Bookings
                .Where(b => b.TeacherId == teacherId &&
                  b.ScheduledAt >= fromDate &&
                  b.ScheduledAt <= toDate &&
                  b.Status != BookingStatus.Cancelled)
                .ToListAsync();
            List<DayAvailabilityDto> availabilities = new List<DayAvailabilityDto>();
            var regularAvailability = await _context.TeacherAvailabilities.Where(a => a.TeacherId == teacherId).ToListAsync();
            var exceptions = await _context.AvailabilityExceptions.Where(e => e.TeacherId == teacherId && (e.ExceptionDate <= toDate && e.ExceptionDate >= fromDate)).ToListAsync();
            var duration = TimeSpan.FromMinutes(durationInMinutes);
            for (DateTime currentDay = fromDate; currentDay <= toDate; currentDay = currentDay.AddDays(1))
            {
                List<TimeDto> dailyTimeSlots = new List<TimeDto>();
                var dayOfWeek = currentDay.DayOfWeek;
                var dayAvailability = regularAvailability.FirstOrDefault(a => a.DayOfWeek == dayOfWeek);
                if (dayAvailability != null)
                {
                    var current = dayAvailability.StartTime;
                    while (current + duration <= dayAvailability.EndTime)
                    {
                        var slotDateTime = currentDay.Date + current;
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
                            var slotDateTime = currentDay.Date + current;
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
        [Route("api/teachers/availability")]
        [Authorize(Roles ="Teacher")]
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
    }
}
