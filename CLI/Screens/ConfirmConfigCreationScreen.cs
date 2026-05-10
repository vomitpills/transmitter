using CLI;
using CLI.Visuals;
using System.Net;

namespace CLI.Screens;

public class ConfirmConfigCreationScreen : Screen
{
    protected override Element Root { get; }

    public bool? Status
    {
        get;
        private set
        {
            field = value;
            OnDone();
        }
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

    private static Visual GetHighlightedText(params IEnumerable<string> strings)
    {
        LayoutSettings textLayout = new() { HorizontalAlignment = VisualAlignment.Nearest, VerticalAlignment = VisualAlignment.Center };

        List<Visual> text = [];
        bool doHighlight = false;
        foreach (var item in strings)
        {
            text.Add(new TextVisual(textLayout, item)
            {
                StrokeColor = doHighlight? ConsoleColor.Yellow : ConsoleColor.White,
            });
            doHighlight = !doHighlight;
        }
        return new WrapPanel(textLayout, text);
    }

    public ConfirmConfigCreationScreen(IPAddress serverAddress, string configFileLocation)
    {
        LayoutSettings defaultLayout = new() { HorizontalAlignment = VisualAlignment.Center, VerticalAlignment = VisualAlignment.Center };

        Visual lines = new StackPanel(defaultLayout,
        [
            GetHighlightedText("- App config will be written to ", $"'{configFileLocation}'", "."),
            GetHighlightedText("- You ", "will not", " be able to log in as the same user without this file.")
        ]);

        Box box = new(defaultLayout with { Padding = new(0, 0, 0, 1) }, lines, $"You are going to connect to {serverAddress}") { StrokeColor = ConsoleColor.White };

        Element cancelButton = GetButton("Cancel", (_) => Status = false);
        Element confirmButton = GetButton("  >>  ", (_) => Status = true);

        WrapPanel buttons = new(defaultLayout, [cancelButton.Visual, confirmButton.Visual]);
        StackPanel stackPanel = new(defaultLayout, [box, buttons]);

        ControlFlow flow = new(cancelButton.Control, Navigation.Right, confirmButton.Control);

        Root = new(stackPanel, flow);
    }
}
