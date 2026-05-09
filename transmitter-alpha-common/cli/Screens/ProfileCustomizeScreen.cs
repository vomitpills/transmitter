namespace transmitter_alpha_common.cli;

public class ProfileCustomizeScreen : Screen
{
    public Profile? Profile
    {
        get
        {
            if (Status is false)
                return null;

            try
            {
                return new(inputBoxControl.Text, selectedColor, selectedFrequency);
            }
            catch (ArgumentOutOfRangeException)
            {
                if (Status is true)
                    throw new InvalidOperationException($"{nameof(Status)} was true, but the {nameof(Profile)} constructor failed");
                return null;
            }
        }
    }

    public bool? Status
    {
        get;
        private set
        {
            field = value;
            OnDone();
        }
    }

    private ConsoleColor selectedColor = Profile.UserColors[Random.Shared.Next(Profile.UserColors.Count)];
    private int selectedFrequency;

    private readonly Slider sliderControl = new() { Range = 5, Value = Random.Shared.Next(5 + 1)};
    private readonly InputBox inputBoxControl = new() { MaxLength = Profile.MaxNameLength };
    private readonly List<Button> colorButtons = [];

    protected override Element Root { get; }

    private static readonly int hintPadding = 16;

    private static WrapPanel AddHint(IVisual visual, string hint, out TextVisual hintVisual)
    {
        LayoutSettings hintLayout = new() { HorizontalAlignment = VisualAlignment.Furthest, VerticalAlignment = VisualAlignment.Center };
        LayoutSettings panelLayout = new() { HorizontalAlignment = VisualAlignment.Fill, VerticalAlignment = VisualAlignment.Center };
        hintVisual = new(hintLayout, hint.PadLeft(hintPadding) + " ") { StrokeColor = ConsoleColor.White };
        return new WrapPanel(panelLayout, [hintVisual, visual]);
    }

    private Element GetInputBoxElement(Profile? oldProfile)
    {
        LayoutSettings defaultLayout = new() { HorizontalAlignment = VisualAlignment.Fill, VerticalAlignment = VisualAlignment.Center };
        LayoutSettings defaultLayout2 = new() { HorizontalAlignment = VisualAlignment.Nearest, VerticalAlignment = VisualAlignment.Center };
        string idlePlaceholderString = "Type here";
        string selectedPlaceholderString = "Enter to focus";
        TextVisual textVisual = new(defaultLayout2);
        Box box = new(defaultLayout, textVisual);

        void UpdateTextVisual(string text, string placeholderText)
        {
            textVisual.Text = "╰→ " + (text == string.Empty? placeholderText : text);
        }

        UpdateTextVisual(inputBoxControl.Text, idlePlaceholderString);

        inputBoxControl.Trigger += (obj) =>
        {
            if (obj is not string text)
                return;

            UpdateTextVisual(text, inputBoxControl.CurrentState is ControlState.Engaged? string.Empty : idlePlaceholderString);
        };

        IVisual finalVisual = AddHint(box, "Display name", out TextVisual hint);

        inputBoxControl.StateChanged += (state) =>
        {
            switch (state)
            {
                case ControlState.Selected:
                    UpdateTextVisual(inputBoxControl.Text, selectedPlaceholderString);
                    textVisual.StrokeColor = ConsoleColor.Yellow;
                    textVisual.BackdropColor = null;
                    box.StrokeColor = ConsoleColor.Yellow;
                    box.BackdropColor = null;
                    hint.StrokeColor = ConsoleColor.Yellow;
                    break;
                case ControlState.Engaged:
                    UpdateTextVisual(inputBoxControl.Text, string.Empty);
                    textVisual.StrokeColor = ConsoleColor.Black;
                    box.StrokeColor = ConsoleColor.Black;
                    textVisual.BackdropColor = ConsoleColor.Yellow;
                    box.BackdropColor = ConsoleColor.Yellow;
                    hint.StrokeColor = ConsoleColor.Yellow;
                    break;
                default:
                    UpdateTextVisual(inputBoxControl.Text, idlePlaceholderString);
                    textVisual.StrokeColor = ConsoleColor.White;
                    textVisual.BackdropColor = null;
                    box.StrokeColor = ConsoleColor.White;
                    box.BackdropColor = null;
                    hint.StrokeColor = ConsoleColor.White;
                    break;
            }
        };

        ControlFlow controlFlow = new(inputBoxControl);

        return new(finalVisual, controlFlow);
    }

    private Element GetColorButtons(Profile? oldProfile)
    {
        LayoutSettings defaultLayout = new() { HorizontalAlignment = VisualAlignment.Center, VerticalAlignment = VisualAlignment.Center };
        LayoutSettings fillLayout = new() { HorizontalAlignment = VisualAlignment.Fill, VerticalAlignment = VisualAlignment.Fill, Margin = (1, 0) };
        LayoutSettings boxLayout = new() { Padding = (1, 0), HorizontalAlignment = VisualAlignment.Center, VerticalAlignment = VisualAlignment.Furthest };

        Line? previousLine = null;
        WrapPanel? panel = null;
        IEnumerable<Element> GenerateColorButtons()
        {
            List<IVisual> panelChildren = [];
            foreach ((int index, ConsoleColor color) in Profile.UserColors.Index())
            {
                Rectangle rectangle = new(boxLayout, (3, 1)) { BackdropColor = color };
                Box box = new(boxLayout, rectangle) { StrokeColor = color };
                Line line = new(defaultLayout);
                StackPanel stackPanel = new(defaultLayout, [box, line]);                

                panelChildren.Add(stackPanel);
                Button control = new();

                colorButtons.Add(control);

                void ShowLine()
                {
                    previousLine?.StrokeColor = null;
                    previousLine?.BackdropColor = null;
                    line.BackdropColor = color;
                    line.StrokeColor = ConsoleColor.Black;
                    previousLine = line;
                }

                control.Trigger += (obj) =>
                {
                    ShowLine();
                    selectedColor = color;
                };

                yield return new(stackPanel, control, (state, theme) =>
                {
                    (ConsoleColor? originalStroke, ConsoleColor? originalBackdrop) = theme;
                    switch (state)
                    {
                        case ControlState.Selected:
                            box.BackdropColor = color;
                            ConsoleColor selectionColor = color is ConsoleColor.White? ConsoleColor.Black : ConsoleColor.White;
                            box.StrokeColor = selectionColor;
                            rectangle.BackdropColor = selectionColor;
                            break;

                        case ControlState.Engaged:
                            ShowLine();
                            break;

                        default:
                            box.StrokeColor = color;
                            box.StrokeColor = ConsoleColor.White;
                            box.BackdropColor = null;
                            rectangle.BackdropColor = color;
                            break;
                    }
                });

                if (selectedColor == color)
                    control.SetState(ControlState.Engaged);
            }
            panel = new(defaultLayout, panelChildren);
        }

        var buttons = GenerateColorButtons();
        var buttonsEnumerator = buttons.GetEnumerator();
        if (!buttonsEnumerator.MoveNext())
            throw new ArgumentOutOfRangeException();
        ControlFlow flow = new(buttonsEnumerator.Current.Control!);

        IControl previous = buttonsEnumerator.Current.Control!;
        while (buttonsEnumerator.MoveNext())
        {
            flow.Add(previous, Navigation.Right, buttonsEnumerator.Current.Control!);
            previous = buttonsEnumerator.Current.Control!;
        }
        flow.Add(previous, Navigation.Right, flow.CurrentControl);

        IVisual finalVisual = AddHint(panel!, "Accent color", out TextVisual? hint);

        return new(finalVisual, flow);
    }

    private static int GetFrequency(int value, int maxValue)
    {
        double rangeLog = Math.Log(maxValue + 1);
        double valueLog = Math.Log(value + 1);

        double normalized = valueLog / rangeLog;

        return (int)(normalized * (Profile.BeepRange.To - Profile.BeepRange.From) + Profile.BeepRange.From);
    }

    private static void Beep(Slider control)
    {
        if (!OperatingSystem.IsWindows())
            return;
        int freq = GetFrequency(control.Value, control.Range);
        Console.Beep(freq, 50);
    }

    private Element GetSliderElement(Profile? oldProfile)
    {
        LayoutSettings defaultLayout = new() { HorizontalAlignment = VisualAlignment.Center, VerticalAlignment = VisualAlignment.Center };
        LayoutSettings layout = new() { HorizontalAlignment = VisualAlignment.Fill, VerticalAlignment = VisualAlignment.Center };
        SliderVisual sliderVisual = new(layout) { Range = sliderControl.Range, Value = sliderControl.Value, StrokeColor = ConsoleColor.White };

        IVisual finalVisual = AddHint(sliderVisual, "Beep frequency", out TextVisual hint);

        sliderControl.Trigger += (obj) =>
        {
            if (obj is not (int value, int step))
                return;

            Beep(sliderControl);

            sliderVisual.Value = value;
            selectedFrequency = GetFrequency(sliderControl.Value, sliderControl.Range);
        };

        selectedFrequency = GetFrequency(sliderControl.Value, sliderControl.Range);

        sliderControl.StateChanged += (state) =>
        {
            switch (state)
            {
                case ControlState.Selected:
                    sliderVisual.StrokeColor = ConsoleColor.Yellow;
                    sliderVisual.BackdropColor = null;
                    hint.StrokeColor = ConsoleColor.Yellow;
                    break;

                case ControlState.Engaged:
                    sliderVisual.StrokeColor = null;
                    sliderVisual.BackdropColor = ConsoleColor.Yellow;
                    hint.StrokeColor = ConsoleColor.Yellow;
                    Beep(sliderControl);
                    break;

                default:
                    sliderVisual.StrokeColor = ConsoleColor.White;
                    sliderVisual.BackdropColor = null;
                    hint.StrokeColor = ConsoleColor.White;
                    break;
            }
        };

        ControlFlow flow = new(sliderControl);
        return new(finalVisual, flow);
    }

    private static Element GetButton(string text, Action<object?> callback)
    {
        LayoutSettings defaultLayout = new() { HorizontalAlignment = VisualAlignment.Center, VerticalAlignment = VisualAlignment.Center };
        Box box = new(defaultLayout with { Padding = (1, 0) }, new TextVisual(defaultLayout, text)) { StrokeColor = ConsoleColor.White };

        Button control = new();
        control.Trigger += callback;

        return new(box, control, (state, theme) =>
        {
            switch (state)
            {
                case ControlState.Selected:
                    box.StrokeColor = ConsoleColor.Yellow;
                    break;
                default:
                    box.StrokeColor = ConsoleColor.White;
                    break;
            }
        });
    }

    public ProfileCustomizeScreen(Profile? oldProfile = null)
    {
        LayoutSettings defaultLayout = new() { HorizontalAlignment = VisualAlignment.Center, VerticalAlignment = VisualAlignment.Center };

        if (oldProfile is not null)
        {
            inputBoxControl = new(oldProfile.DisplayName);
            selectedColor = oldProfile.Color;
            sliderControl.Value = (int)((double)(oldProfile.BeepFrequency - Profile.BeepRange.From) / (Profile.BeepRange.To - Profile.BeepRange.From) * sliderControl.Range);
        }

        Element name = GetInputBoxElement(oldProfile);
        Element colorbuttons = GetColorButtons(oldProfile);
        Element slider = GetSliderElement(oldProfile);

        Visual title = new Box(defaultLayout with { Padding = (1, 0)}, new TextVisual(defaultLayout, "PROFILE SETUP")) { StrokeColor = ConsoleColor.Black, BackdropColor = ConsoleColor.White };

        Element cancelButton = GetButton("Cancel", (_) => Status = false);
        Element confirmButton = GetButton("  >>  ", (_) =>
        {
            if (Profile is not null)
                Status = true;
        });
        WrapPanel confirmationButtons = new(defaultLayout, [cancelButton.Visual, confirmButton.Visual]);
        ControlFlow confirmationFlow = new(cancelButton.Control, Navigation.Right, confirmButton.Control);

        StackPanel stackPanel = new(defaultLayout, [title, new Line(defaultLayout), new Line(defaultLayout), name.Visual, colorbuttons.Visual, slider.Visual, confirmationButtons]); // margins are broken so we spam lines

        ControlFlow nameFlow = (ControlFlow)name.Control!;
        ControlFlow buttonsFlow = (ControlFlow)colorbuttons.Control!;
        ControlFlow sliderFlow = (ControlFlow)slider.Control!;

        buttonsFlow.Link(nameFlow, Navigation.Up);
        sliderFlow.Link(buttonsFlow, Navigation.Up);
        sliderFlow.Link(confirmationFlow, Navigation.Down);

        Root = new(stackPanel, sliderFlow);
    }
}