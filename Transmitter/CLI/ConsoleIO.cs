using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Transmitter.CLI;

public static class ConsoleIO
{
    private static TextWriter? realConsoleTextWriter;
    private static TextReader? realConsoleTextReader;
    private static Encoding? savedOutEncoding;
    private static Encoding? savedInEncoding;

    private static bool IsInitialized
    {
        get
        {
            if ((realConsoleTextReader is null) ^ (realConsoleTextReader is null) ^ (savedOutEncoding is null) ^ (savedInEncoding is null))
                throw new InvalidOperationException("Incorrect state");
            return realConsoleTextReader is not null;
        }
    }

    public static void Initialize()
    {
        if (IsInitialized)
            throw new InvalidOperationException("Already initialized");

        (savedOutEncoding, savedInEncoding) = (Console.OutputEncoding, Console.InputEncoding);
        (Console.OutputEncoding, Console.InputEncoding) = (Encoding.Unicode, Encoding.Unicode);
        realConsoleTextWriter = Console.Out;
        realConsoleTextReader = Console.In;

        Console.SetOut(DummyTextWriter.Instance);
        Console.SetIn(DummyTextReader.Instance);

        Console.CursorVisible = false; // move this and all above fields to ConsoleStateContext, call it once per screen change

        Console.ResetColor();
        Console.Clear();
    }

    public static void Reset()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Not initialized");

        (Console.OutputEncoding, Console.InputEncoding) = (savedOutEncoding!, savedInEncoding!);
        Console.SetOut(realConsoleTextWriter!);
        Console.SetIn(realConsoleTextReader!);

        (savedOutEncoding, savedInEncoding) = (null, null);
        realConsoleTextWriter = null;
        realConsoleTextReader = null;

        Console.ResetColor();
        Console.Clear();
    }

    public static void ShowScreen(Screen screen)
    {
        screen.Draw();
    }

    private class DummyTextWriter : TextWriter
    {
        public static DummyTextWriter Instance { get; } = new();

        private DummyTextWriter() { }

        public override Encoding Encoding => throw Exception;
        public override void Write(char value) => throw Exception;

        private static Exception Exception => new InvalidOperationException($"Cannot write to regular {nameof(Console)} because {nameof(ConsoleIO)} is initialized");
    }

    private class DummyTextReader : TextReader
    {
        public static DummyTextReader Instance { get; } = new();

        private DummyTextReader() { }

        public override int Peek() => throw Exception;
        public override int Read() => throw Exception;

        private static Exception Exception => new InvalidOperationException($"Cannot read from regular {nameof(Console)} because {nameof(ConsoleIO)} is initialized");
    }

    #region Console mirror
    public static int CursorLeft { get => Console.CursorLeft; set => Console.CursorLeft = value; }
    public static int CursorTop { get => Console.CursorTop; set => Console.CursorTop = value; }
    public static Vector2 CursorPosition { get => (CursorLeft, CursorTop); set => (CursorLeft, CursorTop) = value; }

    public static int BufferWidth => Console.WindowWidth;
    public static int BufferHeight => Console.WindowHeight;
    public static Vector2 BufferSize => (BufferWidth, BufferHeight);

    public static void SetCursorPosition((int Left, int Top) position) => Console.SetCursorPosition(position.Left, position.Top);
    public static void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);

    public static void Write(char text) => realConsoleTextWriter!.Write(text);
    public static void Write(string text) => realConsoleTextWriter!.Write(text);

    public static void WriteLine() => realConsoleTextWriter!.WriteLine();
    public static void WriteLine(char text) => realConsoleTextWriter!.WriteLine(text);
    public static void WriteLine(string text) => realConsoleTextWriter!.WriteLine(text);

    public static Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<ConsoleKeyInfo> completionSource = new();
        _ = Task.Run(async () =>
        {
            completionSource.SetResult(Console.ReadKey(true));
        }, cancellationToken);
        return completionSource.Task;
    }

    public static void Clear() => Console.Clear();
    #endregion Console mirror
}

internal readonly ref struct ConsoleLock : IDisposable
{
    private static readonly SemaphoreSlim writeSemaphore = new(1);

    private readonly ConsoleStateContext consoleState;

    public ConsoleLock()
    {
        if (writeSemaphore.CurrentCount > 1)
            throw new InvalidOperationException("Deadlock");

        writeSemaphore.Wait();
        consoleState = new();
    }

    public readonly void Dispose()
    {
        consoleState.Dispose();
        writeSemaphore.Release();
    }
}

public readonly ref struct ConsoleStateContext : IDisposable
{
    private readonly (ConsoleColor, ConsoleColor) savedColors;
    private readonly bool cursorVisible;

    public ConsoleStateContext()
    {
        savedColors = (Console.ForegroundColor, Console.BackgroundColor);
        if (OperatingSystem.IsWindows())
            cursorVisible = Console.CursorVisible;
    }

    public readonly void Dispose()
    {
        (Console.ForegroundColor, Console.BackgroundColor) = savedColors;
        if (OperatingSystem.IsWindows())
            Console.CursorVisible = cursorVisible;
    }
}

public abstract class Screen(Domain domain)
{
    public Domain Domain { get; } = domain;

    public Screen() : this(new(0, ConsoleIO.BufferSize)) { }

    private CancellationTokenSource? inputTaskCancellation;

    private event Action? Done;

    protected void OnDone() => Done?.Invoke();

    protected abstract Element Root { get; }

    private static bool TryConvertKeyToNavAction(ConsoleKeyInfo keyInfo, [NotNullWhen(true)] out NavigationControlAction? navigationControlAction)
    {
        navigationControlAction = keyInfo.Key switch
        {
            ConsoleKey.UpArrow => new(Navigation.Up),
            ConsoleKey.RightArrow => new(Navigation.Right),
            ConsoleKey.DownArrow => new(Navigation.Down),
            ConsoleKey.LeftArrow => new(Navigation.Left),
            ConsoleKey.Enter => new(Navigation.Enter),
            ConsoleKey.Escape => new(Navigation.Exit),
            _ => null,
        };
        return navigationControlAction is not null;
    }

    private static IControlAction ConvertKeyToAction(ConsoleKeyInfo keyInfo)
    {
        if (TryConvertKeyToNavAction(keyInfo, out NavigationControlAction? action))
            return action;
        return new KeyboardInputControlAction(keyInfo);
    }

    public void EnableInput()
    {
        if (Root.Control is not ControlFlow flow)
            return;

        if (inputTaskCancellation is not null)
            throw new InvalidOperationException("Input already enabled");

        inputTaskCancellation = new();

        Task.Run(async () =>
        {
            while (inputTaskCancellation is not null)
            {
                ConsoleKeyInfo keyInfo = await ConsoleIO.ReadKeyAsync(inputTaskCancellation.Token);
                flow.AcceptAction(ConvertKeyToAction(keyInfo));
            }
        });
    }

    public void DisableInput()
    {
        if (Root.Control is not ControlFlow)
            return;

        if (inputTaskCancellation is null)
            throw new InvalidOperationException("Input is not enabled");

        inputTaskCancellation.Cancel();
    }

    public void Draw()
    {
        using ConsoleLock _ = new();
        ConsoleIO.CursorTop += 1;
        Root.Draw(Domain, Console.BackgroundColor, Console.BackgroundColor);
    }

    public Task WaitForInvalidationAsync()
    {
        TaskCompletionSource source = new();

        void Callback1()
        {
            source.TrySetResult();
            Root.Invalidated -= Callback1;
        }
        Root.Invalidated += Callback1;

        void CallBack2()
        {
            source.TrySetResult();
            Done -= CallBack2;
        }
        Done += CallBack2;

        return source.Task;
    }
}
