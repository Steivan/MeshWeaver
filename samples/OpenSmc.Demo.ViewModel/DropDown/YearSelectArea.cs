﻿using System.Reactive.Linq;
using OpenSmc.Layout;
using OpenSmc.Layout.Composition;

namespace OpenSmc.Demo.ViewModel.DropDown;

public static class YearSelectArea
{
    public static object YearSelect(LayoutAreaHost area, RenderingContext context)
        => Controls
            .Stack
            .WithVerticalGap(16)
            .WithView(
                (_, _) =>
                    Template.Bind(
                        new ChosenYear(2022),
                        nameof(ChosenYear),
                        sy => Controls.Select(sy.Year)
                            .WithOptions(new[]
                            {
                                new Option(2023, "2023"),
                                new Option(2022, "2022"),
                                new Option(2021, "2021"),
                            })
                    )
            )
            .WithView(
                nameof(ShowSelectedYear),
                (a, _) => a
                    .GetDataStream<ChosenYear>(nameof(ChosenYear))
                    .Select(x => ShowSelectedYear(x.Year))
            )
        ;

    private static object ShowSelectedYear(int year) => Controls.Html($"Year selected: {year}");
}

internal record ChosenYear(int Year);