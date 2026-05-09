using System.Diagnostics;
using System.Text;

namespace transmitter_alpha_common;

public class Logger(string name, ConsoleColor? color = null)
{
    public static Logger Shared { get; } = new("<shared>");

    private static bool LastEphemeral = false;

    public string Name { get; } = name;

    static Logger()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
    }

    private static bool isInputEnabled = false;
    public static void EnableInput()
    {
        isInputEnabled = true;
        WriteInput();
    }
    public static void DisableInput()
    {
        EraseInput();
        isInputEnabled = false;
    }

    public static void Reset()
    {
        LastEphemeral = false;
        Console.Clear();
    }

    public static void LogInputError(string message, string? title = null) => LogNote(title, message, ConsoleColor.Red);
    public static void LogInputNote(string message) => LogNote(null, message, ConsoleColor.White);

    public static void LogMessage(Profile profile, string content, bool doBeep = true)
    {
        if (doBeep && OperatingSystem.IsWindows())
            Console.Beep(profile.BeepFrequency, 100);
        LogNote($"from: {profile.DisplayName}", content, profile.Color);
    }

    public static void LogProfileUpdate(Profile oldProfile, Profile newProfile)
    {
        static void LogProfile(Profile profile)
        {
            LogNote(null, profile.DisplayName, profile.Color);
        }

        if (OperatingSystem.IsWindows())
        {
            Console.Beep(oldProfile.BeepFrequency, 100);
            Console.Beep(newProfile.BeepFrequency, 100);
        }

        LogProfile(oldProfile);
        LogProfile(newProfile);

        if (isInputEnabled)
            EraseInput();

        writeSemaphore.Wait();
        try
        {
            Console.CursorTop -= 7;
            int arrowHeight = 3;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            for (int i = 0; i < arrowHeight; i++)
            {
                Console.CursorLeft = Console.BufferWidth - 5 - 4;
                Console.CursorTop++;
                Console.Write(i == arrowHeight - 1? "▼" : '│');
            }
            Console.CursorTop += 6 - arrowHeight;
        }
        finally
        {
            writeSemaphore.Release();
        }

        if (isInputEnabled)
            WriteInput();
    }

    public static void LogNote(string? title, string content, ConsoleColor? color = null)
    {
        int horizontalPadding = 5;
        int contentLineWidth = Console.BufferWidth - horizontalPadding * 2 - 2 - 2;
        ConsoleColor bg = color ?? StringToColor(title ?? string.Empty);

        void SetColor()
        {
            Console.BackgroundColor = bg;
            Console.ForegroundColor = ConsoleColor.Black;
        }

        void DrawLine(string starter, char filler, char terminator)
        {
            Console.SetCursorPosition(horizontalPadding, Console.CursorTop);
            SetColor();
            Console.Write(starter);
            Console.Write(new string(filler, Console.BufferWidth - Console.CursorLeft - 1 - horizontalPadding) + terminator);
            Console.ResetColor();
            Console.WriteLine();
        }

        writeSemaphore.Wait();
        try
        {
            if (isInputEnabled)
                EraseInput();

            if (LastEphemeral)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                ClearLine();
            }

            DrawLine("┌", '─', '┐');
            if (title is not null)
            {
                DrawLine($"│{title}", ' ', '│');
                DrawLine("├", '─', '┤');
            }

            foreach (var line in content.ReplaceLineEndings().Split(Environment.NewLine).SelectMany(s => s.Chunk(contentLineWidth - 1)))
                DrawLine($"│ {new string(line)}", ' ', '│');

            DrawLine("└", '─', '┘');

            Console.WriteLine();
            LastEphemeral = false;
        }
        finally
        {
            writeSemaphore.Release();
        }
        if (isInputEnabled)
            WriteInput();
    }

    public static void LogReceipt(string from, string content)
    {
        int horizontalPadding = 5;
        ConsoleColor bg = StringToColor(from);

        void SetColor()
        {
            Console.BackgroundColor = bg;
            Console.ForegroundColor = ConsoleColor.Black;
        }

        writeSemaphore.Wait();
        try
        {
            EraseInput();
            Console.WriteLine();

            if (LastEphemeral)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 2);
                ClearLine();
            }

            Console.SetCursorPosition(horizontalPadding, Console.CursorTop);
            SetColor();
            Console.Write($"* {from} has received your message");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine();
            LastEphemeral = false;
        }
        finally
        {
            writeSemaphore.Release();
        }
        WriteInput();
    }

    public static string Input { get; private set; } = string.Empty;

    public static string? LogInput(ConsoleKeyInfo consoleKeyInfo)
    {
        static void Error()
        {
            _ = Task.Run(() =>
            {
                if (OperatingSystem.IsWindows())
                    Console.Beep(500, 10);
            });
        }

        if (!isInputEnabled)
            throw new InvalidOperationException();


        switch (consoleKeyInfo.Key)
        {
            case ConsoleKey.Backspace:
                if (Input.Length > 0)
                    Input = Input[..^1];
                else
                    Error();
                break;

            case ConsoleKey.Enter:
                if (Input.Length <= 0)
                {
                    Error();
                    break;
                }
                string savedInput = Input;
                Input = string.Empty;
                return savedInput;

            default:
                if (Input.Length < Console.BufferWidth - 3)
                    Input += consoleKeyInfo.KeyChar;
                else
                    Error();
                break;
        }
        EraseInput();
        WriteInput();
        return null;
    }

    public Logger CreateChild(string? childName = null) => new($"{Name}.{childName ?? Random.Shared.GetHexString(6)}");

    private static readonly SemaphoreSlim writeSemaphore = new(1);

    public void Write(string message, ConsoleColor? foreground = null, ConsoleColor? background = null, bool isEphemeral = false, bool decorateName = true)
    {
        writeSemaphore.Wait();
        try
        {
            if (isInputEnabled)
                EraseInput();

            if (LastEphemeral)
                Console.SetCursorPosition(0, Console.CursorTop - 1);

            Console.ForegroundColor = Color;
            Console.Write((decorateName? NameDecoratedPadded : Name) + ' ');

            if (foreground is ConsoleColor fg)
                Console.ForegroundColor = fg;
            if (background is ConsoleColor bg)
                Console.BackgroundColor = bg;

            Console.Write(message);

            Console.ResetColor();
            if (LastEphemeral)
                ClearLine();
            else
                Console.WriteLine();

            LastEphemeral = isEphemeral;

        }
        finally
        {
            writeSemaphore.Release();
        }
        if (isInputEnabled)
            WriteInput();
    }

    private static void ClearLine() => Console.WriteLine(new string(' ', Console.BufferWidth - Console.CursorLeft - 1));

    private static void EraseInput()
    {
        if (!isInputEnabled)
            throw new InvalidOperationException();

        Console.SetCursorPosition(0, Console.CursorTop - 1);
        ClearLine();
        Console.SetCursorPosition(0, Console.CursorTop - 2);
    }

    private static void WriteInput()
    {
        writeSemaphore.Wait();
        try
        {

            if (!isHighlightActive)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine();
                Console.Write($"╰→ ");
                Console.WriteLine(Input);
                Console.ResetColor();
                return;
            }

            string[] split = [.. Input.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s))];

            Console.ForegroundColor = split.Length switch
            {
                0 => ConsoleColor.White,
                1 => ConsoleColor.Green,
                _ => ConsoleColor.Yellow
            };
            Console.WriteLine();
            Console.Write($"╰→ ");

            for (int i = 0; i < split.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.Black;
                Console.BackgroundColor = i is 0? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.Write(split[i]);
                Console.ResetColor();

                if (i < split.Length - 1)
                    Console.Write(' ');
            }
            Console.WriteLine();
        }
        finally
        {
            writeSemaphore.Release();
        }
    }

    public void LogAbort(string process = "")
    {
        Write($"ABORTED {process.ToUpper()}", ConsoleColor.Yellow);
    }

    public void LogWrite(byte[] data)
    {
        Write($"W {BitConverter.ToString(data)}", ConsoleColor.DarkGray);
    }

    public void LogRead(byte[] data)
    {
        Write($"R {BitConverter.ToString(data)}");
    }

    public void LogProgress(string message)
    {
        Write(message, ConsoleColor.Black, Color);
    }

    public void LogRead(Memory<byte> data) => LogRead(data.ToArray());

    private string NameDecoratedPadded => $"[{Name}]".PadRight(10);

    private static readonly ConsoleColor[] validColors = [ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Cyan];
    private static ConsoleColor StringToColor(string text) => validColors[int.Abs(text.GetHashCode()) % validColors.Length];
    private ConsoleColor Color => color ?? StringToColor(Name);

    public ContextTracker LogContext(string action, string? @object = null) => new($"{action}" + (@object is null ? string.Empty : " " + @object), this);

    private static bool isHighlightActive = false;

    public static void EnableHighlight() => isHighlightActive = true;

    public static void DisableHighlight() => isHighlightActive = false;

    public sealed class ContextTracker : IDisposable
    {
        private readonly string name;
        private readonly Logger logger;

        public ContextTracker(string name, Logger? logger)
        {
            this.name = name;
            this.logger = logger ?? Shared;
            this.logger.Write($"=== BEGIN {name.ToUpper()} ===", ConsoleColor.DarkGray, null, true);
        }

        public void Write(string message)
        {
            logger.Write($"{name.ToUpper()}: {message}", ConsoleColor.Gray);
        }

        public void Dispose()
        {
            logger.Write($"=== FINISH {name.ToUpper()} ===", ConsoleColor.DarkGray, null, true);
            GC.SuppressFinalize(this);
        }
    }
}

public class LoggerBinaryStream(Stream innerStream, Logger logger) : Stream, IDisposable
{
    public override bool CanRead => innerStream.CanRead;

    public override bool CanSeek => innerStream.CanSeek;

    public override bool CanWrite => innerStream.CanWrite;

    public override long Length => innerStream.Length;

    public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

    [DebuggerHidden]
    public override void Flush()
    {
        innerStream.Flush();
    }

    [DebuggerHidden]
    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = innerStream.Read(buffer, offset, count);
        logger.LogRead(buffer);
        return bytesRead;
    }

    [DebuggerHidden]
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        logger.LogRead(buffer);
        return bytesRead;
    }

    [DebuggerHidden]
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await innerStream.ReadAsync(buffer, cancellationToken);
        logger.LogRead(buffer);
        return bytesRead;
    }

    [DebuggerHidden]
    public override long Seek(long offset, SeekOrigin origin)
    {
        return innerStream.Seek(offset, origin);
    }

    [DebuggerHidden]
    public override void SetLength(long value)
    {
        innerStream.SetLength(value);
    }

    [DebuggerHidden]
    public override void Write(byte[] buffer, int offset, int count)
    {
        logger.LogWrite(buffer);
        innerStream.Write(buffer, offset, count);
    }
}