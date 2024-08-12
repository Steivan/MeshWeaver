﻿namespace MeshWeaver.Layout;

public record NavMenuControl() : 
    ContainerControl<NavMenuControl, UiControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion, null)
{

    public NavMenuControl WithEntry(UiControl item, Func<NamedAreaControl, NamedAreaControl> options = null) => 
        WithItem(item, options ?? (x => x)) ;

    public bool Collapsible { get; init; } = true;

    public int? Width { get; init; } = 250;


    public NavMenuControl WithNavLink(object title, object href) =>
        WithNavLink(title, href, x => x);
    public NavMenuControl WithNavLink(NavLinkControl control) =>
        WithEntry(control);

    public NavMenuControl WithNavLink(object title, object href, Func<NavLinkControl, NavLinkControl> options) =>
        WithNavLink(options.Invoke(new(title, href)));
    public NavMenuControl WithNavLink(object title, object href, object icon) =>
        WithNavLink(new NavLinkControl(title, href){Icon = icon})
        ;

    public NavMenuControl WithNavGroup(NavGroupControl navGroup) =>
        WithEntry(navGroup);
    public NavMenuControl WithNavGroup(object title, Func<NavGroupControl, NavGroupControl> config) =>
        WithEntry(config.Invoke(new(title)));
    public NavMenuControl WithCollapsible(bool collapsible) => this with { Collapsible = collapsible };

    public NavMenuControl WithWidth(int width) => this with { Width = width };

}



public abstract record NavItemControl<TNavItemControl>(object Data) : ContainerControl<TNavItemControl, UiControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion, Data)
where TNavItemControl : NavItemControl<TNavItemControl>
{
    public object Icon { get; init; }
    public object Href { get; init; }
    public object Title { get; init; }
    public TNavItemControl WithTitle(object title) => This with { Title = title };
    public TNavItemControl WithHref(object href) => This with { Href = href };

    public TNavItemControl WithIcon(object icon) => This with { Icon = icon };


}

public record NavLinkControl : NavItemControl<NavLinkControl>
{
    public NavLinkControl(object Data, object Href) : base(Data)
    {
        this.Href = Href;
    }

}

public record NavGroupControl(object Data)
    : NavItemControl<NavGroupControl>(Data)
{

    public NavGroupControl WithLink(string displayName, string link, Func<NavLinkControl, NavLinkControl> options) =>
        WithItem(options.Invoke(new(displayName, link)));
    public NavGroupControl WithGroup(NavGroupControl @group) => WithItem(group);


}
