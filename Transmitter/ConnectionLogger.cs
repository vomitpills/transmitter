using System.Text;

namespace Transmitter;

public class ConnectionLogger(string name, ConsoleColor? color = null)
{
    private static readonly ConsoleColor[] validColors = [ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.Yellow];

    public string Name { get; } = name;

    private static readonly bool enabled;
    private string NameDecoratedPadded => $"[{Name}]".PadRight(10);
    private ConsoleColor Color => color ?? StringToColor(Name);

    private static ConsoleColor StringToColor(string text) => validColors[int.Abs(text.GetHashCode()) % validColors.Length];

    static ConnectionLogger()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

#if ENABLE_LOGGING
        enabled = true;
#else
        enabled = false;
#endif
    }

    public void Log(string message) => Write(message);

    public ConnectionLogger CreateChild(string? childName = null, bool addPostfix = true)
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
        if (!enabled)
            return;

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