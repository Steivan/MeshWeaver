﻿using System.Collections.Immutable;
using OpenSmc.Application.Styles;

namespace OpenSmc.Layout.Views;

public record NavMenuControl()
    : UiControl<NavMenuControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion, null)
{
    public ImmutableList<INavItem> Items { get; init; } =
        ImmutableList<INavItem>.Empty;

    public NavMenuControl WithGroup(NavGroup navGroup) =>
        this with
        {
            Items = Items.Add(navGroup)
        };

    public NavMenuControl WithNavLink(NavLink navLink) => 
        this with
    {
        Items = Items.Add(navLink)
    };

    public NavMenuControl WithGroup(string title) =>
        WithGroup(Controls.NavGroup.WithTitle(title));

    public NavMenuControl WithGroup(string area, string title, Icon icon) =>
        WithGroup(Controls.NavGroup.WithArea(area).WithTitle(title).WithIcon(icon));

    public NavMenuControl WithNavLink(string area) =>
        WithNavLink(Controls.NavLink(area));

    public NavMenuControl WithNavLink(string area, Icon icon) => 
        WithNavLink(Controls.NavLink(area).WithIcon(icon));

    public NavMenuControl WithNavLink(string area, string title, Icon icon) =>
        WithNavLink(Controls.NavLink(area).WithTitle(title).WithIcon(icon));
}

public interface INavItem
{
    string Title { get; init; }
    string Area { get; init; }
    Icon Icon { get; init; }
}

public abstract record NavItem<TItem> : INavItem where TItem : NavItem<TItem>
{
    public string Area { get; init; }

    public string Title { get; init; }

    public Icon Icon { get; init; }

    public TItem WithTitle(string title) => (TItem)(this with { Title = title });

    public TItem WithArea(string area) => (TItem)(this with { Area = area });

    public TItem WithIcon(Icon icon) => (TItem)(this with { Icon = icon });
}

public record NavLink : NavItem<NavLink>
{
    public NavLink(string area)
    {
        Area = area;
    }
}

public record NavGroup : NavItem<NavGroup>;
