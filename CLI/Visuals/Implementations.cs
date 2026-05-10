using CLI;

namespace CLI.Visuals;

using static ConsoleIO;
using static CLI.Visuals.Visual;

public abstract class OverheadContainer(LayoutSettings layoutSettings, IVisual child) : Container(layoutSettings, child)
{
    protected SquareNormals ChildDifference => LayoutSettings.Margin + Overhead + LayoutSettings.Padding;
    protected abstract SquareNormals Overhead { get; }

    public override Vector2 ProbeShrunkSize(Domain availableSpace)
    {
        Vector2 desiredChildSize = Child.ProbeDomain(availableSpace.Shrink(ChildDifference)).Size;
        return desiredChildSize + (ChildDifference.HorizontalSum, ChildDifference.VerticalSum);
    }

    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        using var _ = ApplyColors(parentStrokeColor, parentBackdropColor);
        Domain boundingBox = ProbeDomain(availableSpace);
        Domain childDomain = Child.ProbeDomain(boundingBox.Shrink(ChildDifference - LayoutSettings.Margin));
        if (!IsValid)
        {
            FillEmptySpace(boundingBox, childDomain);
            DrawHook(boundingBox);
        }
        Child.Draw(childDomain, StrokeColor, BackdropColor);
        IsValid = true;
    }

    protected virtual void FillEmptySpace(Domain boundingBox, Domain childDomain)
    {
        foreach (var emptyArea in boundingBox.Shrink(Overhead).Exclude(childDomain))
            DrawRectangle(emptyArea);
    }

    protected abstract void DrawHook(Domain boundingBox);
}

public class RigidPanel(LayoutSettings layoutSettings, IVisual childA, IVisual childB) : MultiContainer(layoutSettings, [childA, childB]), IOrientable
{
    public double Ratio { get; init; } = 0.5d;
    public bool IsVertical { get; init; }

    public override Vector2 ProbeShrunkSize(Domain availableSpace)
    {
        (Domain domainA, Domain domainB) = SliceDomain(availableSpace);
        return childA.ProbeDomain(domainA).Fuse(childB.ProbeDomain(domainB)).Size;
    }

    private (Domain domainA, Domain domainB) SliceDomain(Domain availableSpace)
    {
        if (IsVertical)
            throw new NotImplementedException();

        int width = availableSpace.Width;
        int xCut = (int)(width * Ratio);
        Domain a = availableSpace.Shrink(new(0, width - xCut, 0, 0));
        Domain b = availableSpace.Shrink(new(0, 0, 0, xCut));

        return (a, b);
    }

    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        (Domain domainA, Domain domainB) = SliceDomain(availableSpace);
        childA.Draw(domainA);
        childB.Draw(domainB);
        IsValid = true;
    }
}

public class StackPanel(LayoutSettings layoutSettings, IEnumerable<IVisual> children) : MultiContainer(layoutSettings, children), IOrientable
{
    public bool IsVertical { get; init; } = true;

    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        if (IsValid)
            return;

        Domain boundingBox = ProbeDomain(availableSpace);
        foreach (var child in Children)
        {
            Vector2 childSize = child.ProbeShrunkSize(boundingBox);
            Domain childDomain = boundingBox with { Size = (boundingBox.Width, childSize.Y) };
            child.Draw(childDomain, parentStrokeColor, parentBackdropColor);
            boundingBox = boundingBox.Shrink(new(childSize.Y, 0, 0, 0));
        }
    }

    public override Vector2 ProbeShrunkSize(Domain availableSpace)
    {
        if (!IsVertical)
            throw new NotImplementedException();

        int maxWidth = 0, totalHeight = 0;
        foreach (var child in Children)
        {
            Vector2 childSize = child.ProbeShrunkSize(availableSpace);
            maxWidth = int.Max(maxWidth, childSize.X);
            totalHeight += childSize.Y;
        }
        return (maxWidth, totalHeight);
    }
}

/* todo:
 *  space balancing for greedy elements
 *  justification of children
 *  padding
 *  inter- row & column spacing predicates
 */
public class WrapPanel(LayoutSettings layoutSettings, IEnumerable<IVisual> children) : MultiContainer(layoutSettings, children), IOrientable
{
    public bool IsVertical { get; init; }

    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        if (IsValid)
            return;

        if (IsVertical)
            throw new NotImplementedException();

        int heightOffset = 0;
        Domain boundingBox = ProbeDomain(availableSpace);
        foreach ((Range childrenSliceRange, Vector2 rowSize) in ProbeChildRows(boundingBox))
        {
            Domain rowDomain = new(boundingBox.Position + (0, heightOffset), (boundingBox.Width, rowSize.Y));

            var childrenSlice = Children.Skip(childrenSliceRange.Start.Value).Take(childrenSliceRange.End.Value).ToArray();
            int greedyCount = childrenSlice.Where(c => c.IsHorizontallyGreedy).Count();
            int greedyPadding = (int)((double)(boundingBox.Width - rowSize.X) / greedyCount);

            IEnumerator<IVisual> childrenSliseEnumerator = childrenSlice.AsEnumerable().GetEnumerator();
            while (childrenSliseEnumerator.MoveNext())
            {
                IVisual child = childrenSliseEnumerator.Current;
                Vector2 childSize = child.ProbeShrunkSize(rowDomain);
                if (child.IsHorizontallyGreedy)
                    childSize += (greedyPadding, 0);

                Domain childDomain = rowDomain with { Size = (childSize.X, rowSize.Y) };
                child.Draw(childDomain, parentStrokeColor, parentBackdropColor);
                rowDomain = rowDomain.Shrink(new(0, 0, 0, childSize.X));
            }
            heightOffset += rowSize.Y;
        }
        IsValid = true;
    }

    private IEnumerable<(Range ChildrenSlice, Vector2 RowSize)> ProbeChildRows(Domain availableSpace)
    {
        int childrenSpanStart = 0, childrenSpanLength = 0;
        Domain rowSpace = availableSpace;
        int rowHeight = 0;
        int consumedHeight = 0;
        foreach (var child in Children)
        {
            Vector2 childShrunkSize = child.ProbeShrunkSize(rowSpace);
            rowHeight = int.Max(rowHeight, childShrunkSize.Y);

            if (rowSpace.Width < childShrunkSize.X)
            {
                yield return (new(childrenSpanStart, childrenSpanLength), (availableSpace.Width - rowSpace.Width, rowHeight));

                childrenSpanStart = childrenSpanLength;
                childrenSpanLength = 0;
                rowSpace = availableSpace.Shrink(new(consumedHeight += rowHeight, 0, 0, 0));
                rowHeight = 0;
            }

            rowSpace = rowSpace.Shrink(new(0, 0, 0, childShrunkSize.X));
            childrenSpanLength++;
        }

        if (childrenSpanLength > 0)
            yield return (new(childrenSpanStart, childrenSpanLength), (availableSpace.Width - rowSpace.Width, rowHeight));
    }

    public override Vector2 ProbeShrunkSize(Domain availableSpace)
    {
        int maxWidth = 0, totalHeight = 0;
        foreach ((_, Vector2 rowSize) in ProbeChildRows(availableSpace))
        {
            maxWidth = int.Max(maxWidth, rowSize.X);
            totalHeight += rowSize.Y;
        }
        return (maxWidth, totalHeight);
    }
}

public class Box(LayoutSettings layoutSettings, IVisual child) : OverheadContainer(layoutSettings, child)
{
    protected override SquareNormals Overhead => new(1 + (title is null ? 0 : 2), 1, 1, 1);

    private readonly string? title;

    public Box(LayoutSettings layoutSettings, IVisual child, string title) : this(layoutSettings, child)
    {
        this.title = title;
    }

    protected override void DrawHook(Domain boundingBox)
    {
        int l = 0;
        CursorPosition = boundingBox.Position;
        DrawLine(boundingBox, ref l, "┌", '─', '┐');

        if (title is not null)
        {
            DrawLine(boundingBox, ref l, "│" + title, ' ', '│');
            DrawLine(boundingBox, ref l, "├", '─', '┤');
        }

        for (int i = 0; i < boundingBox.Shrink(Overhead).Size.Y; i++)
            DrawLineTerminals(boundingBox, ref l, '│');

        DrawLine(boundingBox, ref l, "└", '─', '┘');
    }

    private static void MoveCursor(Vector2 anchor, ref int lineNumber) => CursorPosition = anchor + (0, lineNumber++);

    private static void DrawLine(Domain boundingBox, ref int lineNumber, string starter, char filler, char terminator)
    {
        MoveCursor(boundingBox.Position, ref lineNumber);
        Write(starter + new string(filler, boundingBox.Size.X - 1 - starter.Length) + terminator);

    }

    private static void DrawLineTerminals(Domain boundingBox, ref int lineNumber, char terminal)
    {
        MoveCursor(boundingBox.Position, ref lineNumber);
        Write(terminal);
        CursorLeft += boundingBox.Size.X - 2;
        Write(terminal);
    }
}

public class Line(LayoutSettings layoutSettings) : Visual(layoutSettings)
{
    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        if (IsValid) // make Draw() virtual with this pattern baked in, use injection instead of plain overriding (provide boundingBox automatically too?)
            return;

        Domain boundingBox = ProbeDomain(availableSpace);
        using var _ = ApplyColors(parentStrokeColor, parentBackdropColor);

        CursorPosition = boundingBox.Position;
        Write(new string('^', boundingBox.Width));

        IsValid = true;
    }

    public override Vector2 ProbeShrunkSize(Domain availableSpace) => 1;
}

public class Rectangle(LayoutSettings layoutSettings, Vector2 size) : Visual(layoutSettings)
{
    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        if (IsValid)
            return;

        using var _ = ApplyColors(parentStrokeColor, parentBackdropColor);
        DrawRectangle(availableSpace);
        IsValid = true;
    }

    public override Vector2 ProbeShrunkSize(Domain availableSpace) => size;
}

public class TextVisual(LayoutSettings layoutSettings, string? text = null) : Visual(layoutSettings)
{
    public string Text
    {
        get;
        set
        {
            field = value;
            IsValid = false;
        }
    } = text ?? string.Empty;

    private IEnumerable<string> GetLines(Domain availableSpace)
    {
        if (availableSpace.Size is (0, _) or (_, 0))
            return [];
        return Text.ReplaceLineEndings().Split(Environment.NewLine).SelectMany(l => l.Chunk(availableSpace.Size.X)).Select(c => new string(c));
    }

    public override Vector2 ProbeShrunkSize(Domain availableSpace)
    {
        int lineCount = 0, maxLineLength = 0;
        foreach (var line in GetLines(availableSpace))
        {
            maxLineLength = int.Max(maxLineLength, line.Length);
            lineCount++;
        }
        return (maxLineLength, lineCount);
    }

    private LayoutSettings GetOverridenLayoutSettings()
    {
        static VisualAlignment OverrideAlignment(VisualAlignment alignment) => alignment switch
        {
            VisualAlignment.Fill => VisualAlignment.Center,
            _ => alignment
        };
        return LayoutSettings with
        {
            HorizontalAlignment = OverrideAlignment(LayoutSettings.HorizontalAlignment),
            VerticalAlignment = OverrideAlignment(LayoutSettings.VerticalAlignment)
        };
    }

    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        if (IsValid)
            return;

        using var _ = ApplyColors(parentStrokeColor, parentBackdropColor);

        Domain boundingBox = ProbeDomain(availableSpace);
        Domain shrunkBoundingBox = GetBoundingBox(availableSpace, ProbeShrunkSize(availableSpace), GetOverridenLayoutSettings());

        int before = shrunkBoundingBox.UpY - boundingBox.UpY;
        int after = boundingBox.DownY - shrunkBoundingBox.DownY;
        DrawRectangle(new(boundingBox.Position, (boundingBox.Size.X, before)));

        string[] lines = [.. GetLines(availableSpace)];
        foreach (var (index, line) in GetLines(boundingBox).Index())
            DrawLine(boundingBox, line, index);

        DrawRectangle(new(boundingBox.Position + (0, after + shrunkBoundingBox.Size.Y), (boundingBox.Size.X, after)));
        IsValid = true;
    }

    private void DrawLine(Domain boundingBox, string line, int index)
    {
        Domain lineBoundingBoxAbsolute = GetBoundingBox(boundingBox, (line.Length, 1), GetOverridenLayoutSettings());
        CursorPosition = boundingBox.Position + (0, index);
        Write(Pad(lineBoundingBoxAbsolute.LeftX - boundingBox.LeftX));
        Write(line);
        Write(Pad(boundingBox.RightX - lineBoundingBoxAbsolute.RightX));
    }
}

public class SliderVisual(LayoutSettings layoutSettings) : Visual(layoutSettings), IOrientable
{
    public bool IsVertical { get; init; }

    private static readonly Vector2 SLIDER_HEAD_DIMENSIONS = (3, 4);
    private static readonly int ERROR = 5; // ew

    public int Value
    {
        get;
        set
        {
            field = int.Clamp(value, 0, Range);
            IsValid = false;
        }
    }

    public int Step // add visual ticks that update when you hold or release ctrl
    {
        get;
        set
        {
            field = int.Max(1, value);
            IsValid = false;
        }
    } = 1;

    required public int Range { get; init; }

    public override Vector2 ProbeShrunkSize(Domain availableSpace) => (Range + SLIDER_HEAD_DIMENSIONS.X + ERROR, SLIDER_HEAD_DIMENSIONS.Y);

    public override void Draw(Domain availableSpace, ConsoleColor? parentStrokeColor = null, ConsoleColor? parentBackdropColor = null)
    {
        if (IsValid)
            return;

        Domain boundingBox = ProbeDomain(availableSpace);

        int headOffset = (int)double.Round(boundingBox.Width * ((double)Value / Range));
        Vector2 headPosition = boundingBox.Position + (headOffset, boundingBox.Height / 2 - SLIDER_HEAD_DIMENSIONS.Y / 2);
        headPosition = (int.Min(headPosition.X, availableSpace.RightX - SLIDER_HEAD_DIMENSIONS.X), headPosition.Y);

        using (ApplyColors(parentStrokeColor, parentBackdropColor, true))
            DrawHead(headPosition);
        using (ApplyColors(parentStrokeColor, parentBackdropColor))
            DrawLine(availableSpace, boundingBox, headPosition);
        IsValid = true;

    }

    private static void DrawLine(Domain availableSpace, Domain boundingBox, Vector2 headPosition)
    {
        Domain lineBoundingBox = new Domain(boundingBox.Position + (0, boundingBox.Height / 2), (boundingBox.Width, 2)).Expand((0, SLIDER_HEAD_DIMENSIONS.X, 0, 0)).Interscect(availableSpace);

        headPosition -= boundingBox.Position;

        Vector2 firstSegmentPosition = boundingBox.Position;
        Vector2 firstSegmentSize = headPosition;
        Domain firstSegment = new(firstSegmentPosition, firstSegmentSize);

        if (firstSegment.Width > 0)
        {
            int l = 0;

            DrawEmptySpace(boundingBox, firstSegment, ref l);

            NextLine(firstSegment.Position, ref l);
            Write('┌' + new string('─', firstSegment.Width - 1));

            NextLine(firstSegment.Position, ref l);
            Write('└' + new string('─', firstSegment.Width - 1));

            DrawEmptySpace(boundingBox, firstSegment, ref l);
        }

        Vector2 secondSegmentPosition = boundingBox.Position + (firstSegment.Width + SLIDER_HEAD_DIMENSIONS.X, 0);
        Vector2 secondSegmentSize = (lineBoundingBox.Width - SLIDER_HEAD_DIMENSIONS.X - firstSegmentSize.X, headPosition.Y);
        if (Domain.TryCreate(secondSegmentPosition, secondSegmentSize, out Domain? result) && result.Value.Width > 0)
        {
            Domain secondSegment = result.Value;
            int l = 0;

            DrawEmptySpace(boundingBox, secondSegment, ref l);

            NextLine(secondSegment.Position, ref l);
            Write(new string('─', secondSegment.Width - 1) + '┐');
            NextLine(secondSegment.Position, ref l);
            Write(new string('─', secondSegment.Width - 1) + '┘');

            DrawEmptySpace(boundingBox, secondSegment, ref l);
        }
    }

    private static void DrawEmptySpace(Domain boundingBox, Domain segment, ref int l)
    {
        using (new ConsoleStateContext())
        {
            Console.BackgroundColor = ConsoleColor.Black; // cancer
            for (int i = 0; i < (boundingBox.Height - 2) / 2; i++)
            {
                NextLine(segment.Position, ref l);
                Write(new string(' ', segment.Width));
            }
        }
    }

    private void DrawHead(Vector2 headPosition)
    {
        bool hideLeftConnections = Value == 0;
        bool hideRightConnections = Value == Range;

        int lineWidth = SLIDER_HEAD_DIMENSIONS.X - 2;

        int l = 0;
        NextLine(headPosition, ref l);
        Write('┌' + new string('─', lineWidth) + '┐');

        DrawHeadLines(headPosition, lineWidth, ref l);
        
        NextLine(headPosition, ref l);
        Write((hideLeftConnections? '│' : '┘') + new string(' ', lineWidth) + (hideRightConnections? '│' : '└'));
        NextLine(headPosition, ref l);
        Write((hideLeftConnections ? '│' : '┐') + new string(' ', lineWidth) + (hideRightConnections ? '│' : '┌'));

        DrawHeadLines(headPosition, lineWidth, ref l);

        NextLine(headPosition, ref l);
        Write('└' + new string('─', lineWidth) + '┘');
    }

    private static void DrawHeadLines(Vector2 headPosition, int lineWidth, ref int l)
    {
        for (int i = 0; i < (SLIDER_HEAD_DIMENSIONS.Y - 4) / 2; i++)
        {
            NextLine(headPosition, ref l);
            Write('│' + new string(' ', lineWidth) + '│');
        }
    }

    private static void NextLine(Vector2 headPosition, ref int l) => CursorPosition = headPosition + (0, l++);
}