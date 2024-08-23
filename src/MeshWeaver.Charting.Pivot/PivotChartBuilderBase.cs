﻿using MeshWeaver.Charting.Builders.Chart;
using MeshWeaver.Charting.Enums;
using MeshWeaver.Charting.Models;
using MeshWeaver.Charting.Models.Options;
using MeshWeaver.Pivot.Builder;
using MeshWeaver.Pivot.Models;

namespace MeshWeaver.Charting.Pivot;

public abstract record PivotChartBuilderBase<
    T,
    TTransformed,
    TIntermediate,
    TAggregate,
    TPivotBuilder,
    TChart,
    TDataSet
>(PivotBuilderBase<T, TTransformed, TIntermediate, TAggregate, TPivotBuilder> pivotBuilder)
    : IPivotChartBuilder
    where TPivotBuilder : PivotBuilderBase<
            T,
            TTransformed,
            TIntermediate,
            TAggregate,
            TPivotBuilder
        >
    where TChart : Chart<
            TChart,
            TDataSet
        >
    where TDataSet : DataSet, new()
{
    private readonly PivotBuilderBase<
        T,
        TTransformed,
        TIntermediate,
        TAggregate,
        TPivotBuilder
    > pivotBuilder = pivotBuilder;
    protected TChart Chart;
    protected PivotChartModelBuilder PivotChartModelBuilder { get; init; } = new();

    public IPivotChartBuilder WithLegend(Func<Legend, Legend> legendModifier = null)
    {
        Chart = Chart.WithLegend(legendModifier);
        return this;
    }

    public IPivotChartBuilder WithTitle(string title, Func<Title, Title> titleModifier)
    {
        Chart = Chart.WithTitle(title, titleModifier);
        return this;
    }

    public IPivotChartBuilder WithSubTitle(string title, Func<Title, Title> titleModifier)
    {
        Chart = Chart.WithSubTitle(title, titleModifier);
        return this;
    }

    public IPivotChartBuilder WithColorScheme(string[] scheme)
    {
        Chart = Chart.WithColorPalette(scheme);
        return this;
    }

    public IPivotChartBuilder WithColorScheme(Palettes scheme)
    {
        Chart = Chart.WithColorPalette(scheme);
        return this;
    }

    public Chart Execute()
    {
        var pivotModel = pivotBuilder.Execute();
        var pivotChartModel = CreatePivotModel(pivotModel);
        AddDataSets(pivotChartModel);
        AddOptions(pivotChartModel);

        return Chart.ToChart();
    }

    public IPivotChartBuilder WithOptions(Func<PivotChartModel, PivotChartModel> postProcessor)
    {
        PivotChartModelBuilder.AddPostProcessor(postProcessor);
        return this;
    }

    public IPivotChartBuilder WithRows(params string[] lineRows)
    {
        PivotChartModelBuilder.AddPostProcessor(model =>
            model with
            {
                Rows = model
                    .Rows.Where(row =>
                        lineRows.Contains(row.Descriptor.Id)
                        || lineRows.Contains(row.Descriptor.DisplayName)
                    )
                    .ToList()
            }
        );
        return this;
    }

    protected abstract PivotChartModel CreatePivotModel(PivotModel pivotModel);

    protected abstract void AddDataSets(PivotChartModel pivotChartModel);

    protected abstract void AddOptions(PivotChartModel pivotChartModel);
}
