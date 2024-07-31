﻿namespace OpenSmc.Layout;

public record ListboxControl(object Data) : ListControlBase<ListboxControl>(Data), IListControl;

public interface IListControl : IUiControl
{
    IReadOnlyCollection<Option> Options { get; init; }
}

public abstract record ListControlBase<TControl>(object Data)
    : UiControl<TControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion, Data)
    where TControl : ListControlBase<TControl>, IListControl
{
    public IReadOnlyCollection<Option> Options { get; init; }

    public TControl WithOptions(IReadOnlyCollection<Option> options) => (TControl) this with { Options = options };

    public TControl WithOptions<T>(IEnumerable<T> options) => 
        WithOptions(options.Select(o => new Option(o, o.ToString())).ToArray());

    public virtual bool Equals(ListControlBase<TControl> other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return base.Equals(other) && Options.SequenceEqual(other.Options);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Options.Aggregate(17, (x, y) => x ^y.GetHashCode()));
    }
}

public record Option(object Item, string Text);


public enum SelectPosition
{
    Above,
    Below
}
