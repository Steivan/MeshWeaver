﻿using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace OpenSmc.Layout.Markdown;

public class LayoutAreaMarkdownRenderer : HtmlObjectRenderer<LayoutAreaComponentInfo>
{
    protected override void Write(HtmlRenderer renderer, LayoutAreaComponentInfo obj)
    {
        renderer.EnsureLine();
        renderer.WriteLine($"<div id='{obj.DivId}' class='layout-area'></div>");
        renderer.EnsureLine();
    }
}
