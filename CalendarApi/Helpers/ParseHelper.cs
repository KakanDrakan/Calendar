namespace CalendarApi.Helpers
{
    public static class ParseHelper
    {
        public static bool TryParseResource(
            string resource,
            out string userId,
            out string? calendarId,
            out string eventId)
        {
            userId = "";
            calendarId = null;
            eventId = "";

            if (string.IsNullOrWhiteSpace(resource))
            {
                Console.WriteLine("Empty resource string");
                return false;
            }

            var parts = resource.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length >= 4 && parts[0].Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                userId = parts[1];

                if (parts[2].Equals("Events", StringComparison.OrdinalIgnoreCase))
                {
                    eventId = parts[3];
                    return true;
                }
                else if (parts.Length >= 6 &&
                         parts[2].Equals("Calendars", StringComparison.OrdinalIgnoreCase) &&
                         parts[4].Equals("Events", StringComparison.OrdinalIgnoreCase))
                {
                    calendarId = parts[3];
                    eventId = parts[5];
                    return true;
                }
            }

            Console.WriteLine($"Unexpected resource format: {resource}");
            return false;
        }
    }
}
