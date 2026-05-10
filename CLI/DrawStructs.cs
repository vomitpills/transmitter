using CLI.Visuals;
using System.Diagnostics.CodeAnalysis;

namespace CLI;

public readonly struct Vector2(int x, int y) : IEquatable<Vector2>
{
    public readonly int X = x;
    public readonly int Y = y;

    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Vector2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);
    public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);

    public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);
    public static Vector2 operator -(Vector2 left, Vector2 right) => new(left.X - right.X, left.Y - right.Y);
    public static Vector2 operator *(Vector2 vector, int factor) => new(vector.X * factor, vector.Y * factor);
    public static Vector2 operator /(Vector2 vector, int factor) => new(vector.X / factor, vector.Y / factor);

    public static implicit operator (int X, int Y)(Vector2 vector) => (vector.X, vector.Y);
    public static implicit operator Vector2((int X, int Y) vector) => new(vector.X, vector.Y);
    public static implicit operator Vector2(int value) => new(value, value);

    public static Vector2 Max(Vector2 a, Vector2 b) => new(int.Max(a.X, b.X), int.Max(a.Y, b.Y));
    public static Vector2 Min(Vector2 a, Vector2 b) => new(int.Min(a.X, b.X), int.Min(a.Y, b.Y));
}

public readonly struct SquareNormals(int up, int right, int down, int left) : IEquatable<SquareNormals>
{
    public int VerticalSum => Up + Down;
    public int HorizontalSum => Left + Right;
    public Vector2 Delta => (HorizontalSum, VerticalSum);

    public readonly int Up = up;
    public readonly int Right = right;
    public readonly int Down = down;
    public readonly int Left = left;

    public void Deconstruct(out int up, out int right, out int down, out int left)
    {
        up = Up;
        right = Right;
        down = Down;
        left = Left;
    }

    public bool Equals(SquareNormals other) => Up == other.Up && Right == other.Right && Down == other.Down && Left == other.Left;
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SquareNormals other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Up, Right, Down, Left);
    public static bool operator ==(SquareNormals left, SquareNormals right) => left.Equals(right);
    public static bool operator !=(SquareNormals left, SquareNormals right) => !left.Equals(right);

    public static SquareNormals operator -(SquareNormals a, SquareNormals b) => new(a.Up - b.Up, a.Right - b.Right, a.Down - b.Down, a.Left - b.Left);
    public static SquareNormals operator +(SquareNormals a, SquareNormals b) => new(a.Up + b.Up, a.Right + b.Right, a.Down + b.Down, a.Left + b.Left);

    public static implicit operator (int Up, int Right, int Down, int Left)(SquareNormals squareNormals) => (squareNormals.Up, squareNormals.Right, squareNormals.Down, squareNormals.Left);
    public static implicit operator SquareNormals((int Up, int Right, int Down, int Left) squareNormals) => new(squareNormals.Up, squareNormals.Right, squareNormals.Down, squareNormals.Left);
    public static implicit operator SquareNormals(int value) => new(value, value, value, value);
    public static implicit operator SquareNormals((int Horizontal, int Vertical) factors) => new(factors.Vertical, factors.Horizontal, factors.Vertical, factors.Horizontal);
}

public class BoxStyle
{
    public char NWCorner { get; }
    public char NECorner { get; }
    public char SWCorner { get; }
    public char SECorner { get; }
    public char Horizontal { get; }
    public char Vertical { get; }

    private BoxStyle(char nWCorner, char nECorner, char sWCorner, char sECorner, char horizontal, char vertical)
    {
        NWCorner = nWCorner;
        NECorner = nECorner;
        SWCorner = sWCorner;
        SECorner = sECorner;
        Horizontal = horizontal;
        Vertical = vertical;
    }

    public static BoxStyle Light { get; } = new('┌', '┐', '└', '┘', '─', '│');
    public static BoxStyle Heavy { get; } = new('┏', '┓', '┗', '┛', '━', '┃');
    public static BoxStyle Double { get; } = new('╔', '╗', '╚', '╝', '═', '║');
}

public readonly struct LayoutSettings
{
    public VisualAlignment HorizontalAlignment { get; init; }
    public VisualAlignment VerticalAlignment { get; init; }
    public VisualAlignment HorizontalJustification { get; init; } // use for internal TextVisual and WrapPanel alignment
    public VisualAlignment VerticalJustification { get; init; }
    public SquareNormals Margin { get; init; }
    public SquareNormals Padding { get; init; }
}

public readonly struct Domain : IEquatable<Domain>
{
    public static Domain Zero { get; } = new Domain(0, 0);

    public Vector2 Position { get; init; }
    public Vector2 Size { get; init; }

    public Vector2 LeftUpperCorner => Position;
    public Vector2 RightUpperCorner => Position + (Size.X, 0);
    public Vector2 LeftBottomCorner => Position + (0, Size.Y);
    public Vector2 RightBottomCorner => Position + Size;

    public int UpY => Position.Y;
    public int RightX => Position.X + Size.X;
    public int DownY => Position.Y + Size.Y;
    public int LeftX => Position.X;

    public int Width => Size.X;
    public int Height => Size.Y;

    public int Area => Size.X * Size.Y;

    public Domain(Vector2 position, Vector2 size)
    {
        if (size.X < 0 || size.Y < 0)
            throw new ArgumentOutOfRangeException(nameof(size), $"{nameof(size)} cannot be negative");

        Position = position;
        Size = size;
    }

    public void Deconstruct(out Vector2 position, out Vector2 size)
    {
        position = Position;
        size = Size;
    }

    public readonly Domain Shrink(SquareNormals squareNormals)
    {
        Vector2 newPos = (Position.X + squareNormals.Left, Position.Y + squareNormals.Up);
        Vector2 newSize = (Size.X - squareNormals.Left - squareNormals.Right, Size.Y - squareNormals.Up - squareNormals.Down);
        return new(newPos, newSize);
    }

    public readonly Domain Expand(SquareNormals squareNormals)
    {
        Vector2 newPos = (Position.X - squareNormals.Left, Position.Y - squareNormals.Up);
        Vector2 newSize = (Size.X + squareNormals.Left + squareNormals.Right, Size.Y + squareNormals.Up + squareNormals.Down);
        return new(newPos, newSize);
    }

    //public readonly Domain Move(Vector2 delta) => this with { Position = Position + delta };

    public readonly Domain Fuse(Domain domain)
    {
        Vector2 min = Vector2.Min(LeftUpperCorner, domain.LeftUpperCorner);
        Vector2 max = Vector2.Max(RightBottomCorner, domain.RightBottomCorner);
        return new(min, max);
    }

    public static bool TryCreate(Vector2 position, Vector2 size, [NotNullWhen(true)] out Domain? result)
    {
        if (size.X < 0 || size.Y < 0)
        {
            result = null;
            return false;
        }

        result = new(position, size);
        return true;
    }

    public bool Equals(Domain other) => Position == other.Position && Size == other.Size;
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Domain other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Position, Size);
    public static bool operator ==(Domain left, Domain right) => left.Equals(right);
    public static bool operator !=(Domain left, Domain right) => !left.Equals(right);

    // this is mathematically inconsistent because a Domain can represent a rectangle, a line, or even a point, yet we yank everything with no area.
    // better make it so Domain can only represent an axis-aligned rectangle with integer points (with no negative components?).
    // or not. needs pondering.
    public readonly Domain Interscect(Domain domain)
    {
        if (Area == 0 || domain.Area == 0)
            return Zero;

        Vector2 position = Vector2.Max(LeftUpperCorner, domain.LeftUpperCorner);
        Vector2 size = Vector2.Min(RightBottomCorner, domain.RightBottomCorner) - position;

        return TryCreate(position, size, out Domain? result) ? (Domain)result : Zero;
    }

    public readonly IEnumerable<Domain> Exclude(Domain domain)
    {
        if (domain == this || Size == 0)
            yield break;

        if (domain.Size == 0)
        {
            yield return this;
            yield break;
        }

        (Vector2 DomainMin, Vector2 DomainMax)[] tests =
        [
            (LeftUpperCorner, (RightX, domain.UpY)),
            ((LeftX, domain.UpY), domain.LeftBottomCorner),
            (domain.RightUpperCorner, (RightX, domain.DownY)),
            ((LeftX, domain.DownY), RightBottomCorner)
        ];
        foreach (var (DomainMin, DomainMax) in tests)
        {
            if (!TryCreate(DomainMin, DomainMax - DomainMin, out Domain? testDomain))
                continue;
            Domain intersection = Interscect((Domain)testDomain);
            if (intersection.Area > 0)
                yield return intersection;
        }
    }
}