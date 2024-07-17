﻿using System.Reactive.Linq;
using Markdig;
using System.Reflection;
using OpenSmc.Data;
using OpenSmc.Layout.Composition;
using OpenSmc.Layout.DataGrid;

namespace OpenSmc.Layout.Domain;

public record DomainViewsBuilder
{
    public DomainViewsBuilder(LayoutDefinition Layout)
    {
        this.Layout = Layout;
        MainLayout = DefaultLayoutViewElement
            ;

    }

    private ViewElement DefaultLayoutViewElement(ViewElement view, NavMenuControl navMenu)
        => new ViewElementWithView(view.Area, DefaultLayoutControl(view, navMenu));

    public const string Type = nameof(Type);



    private object DefaultLayoutControl(ViewElement view, NavMenuControl navMenu)
    {
        if (navMenu == null)
            return Controls.Body(view);
        return Controls.Stack()
            .WithOrientation(Orientation.Horizontal)
            .WithWidth("100%")
            .WithView(navMenu)
            .WithView(Controls.Body(view));
    }

    // ReSharper disable once WithExpressionModifiesAllMembers
    public DomainViewsBuilder WithCatalog(string area = nameof(Catalog)) => this with { Layout = Layout.WithView(area, Catalog) };

    public object Catalog(LayoutAreaHost area, RenderingContext ctx)
    {
        if (area.Stream.Reference.Id is not string collection)
            throw new InvalidOperationException("No type specified for catalog.");
        var typeSource = area.Workspace.DataContext.GetTypeSource(collection);
        if (typeSource == null)
            throw new DataSourceConfigurationException(
                $"Collection {collection} is not mapped in Address {Layout.Hub.Address}.");
        return
            Controls.Stack()
                .WithView(Controls.Title(typeSource.DisplayName, 1))
                .WithView(Controls.Html(typeSource.Description))
                .WithView((a, _) => a
                    .Workspace
                    .Stream
                    .Reduce(new CollectionReference(collection), area.Stream.Subscriber)
                    .Select(changeItem =>
                        area.ToDataGrid(
                            changeItem
                                .Value
                                .Instances
                                .Values,
                            typeSource.ElementType,
                            x => x.AutoMapColumns()
                        )
                    )
                )
            ;
    }
    public DomainViewsBuilder WithMarkdown(string area = nameof(Markdown)) => this with { Layout = Layout.WithView(area, Markdown) };

    public object Markdown(LayoutAreaHost area, RenderingContext ctx)
    {
        if (area.Stream.Reference.Id is not string fileName)
            throw new InvalidOperationException("No file name specified.");

        var type = area.Stream.Reference.Options.GetValueOrDefault(nameof(FileSource)) ?? FileSource.EmbeddedResource;
        // Assuming the embedded resource is in the same assembly as this class

        // TODO V10: this is not the correct assembly. need to parse out all the files. (17.07.2024, Roland Bürgi)
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{assembly.GetName().Name}.Markdown.{fileName}";

        var markdown = "";
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                // Resource not found, return a warning control/message instead
                markdown = $":error: **File not found**: {fileName}";
            }

            else
            {
                using var reader = new StreamReader(stream);
                markdown = reader.ReadToEnd();
            }
        }

        return new MarkdownControl(markdown);
    }

    public LayoutDefinition Build() => Layout with
    {
        MainLayout = MainLayout,
        NavMenu = MenuConfig == null ? null : reference => MenuConfig.Invoke(new(MenuArea, Layout, reference)).Build()
    };

    public DomainViewsBuilder WithMainLayout(Func<ViewElement, NavMenuControl, ViewElement> configuration)
        =>  this with
        {
            MainLayout = configuration
        };

    private Func<ViewElement, NavMenuControl, ViewElement> MainLayout { get; init; }


    public DomainViewsBuilder WithMenu(Func<DomainMenuBuilder, DomainMenuBuilder> menuConfig, string areaName = nameof(DomainViews.NavMenu))
        // ReSharper disable once WithExpressionModifiesAllMembers
        => this with { MenuConfig = menuConfig, MenuArea = areaName};

    private Func<DomainMenuBuilder, DomainMenuBuilder> MenuConfig { get; init; }

    private string MenuArea { get; init; }

    public LayoutDefinition Layout { get; init; }

}

public static class FileSource
{
    public const string EmbeddedResource = nameof(EmbeddedResource);
}
