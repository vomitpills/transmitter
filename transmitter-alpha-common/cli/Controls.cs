namespace transmitter_alpha_common.cli;

public class ControlFlow : BlockingControl
{
    public IControl CurrentControl
    {
        get;
        private set
        {
            var oldValue = field;
            field = value;
            UpdateState(oldValue);
            UpdateState(field);
        }
    }
    private IBlockingControl? CurrentInputReceiver
    {
        get => field;
        set
        {
            var oldValue = field;
            field = value;
            oldValue?.IsBlockedBy = null;
            field?.IsBlockedBy = this;
            UpdateState(oldValue);
            UpdateState(field);
        }
    }

    private void UpdateState(IControl? control) => control?.SetState(GetState(control));

    private ControlState GetState(IControl? control)
    {
        if (ReferenceEquals(CurrentInputReceiver, control))
            return ControlState.Engaged;
        if (ReferenceEquals(CurrentControl, control))
            return ControlState.Selected;
        else
            return ControlState.Idle;
    }

    private readonly Dictionary<Navigation, ControlFlow> escapes = [];
    private readonly Dictionary<(IControl, Navigation), IControl> controls = [];

    public ControlFlow(IControl initialControl)
    {
        CurrentControl = initialControl;
    }

    public ControlFlow(IControl initialControl, Navigation action, IControl to)
    {
        CurrentControl = initialControl;
        Add(initialControl, action, to);
    }

    public ControlFlow Add(IControl from, NavigationControlAction action, IControl to)
    {
        if (action.Navigation is Navigation.Exit or Navigation.Enter) // replace with property and throw helper method
            throw new ArgumentOutOfRangeException($"Can't use {action} as a navigation action");
        controls.Add((from, action), to);
        controls.Add((to, action.GetOpposite()), from);
        return this;
    }

    private static void Link(ControlFlow from, NavigationControlAction action, ControlFlow to) => from.escapes[action] = to;

    public ControlFlow Link(ControlFlow flow, NavigationControlAction action)
    {
        if (action.Navigation is Navigation.Exit or Navigation.Enter) // replace with property and throw helper method
            throw new ArgumentOutOfRangeException($"Can't use {action} as a navigation action");
        Link(this, action, flow);
        Link(flow, action.GetOpposite(), this);
        flow.CurrentControl.SetState(ControlState.Idle);
        return this;
    }

    public override void AcceptAction(IControlAction action)
    {
        if (action is not NavigationControlAction navAction)
        {
            CurrentInputReceiver?.AcceptAction(action);
            return;
        }

        if (CurrentInputReceiver is not null)
        {
            if (CurrentInputReceiver is not ControlFlow && navAction.Navigation is Navigation.Exit)
                CurrentInputReceiver = null;
            else
                CurrentInputReceiver.AcceptAction(action);
            return;
        }
        
        switch (navAction.Navigation)
        {
            case Navigation.Enter:
                if (CurrentControl is IBlockingControl blockingControl)
                    CurrentInputReceiver = blockingControl;
                else
                    CurrentControl.AcceptAction();
                break;

            default:
                if (escapes.TryGetValue(navAction, out ControlFlow? flow))
                {
                    if (ReferenceEquals(flow, IsBlockedBy))
                    {
                        OnTrigger();
                        CurrentControl.SetState(ControlState.Idle);
                    }
                    else
                        Delegate(flow);
                }
                else if (controls.TryGetValue((CurrentControl, navAction), out IControl? next))
                    CurrentControl = next;
                break;
        }
    }

    private void Delegate(ControlFlow controlFlow)
    {
        if (controlFlow.CurrentInputReceiver is not null)
            throw new InvalidOperationException("blocking on flow switch");

        void ReturnToRoot(object? _)
        {
            controlFlow.Trigger -= ReturnToRoot;
            CurrentInputReceiver = null;
            CurrentControl.SetState(ControlState.Selected);
        }

        controlFlow.Trigger += ReturnToRoot;

        CurrentInputReceiver = controlFlow;
        CurrentControl.SetState(ControlState.Idle);
        controlFlow.CurrentControl.SetState(ControlState.Selected);
    }
}

public interface IControlAction { }

public readonly struct NavigationControlAction(Navigation navigation) : IControlAction
{
    public NavigationControlAction GetOpposite() => Navigation switch
    {
        Navigation.Up => Navigation.Down,
        Navigation.Right => Navigation.Left,
        Navigation.Down => Navigation.Up,
        Navigation.Left => Navigation.Right,

        _ => throw new ArgumentOutOfRangeException($"{Navigation} does not have an opposite action")
    };

    public Navigation Navigation { get; } = navigation;

    public static implicit operator Navigation(NavigationControlAction action) => action.Navigation;
    public static implicit operator NavigationControlAction(Navigation action) => new(action);
}

public enum Navigation
{
    Up, Right, Down, Left,
    Enter, Exit
}

public readonly struct KeyboardInputControlAction(ConsoleKeyInfo info) : IControlAction
{
    public ConsoleKeyInfo Input { get; } = info;
}

public enum ControlState
{
    Idle, Selected, Engaged, Unavailable
}

public interface IControl
{
    abstract void AcceptAction();
    abstract event Action<object?>? Trigger;
    abstract event Action<ControlState>? StateChanged;

    abstract ControlState CurrentState { get; }

    abstract void SetState(ControlState state);
}

public interface IBlockingControl : IControl
{
    abstract ControlFlow? IsBlockedBy { get; set; }
    abstract void AcceptAction(IControlAction action);
}

public abstract class Control : IControl
{
    public ControlState CurrentState { get; private set; }

    public abstract void AcceptAction();
    protected void OnTrigger() => Trigger?.Invoke(default);

    public event Action<object?>? Trigger;

    public event Action<ControlState>? StateChanged;
    public void SetState(ControlState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }
}

public abstract class Control<TTrigger> : IControl
{
    public ControlState CurrentState { get; private set; }

    public abstract void AcceptAction();
    protected void OnTrigger(TTrigger value) => Trigger?.Invoke(value);
    public event Action<object?>? Trigger;

    public event Action<ControlState>? StateChanged;
    public void SetState(ControlState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }
}

public class Button : Control
{
    public override void AcceptAction()
    {
        OnTrigger();
    }
}

public abstract class BlockingControl : Control, IBlockingControl
{
    public ControlFlow? IsBlockedBy { get; set; }

    public sealed override void AcceptAction() => throw new NotSupportedException();

    public abstract void AcceptAction(IControlAction action);
}

public abstract class BlockingControl<TTrigger> : Control<TTrigger>, IBlockingControl
{
    public ControlFlow? IsBlockedBy { get; set; }

    public sealed override void AcceptAction() => throw new NotSupportedException();

    public abstract void AcceptAction(IControlAction action);
}

public class Slider : BlockingControl<(int Value, int Step)>
{
    public int Value
    {
        get;
        set
        {
            field = int.Clamp(value, 0, Range);
            OnTrigger((Value, Step));
        }
    }

    public int Step
    {
        get;
        private set
        {
            field = int.Max(0, value);
            OnTrigger((Value, Step));
        }
    } = 1;

    public required int Range { get; init; }

    public override void AcceptAction(IControlAction action)
    {
        if (action is not NavigationControlAction navigationControlAction)
            return;

        switch (navigationControlAction.Navigation)
        {
            case Navigation.Left:
                Value -= Step;
                break;
            case Navigation.Right:
                Value += Step;
                break;
        }
    }
}

public class InputBox(string text = "") : BlockingControl<string>
{
    public string Text
    {
        get;
        private set
        {
            field = value;
            OnTrigger(field);
        }
    } = text;

    public bool Multiline { get; init; } = false;

    public int? MaxLength { get; init; }

    public override void AcceptAction(IControlAction action)
    {
        if (action is not KeyboardInputControlAction keyboardInputControlAction)
            return;

        switch (keyboardInputControlAction.Input.Key)
        {
            case ConsoleKey.Backspace:
                if (Text.Length > 0)
                    Text = Text[..^1];
                break;

            default:
                if (MaxLength is null || Text.Length < MaxLength)
                    Text += keyboardInputControlAction.Input.KeyChar;
                break;
        }

    }
}

public class ControlFlowContainer(ControlFlow controlFlow) : BlockingControl
{
    public ControlFlow ControlFlow { get; } = controlFlow;

    public override void AcceptAction(IControlAction action)
    {
        ControlFlow.AcceptAction(action);
    }
}

//public class AnyOf<T>(Func<T, IControl> factory, params IEnumerable<T> values) : Control<T> // use this for color selection
//{
//    public override void AcceptAction()
//    {
//        throw new NotImplementedException();
//    }
//}