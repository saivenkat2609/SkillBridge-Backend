namespace DotnetLearning.Utils
{
    public static class EmailTemplates
    {
        public static string BookingRequestedStudent(string studentName, string skillTitle, DateTime scheduledAt) => $"""
            <h2>Booking Requested!</h2>
            <p>Hi {studentName},</p>
            <p>Your session for <strong>{skillTitle}</strong> on <strong>{scheduledAt:MMMM dd, yyyy}</strong> at <strong>{scheduledAt:h:mm tt}</strong> has been requested.</p>
            <p>The teacher will confirm shortly.</p>
            <p>— SkillSwap</p>
            """;

        public static string BookingRequestedTeacher(string teacherName, string studentName, string skillTitle, DateTime scheduledAt) => $"""
            <h2>New Booking Request</h2>
            <p>Hi {teacherName},</p>
            <p><strong>{studentName}</strong> has requested a session for <strong>{skillTitle}</strong> on <strong>{scheduledAt:MMMM dd, yyyy}</strong> at <strong>{scheduledAt:h:mm tt}</strong>.</p>
            <p>— SkillSwap</p>
            """;
    }
}
