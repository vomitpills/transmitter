using CLI;
using System.Collections.ObjectModel;

namespace CLI.Visuals;

public abstract class Container : Visual
{
    public virtual IVisual Child { get; init; }

    public override ConsoleColor? StrokeColor
    {
        get => base.StrokeColor;
        set
        {
            base.StrokeColor = value;
            Child.IsValid = false;
        }
    }

    public override ConsoleColor? BackdropColor
    {
        get => base.BackdropColor;
        set
        {
            base.BackdropColor = value;
            Child.IsValid = false;
        }
    }

    public Container(LayoutSettings layoutSettings, IVisual child) : base(layoutSettings)
    {
        Child = child;
        child.Invalidated += () =>
        {
            IsValid = false;
        };
    }
}

public abstract class MultiContainer : Visual
{
    public virtual ObservableCollection<IVisual> Children { get; init; }

    public override ConsoleColor? StrokeColor
    {
        get => base.StrokeColor;
        set
        {
            base.StrokeColor = value;
            foreach (var child in Children)
                child.IsValid = false;
        }
    }

    public override ConsoleColor? BackdropColor
    {
        get => base.BackdropColor;
        set
        {
            base.BackdropColor = value;
            foreach (var child in Children)
                child.IsValid = false;
        }
    }

    private void Invalidate() => IsValid = false;

    public MultiContainer(LayoutSettings layoutSettings, IEnumerable<IVisual> children) : base(layoutSettings)
    {
        Children = [.. children];
        foreach (var child in Children)
            child.Invalidated += Invalidate;

        Children.CollectionChanged += (sender, e) =>
        {
            if (e.NewItems is not null)
                foreach (IVisual child in e.NewItems)
                    child.Invalidated += Invalidate;

            if (e.OldItems is not null)
                foreach (IVisual child in e.OldItems)
                    child.Invalidated -= Invalidate;

            IsValid = false;
        };
    }
}