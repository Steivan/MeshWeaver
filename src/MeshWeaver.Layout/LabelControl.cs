﻿namespace MeshWeaver.Layout;

public record LabelControl(object Data)
    : UiControl<LabelControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion)
{
    public object Alignment { get; init; }
    public object Color { get; init; }
    public object Disabled { get; init; }
    public object Typo { get; init; }
    public object Weight { get; init; }

    public LabelControl WithAlignment(object alignment) => this with {Alignment = alignment};
    public LabelControl WithColor(object color) => this with {Color = color};
    public LabelControl WithDisabled(object disabled) => this with {Disabled = disabled};
    public LabelControl WithTypo(object typo) => this with {Typo = typo};
    public LabelControl WithWeight(object weight) => this with {Weight = weight};
}

public enum Typography
{
    Body,
    Subject,
    Header,
    PaneHeader,
    EmailHeader,
    PageTitle,
    HeroTitle,
    H1,
    H2,
    H3,
    H4,
    H5,
    H6
}

public enum FontWeight
{
    Normal,
    Bold,
    Bolder
}
