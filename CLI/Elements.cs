using CLI.Visuals;

namespace CLI;

public interface IElement
{
    abstract IElement? Child { get; }
    abstract event Action? Invalidated;
}

public class Element
{
    private readonly (ConsoleColor? Stroke, ConsoleColor? Backdrop) originalTheme;

    public IVisual Visual { get; }
    public IControl Control { get; }
    public IElement? Child { get; }

    public Element(IVisual visual, IControl control, Action<ControlState, (ConsoleColor?, ConsoleColor?)>? styler = null, IElement? child = null)
    {
        Visual = visual;
        Control = control;
        Child = child;

        originalTheme = (visual.StrokeColor, visual.BackdropColor);

        Visual.Invalidated += () => Invalidated?.Invoke();
        Child?.Invalidated += () => Invalidated?.Invoke();

        if (styler is not null)
            control?.StateChanged += (state) => styler(state, originalTheme);
        control?.SetState(ControlState.Idle);
    }

    public void Draw(Domain domain, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null) => Visual.Draw(domain, parentStrokeColor, parentBackdropColor);

    public event Action? Invalidated;
}