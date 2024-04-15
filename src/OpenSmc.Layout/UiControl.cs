﻿using System.Collections.Immutable;
using Newtonsoft.Json;
using OpenSmc.Messaging;

namespace OpenSmc.Layout;

public interface IUiControl : IDisposable
{
    object Id { get; }
    //object Data { get; init; }
    IUiControl WithBuildAction(Func<IUiControl, IServiceProvider, IUiControl> buildFunction);
    bool IsClickable { get; }

    bool IsUpToDate(object other);
}



public interface IUiControl<out TControl> : IUiControl
    where TControl : IUiControl<TControl>
{
    TControl WithLabel(object label);
    TControl WithClickAction(object payload, Func<IUiActionContext, Task> onClick);
}


public abstract record UiControl(object Data) : IUiControl
{
    public object Id { get; init; }


    IUiControl IUiControl.WithBuildAction(Func<IUiControl, IServiceProvider, IUiControl> buildFunction) => WithBuild(buildFunction);

    void IDisposable.Dispose() => Dispose();
    protected abstract IUiControl WithBuild(Func<IUiControl, IServiceProvider, IUiControl> buildFunction);
    protected abstract void Dispose();
    public object Style { get; init; } //depends on control, we need to give proper style here!
    public object Skin { get; init; }

    public string Tooltip { get; init; }
    public bool IsReadonly { get; init; }//TODO add concept of registering conventions for properties to distinguish if it is editable!!! have some defaults, no setter=> iseditable to false, or some attribute to mark as not editable, or checking if it has setter, so on... or BProcess open

    //object instance to be bound to
    public object DataContext { get; init; }

    public object Label { get; init; }
    public abstract bool IsUpToDate(object other);

    public MessageAndAddress ClickMessage { get; init; }

    // ReSharper disable once IdentifierTypo
    public bool IsClickable => ClickAction != null;

    internal Func<IUiActionContext, Task> ClickAction { get; init; }

    public Task ClickAsync(IUiActionContext context) => ClickAction?.Invoke(context) ?? Task.CompletedTask;
}


public abstract record UiControl<TControl>(string ModuleName, string ApiVersion, object Data) : UiControl(Data), IUiControl<TControl>
    where TControl : UiControl<TControl>, IUiControl<TControl>
{


    protected TControl This => (TControl)this;

    public TControl WithId(object id) => This with { Id = id };
    public TControl WithLabel(object label)
    {
        return This with { Label = label };
    }

    public override bool IsUpToDate(object other)
        => Equals(other);


    public TControl WithStyle(Func<StyleBuilder, StyleBuilder> styleBuilder) => This with { Style = styleBuilder(new StyleBuilder()).Build() };
    public TControl WithSkin(string skin) => This with { Skin = skin };

    public TControl WithClickAction(object payload, Func<IUiActionContext, Task> onClick)
    {
        return This with
        {
            ClickAction = onClick,
        };
    }
    public TControl WithClickMessage(object message, object target)
    {
        return This with
        {
            ClickMessage = new(message, target),
        };
    }

    public TControl WithDisposeAction(Action<TControl> action)
    {
        return This with
        {
            DisposeActions = DisposeActions.Add(action)
        };
    }

    public TControl WithClickAction(Func<IUiActionContext, Task> onClick) => WithClickAction(null, onClick);
    public TControl WithClickAction(Action<IUiActionContext> onClick) => WithClickAction(null, c =>
    {
        onClick(c);
        return Task.CompletedTask;

    });


    protected override void Dispose()
    {
        foreach (var disposable in DisposeActions)
        {
            disposable(This);
        }
    }



    private ImmutableList<Action<TControl>> DisposeActions { get; init; } = ImmutableList<Action<TControl>>.Empty;



    public TControl WithBuildAction(Func<TControl, IServiceProvider, TControl> buildFunction) => This with { BuildFunctions = BuildFunctions.Add(buildFunction) };
    [JsonIgnore]
    public ImmutableList<Func<TControl, IServiceProvider, TControl>> BuildFunctions { get; init; } = ImmutableList<Func<TControl, IServiceProvider, TControl>>.Empty;

    protected override IUiControl WithBuild(Func<IUiControl, IServiceProvider, IUiControl> buildFunction)
    {
        return WithBuildAction((c, sp) => (TControl)buildFunction(c, sp));
    }

    IUiControl IUiControl.WithBuildAction(Func<IUiControl, IServiceProvider, IUiControl> buildFunction)
    {
        return WithBuildAction((c, sp) => (TControl)buildFunction(c, sp));
    }
}

internal interface IExpandableUiControl : IUiControl
{
    internal Func<IUiActionContext, Task<UiControl>> ExpandFunc { get; init; }
    public ViewRequest ExpandMessage { get; }

}

public interface IExpandableUiControl<out TControl> : IUiControl<TControl>
    where TControl : IExpandableUiControl<TControl>
{
    bool IsExpandable { get; }
    TControl WithExpandAction<TPayload>(TPayload payload, Func<object, Task<object>> expandFunction);
}

public abstract record ExpandableUiControl<TControl>(string ModuleName, string ApiVersion, object Data) : UiControl<TControl>(ModuleName, ApiVersion, Data), IExpandableUiControl
    where TControl : ExpandableUiControl<TControl>
{
    public const string Expand = nameof(Expand);
    public bool IsExpandable => ExpandFunc != null;
    public ViewRequest ExpandMessage { get; init; }
    public Action<object> CloseAction { get; init; }

    public TControl WithCloseAction(Action<object> closeAction)
        => (TControl)(this with { CloseAction = closeAction });

    Func<IUiActionContext, Task<UiControl>> IExpandableUiControl.ExpandFunc
    {
        get => ExpandFunc;
        init => ExpandFunc = value;
    }

    internal Func<IUiActionContext, Task<UiControl>> ExpandFunc { get; init; }
    public TControl WithExpand(object message, object target, object area) => This with { ExpandMessage = new(message, target, area) };

    public TControl WithExpand(object payload, Func<IUiActionContext, Task<UiControl>> expand)
    {
        return This with
        {
            ExpandFunc = expand,
        };
    }
    public TControl WithExpand(Func<IUiActionContext, Task<UiControl>> expand)
    {
        return WithExpand(null, expand);
    }


}

