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

        public static void WriteLineColored(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
