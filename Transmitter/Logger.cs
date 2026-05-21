using System.Text;

namespace Transmitter;

public class Logger(string name, ConsoleColor? color = null)
{
    private static readonly ConsoleColor[] validColors = [ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Cyan];

    public string Name { get; } = name;

    private string NameDecoratedPadded => $"[{Name}]".PadRight(10);
    private ConsoleColor Color => color ?? StringToColor(Name);

    private static ConsoleColor StringToColor(string text) => validColors[int.Abs(text.GetHashCode()) % validColors.Length];

    static Logger()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
    }

    public void Log(string message) => Write(message);

    public Logger CreateChild(string? childName = null, bool addPostfix = true)
    {
        string name = Name;
        if (childName is not null)
            name += $".{childName}";
        if (addPostfix)
            name += $"-{Random.Shared.GetHexString(6)}";
        return new(name);
    }

    private static readonly SemaphoreSlim writeSemaphore = new(1);

    private void Write(string message, ConsoleColor? foreground = null, ConsoleColor? background = null, bool decorateName = true)
    {
        DateTime timestamp = DateTime.Now;
        writeSemaphore.Wait();
        try
        {      
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = Color;
            Console.Write($"{timestamp:HH:mm:ss.fff}");
            Console.ForegroundColor = Color;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(' ' + (decorateName? NameDecoratedPadded : Name) + ' ');

            if (foreground is ConsoleColor fg)
                Console.ForegroundColor = fg;
            if (background is ConsoleColor bg)
                Console.BackgroundColor = bg;

            Console.WriteLine(message);
            Console.ResetColor();
        }
        finally
        {
            writeSemaphore.Release();
        }
    }
}