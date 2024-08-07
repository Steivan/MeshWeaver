﻿using OpenSmc.Charting.Builders.ChartBuilders;
using OpenSmc.Pivot.Builder;
using OpenSmc.Pivot.Builder.Interfaces;

namespace OpenSmc.Charting.Pivot;

// todo: move to Charting.Layout project (07.08.2024, Alexander Kravets)
public static class PivotChartBuilderExtensions
{
    public static ChartControl ToBarChart<
            T,
            TTransformed,
            TIntermediate,
            TAggregate,
            TPivotBuilder
        >(
        this PivotBuilderBase<T, TTransformed, TIntermediate, TAggregate, TPivotBuilder> pivotBuilder,
        Func<BarChartBuilder, BarChartBuilder> chartBuilder = null
        )
        where TPivotBuilder : PivotBuilderBase<
            T,
            TTransformed,
            TIntermediate,
            TAggregate,
            TPivotBuilder
        >
    {
        var chartModel = pivotBuilder.ToBarChartPivotBuilder(chartBuilder).Execute();
        return new(chartModel);
    }
}
