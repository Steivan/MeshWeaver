﻿using System.Collections.Immutable;
using System.Reactive.Linq;
using MeshWeaver.Data;
using MeshWeaver.Layout.Composition;

namespace MeshWeaver.Layout;

public record LayoutStackControl(): UiControl<LayoutStackControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion, null)
{
    internal const string Root = "";
    private ImmutableList<Renderer> Renderers { get; init; } = ImmutableList<Renderer>.Empty;
    private string GetAutoName() => $"{Renderers.Count + 1}";
    public LayoutStackControl WithView(object value) => WithView(GetAutoName(), value);

    public LayoutStackControl WithView(string area, object view) =>
        this with
        {
            RawAreas = RawAreas.Add(area),
            Renderers = Renderers.Add((host,context) => host.RenderArea(GetContextForArea(context, area), view))
        };


    public override IEnumerable<Func<EntityStore, EntityStore>> Render(LayoutAreaHost host, RenderingContext context) =>
        base.Render(host, context)
            .Concat(Renderers.SelectMany(r => (r.Invoke(host, context))));
    protected override Func<EntityStore, EntityStore> RenderSelf(LayoutAreaHost host, RenderingContext context)
        => store => store.UpdateControl(context.Area, this with
            { Areas = RawAreas.Select(i => $"{context.Area}/{i}").ToArray() });

    private static RenderingContext GetContextForArea(RenderingContext context, string area)
    {
        return context with{Area = $"{context.Area}/{area}", Parent=context};
    }

    public LayoutStackControl WithView<T>(ViewDefinition<T> viewDefinition) =>
        WithView(GetAutoName(), Observable.Return(viewDefinition));


    public LayoutStackControl WithView<T>(string area, IObservable<ViewDefinition<T>> viewDefinition) =>
        this with
        {
            RawAreas = RawAreas.Add(area),
            Renderers = Renderers.Add((host,context) => host.RenderArea(GetContextForArea(context, area), viewDefinition))
        };

    public LayoutStackControl WithView(string area, IObservable<ViewDefinition> viewDefinition) =>
        this with
        {
            RawAreas = RawAreas.Add(area),
            Renderers = Renderers.Add((host, context) => host.RenderArea(GetContextForArea(context, area), viewDefinition))
        };
    public LayoutStackControl WithView(IObservable<object> viewDefinition) =>
        WithView(GetAutoName(), viewDefinition);
    public LayoutStackControl WithView(string area, IObservable<object> viewDefinition) =>
        this with
        {
            RawAreas = RawAreas.Add(area),
            Renderers = Renderers.Add((host, context) => host.RenderArea(GetContextForArea(context, area), viewDefinition))
        };

    public LayoutStackControl WithView(IObservable<ViewDefinition> viewDefinition)
        => WithView(GetAutoName(), viewDefinition);

    public LayoutStackControl WithView<T>(ViewStream<T> viewDefinition)
        => WithView(GetAutoName(), viewDefinition);
    public LayoutStackControl WithView<T>(string area, ViewStream<T> viewDefinition)
        => this with
        {
            RawAreas = RawAreas.Add(area),
            Renderers = Renderers.Add((host, context) => host.RenderArea(GetContextForArea(context, area),viewDefinition.Invoke))
        };


    public LayoutStackControl WithView(string area, Func<LayoutAreaHost, RenderingContext, object> viewDefinition)
        => WithView(area, (la, ctx) => Observable.Return(viewDefinition.Invoke(la, ctx)));
    public LayoutStackControl WithView(Func<LayoutAreaHost, RenderingContext, object> viewDefinition)
        => WithView(GetAutoName(), viewDefinition);


    public HorizontalAlignment HorizontalAlignment { get; init; }

    public VerticalAlignment VerticalAlignment { get; init; }
    public int? HorizontalGap { get; init; }
    public int? VerticalGap { get; init; }
    public Orientation? Orientation { get; init; }
    public bool Wrap { get; init; }
    public string Width { get; init; }
    public string Height { get; init; }


    public LayoutStackControl WithHorizontalAlignment(HorizontalAlignment horizontalAlignment)
        => this with { HorizontalAlignment = horizontalAlignment };
    public LayoutStackControl WithVerticalAlignment(VerticalAlignment verticalAlignment)
    => this with { VerticalAlignment = verticalAlignment };
    public LayoutStackControl WithHorizontalGap(int? horizontalGap)
        => this with { HorizontalGap = horizontalGap };
    public LayoutStackControl WithVerticalGap(int? verticalGap)
        => this with { VerticalGap = verticalGap };
    public LayoutStackControl WithOrientation(Orientation orientation)
    => this with { Orientation = orientation };
    public LayoutStackControl WithWrap(bool wrap)
        => this with { Wrap = wrap };
    public LayoutStackControl WithWidth(string width)
    => this with { Width = width };
    public LayoutStackControl WithHeight(string height) => this with { Height = height };

    public IReadOnlyCollection<string> Areas { get; init; } = [];
    private ImmutableList<string> RawAreas { get; init; } = ImmutableList<string>.Empty;

    public virtual bool Equals(LayoutStackControl other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other))
            return true;
        return base.Equals(other) &&
               HorizontalAlignment == other.HorizontalAlignment &&
               VerticalAlignment == other.VerticalAlignment &&
               HorizontalGap == other.HorizontalGap &&
               VerticalGap == other.VerticalGap &&
               Orientation == other.Orientation &&
               Wrap == other.Wrap &&
               Width == other.Width &&
               Height == other.Height &&
               Areas.SequenceEqual(other.Areas) &&
               RawAreas.SequenceEqual(other.RawAreas);

    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            base.GetHashCode(),
            HashCode.Combine(
                Renderers.Aggregate(0, (acc, renderer) => acc ^ renderer.GetHashCode()),
                HorizontalAlignment.GetHashCode(),
                VerticalAlignment.GetHashCode(),
                HorizontalGap?.GetHashCode(),
                VerticalGap?.GetHashCode(),
                Orientation?.GetHashCode(),
                Wrap.GetHashCode()
            ),
            HashCode.Combine(
                Areas.Aggregate(0, (acc, area) => acc ^ area.GetHashCode()),
                RawAreas.Aggregate(0, (acc, rawArea) => acc ^ rawArea.GetHashCode()),
                Width?.GetHashCode() ?? 0,
                Height?.GetHashCode() ?? 0
            )
        );
    }
}

public static class StackSkins
{
    public static LayoutSkin Layout => new();
    public static LayoutGridSkin LayoutGrid => new();
    public static SplitterSkin Splitter => new();
}