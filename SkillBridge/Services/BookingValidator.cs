using SkillBridge.Models;

namespace SkillBridge.Services
{
    public class BookingValidator
    {
        public List<string> Validate(Booking booking, Skill skill)
        {
            var bookingTime = booking.ScheduledAt;
            
            List<string> errors = new List<string>();
            if(bookingTime < DateTime.UtcNow)
            {
                errors.Add("Booking time must be in the future.");
            }
            else if (bookingTime < DateTime.UtcNow.AddHours(24))
            {
                errors.Add("Booking must be at least 24 hours in advance.");
            }
            if (skill.TeacherId == booking.StudentId)
            {
                errors.Add("Cannot book yourself.");
            }
            if (skill?.IsActive != true)
            {
                errors.Add("Skill is not active.");
            }

            return errors;
        }
    }
}
