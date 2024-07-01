﻿using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.DotNet.Interactive.Formatting;
using OpenSmc.Data.Serialization;
using OpenSmc.Layout;
using OpenSmc.Layout.Client;
using OpenSmc.Layout.Views;
using OpenSmc.Messaging;
using static OpenSmc.Layout.Client.LayoutClientConfiguration;

namespace OpenSmc.Blazor;

public static class BlazorClientExtensions
{
    public static MessageHubConfiguration AddBlazor(this MessageHubConfiguration config) =>
        config.AddBlazor(x => x);

    public static MessageHubConfiguration AddBlazor(
        this MessageHubConfiguration config,
        Func<LayoutClientConfiguration, LayoutClientConfiguration> configuration
    ) => config.AddLayoutClient(c => configuration.Invoke(c.WithView(DefaultFormatting)));



    #region Standard Formatting
    private static ViewDescriptor DefaultFormatting(
        object instance,
        ISynchronizationStream<JsonElement, LayoutAreaReference> stream,
        string area
    )
    {
        return instance switch
        {
            HtmlControl html => StandardView<HtmlControl, HtmlView>(html, stream, area),
            LayoutStackControl stack
                => stack.Skin switch
                {
                    ToolbarSkin _ => StandardView<LayoutStackControl, Toolbar>(stack, stream, area),
                    LayoutGridSkin _ => StandardView<LayoutStackControl, LayoutGrid>(stack, stream, area),
                    SplitterSkin _ => StandardView<LayoutStackControl, Splitter>(stack, stream, area),
                    _ => StandardView<LayoutStackControl, LayoutStack>(stack, stream, area)
                },
            MenuItemControl menu => StandardView<MenuItemControl, MenuItem>(menu, stream, area),
            NavLinkControl link => StandardView<NavLinkControl, NavLink>(link, stream, area),
            NavMenuControl navMenu
                => StandardView<NavMenuControl, NavMenuView>(navMenu, stream, area),
            NavGroupControl group
                => StandardView<NavGroupControl, NavGroup>(group, stream, area),
            LayoutAreaControl layoutArea
                => StandardView<LayoutAreaControl, LayoutArea>(layoutArea, stream, area),
            DataGridControl gc => StandardView<DataGridControl, DataGrid>(gc, stream, area),
            SelectControl select => StandardView<SelectControl, SelectView>(select, stream, area),
            SplitterPaneControl splitter => StandardView<SplitterPaneControl, SplitterPane>(splitter, stream, area),
            ButtonControl button => StandardView<ButtonControl, Button>(button, stream, area),
            LayoutGridItemControl gridItem => StandardView<LayoutGridItemControl, LayoutGridItem>(gridItem, stream, area),
            ItemTemplateControl itemTemplate => StandardView<ItemTemplateControl, ItemTemplate>(itemTemplate, stream, area),
            _ => DelegateToDotnetInteractive(instance, stream, area),
        };
    }

    private static ViewDescriptor DelegateToDotnetInteractive(
        object instance,
        ISynchronizationStream<JsonElement, LayoutAreaReference> stream,
        string area
    )
    {
        var mimeType = Formatter.GetPreferredMimeTypesFor(instance?.GetType()).FirstOrDefault();
        var output = Controls.Html(instance.ToDisplayString(mimeType));
        return new ViewDescriptor(
            typeof(HtmlView),
            ImmutableDictionary<string, object>
                .Empty.Add(ViewModel, output)
                .Add(nameof(Stream), stream)
                .Add(nameof(Area), area)
        );
    }
    #endregion
}
