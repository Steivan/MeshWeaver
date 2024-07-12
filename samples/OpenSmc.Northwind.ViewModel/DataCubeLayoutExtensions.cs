﻿using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Text.Json;
using Json.Pointer;
using OpenSmc.DataCubes;
using OpenSmc.Domain;
using OpenSmc.Layout;
using OpenSmc.Layout.Composition;
using static OpenSmc.Layout.Controls;

namespace OpenSmc.Northwind.ViewModel;

public record FilterItem(object Id, string Label, bool Selected);

public record DataCubeFilter
{
    public string SelectedDimension { get; init; }
    public ImmutableDictionary<string, ImmutableList<FilterItem>> FilterItems { get; init; } = ImmutableDictionary<string, ImmutableList<FilterItem>>.Empty;
    public string Search { get; init; }
}

public static class DataCubeLayoutExtensions
{
    /*
     * 1. context panel not rendered until the button is clicked
     * 2. user clicks filter button
     * 3. list of dimensions is rendered from dataCube slices (put an observable of data-cube to layoutAreaHost variables)
     * 4. preselect first dimension
     * 5. call getSlices on selected dimensions, put it to data under current dimension name
     * 6. data-bind list of checkboxes
     *
     * building filtered data cube
     * 1. get unfiltered data cube (observable)
     * 2. for each dimension try get observable from data
     *
     */

    public static UiControl ToDataCubeFilter(this IDataCube dataCube, LayoutAreaHost area, RenderingContext context, string filterId)
    {
        var filter = area.Stream.GetData<DataCubeFilter>(filterId) ?? new();
        var availableDimensions = dataCube.GetAvailableDimensions();

        filter = filter with
        {
            SelectedDimension = filter.SelectedDimension ?? availableDimensions.First().SystemName
        };
        
        // TODO V10: add overload that accepts lambda (09.07.2024, Alexander Kravets)
        var pointer = area.UpdateData(filterId, filter);

        area.AddDisposable(context.Area, 
            area.GetDataStream<DataCubeFilter>(filterId)
                .DistinctUntilChanged()
                .Subscribe(currentFilter =>
                {
                    if (!currentFilter.FilterItems.ContainsKey(currentFilter.SelectedDimension))
                    {
                        currentFilter = currentFilter with
                        {
                            FilterItems = currentFilter.FilterItems.Add(
                                currentFilter.SelectedDimension,
                                dataCube.GetSlices(currentFilter.SelectedDimension)
                                    .SelectMany(s => s.Tuple.Select(
                                        // todo get dimension pairs of type and ids from dataSlices
                                        // go to the workspace and take observable of this type
                                        x => new FilterItem(x.Value, x.Value.ToString(), true))
                                    )
                                    .ToImmutableList()
                            )
                        };
                    }
        
                    area.UpdateData(filterId, currentFilter);
                })
            );


        return area.Bind<DataCubeFilter, UiControl>(pointer.Pointer, f =>
            Stack()
                .WithView(Header("Filter"))
                .WithView(
                    Stack()
                        .WithClass("dimension-filter")
                        .WithOrientation(Orientation.Horizontal)
                        .WithHorizontalGap(16)
                        .WithView(Listbox(f.SelectedDimension)
                            .WithOptions(
                                availableDimensions
                                    .Select(d => new Option<string>(d.SystemName, d.DisplayName))
                                    .ToArray()
                            ))
                        .WithView(Stack()
                            .WithClass("dimension-values")
                            .WithView(
                                TextBox(f.Search)
                                    .WithSkin(TextBoxSkin.Search)
                                    .WithPlaceholder("Search...")
                                // TODO V10: this throws an "access violation" exception, figure out why (08.07.2024, Alexander Kravets)
                                // .WithImmediate(true)
                                // .WithImmediateDelay(200)
                            )
                            // .WithView((a, c) => ToDimensionValues(a, filterId))
                            .WithVerticalGap(16)
                        )
                )
        );
    }

    private static ImmutableList<Dimension> GetAvailableDimensions(this IDataCube dataCube)
    {
        return dataCube.GetDimensionDescriptors()
            .Select(d => new Dimension {SystemName = d.SystemName, DisplayName = d.Type.Name})
            .ToImmutableList();
    }

    private static IObservable<ItemTemplateControl> ToDimensionValues(LayoutAreaHost area, string filterId)
    {
        return area.GetDataStream<DataCubeFilter>(filterId)
            .Select(f => f.SelectedDimension)
            .DistinctUntilChanged()
        .Select(selectedDimension => 
                area.BindEnumerable<FilterItem, CheckBoxControl>(LayoutAreaReference.GetDataPointer(filterId) + $"/FilterItems/{selectedDimension}",
                f => CheckBox(f.Label, f.Selected)
            ));
    }
}
