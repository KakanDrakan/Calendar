namespace CalendarApi.Helpers
{
    public static class ConsoleHelper
    {
        public static void WriteTimeToConsole()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{DateTime.Now:T}] ");
            Console.ResetColor();
        }
    }
}
