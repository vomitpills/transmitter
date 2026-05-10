using CLI;

namespace CLI.Visuals;

using static ConsoleIO;

public interface IVisual
{
    abstract Vector2 ProbeShrunkSize(Domain availableSpace);
    abstract Domain ProbeDomain(Domain availableSpace);
    abstract void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null);
    abstract bool IsValid { get; set; }

    abstract ConsoleColor? StrokeColor { get; set; }
    abstract ConsoleColor? BackdropColor { get; set; }

    abstract bool IsHorizontallyGreedy { get; }
    abstract bool IsVerticallyGreedy { get; }

    abstract event Action? Invalidated;
}

public abstract class Visual(LayoutSettings layoutSettings) : IVisual
{
    public virtual ConsoleColor? StrokeColor
    {
        get;
        set
        {
            field = value;
            IsValid = false;
        }
    }
    public virtual ConsoleColor? BackdropColor
    {
        get;
        set
        {
            field = value;
            IsValid = false;
        }
    }

    public virtual bool IsValid
    {
        get;
        set
        {
            field = value;
            if (!value)
                OnInvalidated();
        }
    } = false;

    public bool IsHorizontallyGreedy => LayoutSettings.HorizontalAlignment is VisualAlignment.Fill;
    public bool IsVerticallyGreedy => LayoutSettings.VerticalAlignment is VisualAlignment.Fill;

    public event Action? Invalidated;

    private void OnInvalidated() => Invalidated?.Invoke();

    protected ConsoleStateContext ApplyColors(ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null, bool invert = false)
    {
        ConsoleStateContext state = new();

        ConsoleColor? strokeColor = StrokeColor ?? parentStrokeColor;
        ConsoleColor? backdropColor = BackdropColor ?? parentBackdropColor;

        if (invert)
            (strokeColor, backdropColor) = (backdropColor, strokeColor);

        if (strokeColor is ConsoleColor stroke)
            Console.ForegroundColor = stroke;
        if (backdropColor is ConsoleColor backdrop)
            Console.BackgroundColor = backdrop;

        return state;
    }

    protected LayoutSettings LayoutSettings { get; } = layoutSettings;

    protected Vector2 GetDesiredSize(Domain availableSpace, Vector2 shrunkSize) => (LayoutSettings.HorizontalAlignment, LayoutSettings.VerticalAlignment) switch
    {
        (VisualAlignment.Fill, VisualAlignment.Fill) => availableSpace.Size,
        (VisualAlignment.Fill, _) => (availableSpace.Size.X, shrunkSize.Y),
        (_, VisualAlignment.Fill) => (shrunkSize.X, availableSpace.Size.Y),
        (_, _) => shrunkSize
    };

    public static Domain GetBoundingBox(Domain availableSpace, Vector2 desiredSize, LayoutSettings layoutSettings)
    {
        availableSpace = availableSpace.Shrink(layoutSettings.Margin);

        desiredSize -= layoutSettings.Margin.Delta;

        int anchorX = layoutSettings.HorizontalAlignment switch
        {
            VisualAlignment.Fill => availableSpace.Position.X,
            VisualAlignment.Center => availableSpace.Position.X + (availableSpace.Size.X - desiredSize.X) / 2,
            VisualAlignment.Nearest => availableSpace.Position.X,
            VisualAlignment.Furthest => availableSpace.Position.X + availableSpace.Size.X - desiredSize.X,

            _ => throw new NotImplementedException()
        };

        int anchorY = layoutSettings.VerticalAlignment switch
        {
            VisualAlignment.Fill => availableSpace.Position.Y,
            VisualAlignment.Center => availableSpace.Position.Y + (availableSpace.Size.Y - desiredSize.Y) / 2,
            VisualAlignment.Nearest => availableSpace.Position.Y,
            VisualAlignment.Furthest => availableSpace.Position.Y + availableSpace.Size.Y - desiredSize.Y,

            _ => throw new NotImplementedException()
        };

        return new((anchorX, anchorY), desiredSize);
    }

    public interface IOrientable
    {
        public bool IsVertical { get; init; }
    }

    public Domain GetBoundingBox(Domain availableSpace, Vector2 desiredSize) => GetBoundingBox(availableSpace, desiredSize, LayoutSettings);

    protected static string Pad(int length) => new(' ', length);

    protected static void DrawRectangle(Domain domain)
    {
        for (int i = 0; i < domain.Size.Y; i++)
        {
            CursorPosition = domain.Position + (0, i);
            Write(Pad(domain.Size.X));
        }
    }

    public Domain ProbeDomain(Domain availableSpace)
    {
        Vector2 desiredSize = GetDesiredSize(availableSpace, ProbeShrunkSize(availableSpace));
        return GetBoundingBox(availableSpace, desiredSize);
    }

    public abstract void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null);
    public abstract Vector2 ProbeShrunkSize(Domain availableSpace);
}

public enum VisualAlignment
{
    Fill, Center, Nearest, Furthest
}