
namespace DotnetLearning.Utils
{
    public static class EmailTemplates
    {
        public static string BookingRequestedStudent(string studentName, string skillTitle, DateTime scheduledAt) => $"""
            <h2>Booking Requested!</h2>
            <p>Hi {studentName},</p>
            <p>Your session for <strong>{skillTitle}</strong> on <strong>{scheduledAt:MMMM dd, yyyy}</strong> at <strong>{scheduledAt:h:mm tt}</strong> has been requested.</p>
            <p>The teacher will confirm shortly.</p>
            <p>— SkillBridge</p>
            """;

        public static string BookingRequestedTeacher(string teacherName, string studentName, string skillTitle, DateTime scheduledAt) => $"""
            <h2>New Booking Request</h2>
            <p>Hi {teacherName},</p>
            <p><strong>{studentName}</strong> has requested a session for <strong>{skillTitle}</strong> on <strong>{scheduledAt:MMMM dd, yyyy}</strong> at <strong>{scheduledAt:h:mm tt}</strong>.</p>
            <p>— SkillBridge</p>
            """;

        public static string ConfirmEmail(string firstName, string url) => $"""
            <h2>Confirm your email</h2>
            <p>Hi {firstName},</p>
            <p>Please confirm your email address to complete the registration process.</p>
            <p><a href="{url}">Confirm Email</a></p>
            <p>— SkillBridge</p>
            """;
        public static string ResetPassword(string firstName, string url) => $"""
            <h2>Password Reset Request</h2>
            <p>Hi {firstName},</p>
            <p>We received a request to reset your password. Click the link below to choose a new password:</p>
            <p><a href="{url}">Reset Password</a></p>
            <p>If you didn't request this, just ignore this email.</p>
            <p>— SkillBridge</p>
            """;
    }
}
