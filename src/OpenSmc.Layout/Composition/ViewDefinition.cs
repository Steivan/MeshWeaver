﻿using AngleSharp.Media.Dom;

namespace OpenSmc.Layout.Composition;

public delegate Task<object> ViewDefinition(LayoutAreaHost area, RenderingContext context);

public record RenderingContext(string Area);

public delegate IObservable<object> ViewStream(LayoutAreaHost area, RenderingContext context);

public abstract record ViewElement(string Area);

public record ViewElementWithViewDefinition(string Area, IObservable<ViewDefinition> ViewDefinition)
    : ViewElement(Area);

public record ViewElementWithView(string Area, object View) : ViewElement(Area);

public record ViewElementWithViewStream(string Area, ViewStream Stream) : ViewElement(Area);
