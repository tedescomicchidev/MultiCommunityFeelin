namespace AgentOrchestration.Services;

public static class WeekWindow
{
    public static DateOnly CurrentWeekStart(DateTimeOffset now)
    {
        int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return DateOnly.FromDateTime(now.AddDays(-diff).Date);
    }

    public static string CurrentWeekLabel(DateTimeOffset now)
    {
        var start = CurrentWeekStart(now);
        var end = start.AddDays(6);
        return $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}";
    }
}
