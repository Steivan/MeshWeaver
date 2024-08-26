﻿using System.Collections.Immutable;
using MeshWeaver.Charting.Builders.DataSetBuilders;
using MeshWeaver.Charting.Builders.OptionsBuilders;
using MeshWeaver.Charting.Enums;
using MeshWeaver.Charting.Helpers;
using MeshWeaver.Charting.Models;
using MeshWeaver.Charting.Models.Bar;
using MeshWeaver.Charting.Models.Options;
using MeshWeaver.Charting.Models.Options.Scales;
using MeshWeaver.Charting.Models.Options.Tooltips;
using MeshWeaver.Utils;

namespace MeshWeaver.Charting.Builders.Chart;

public record FloatingBarChart
    : RangeChart<FloatingBarChart, FloatingBarDataSet>
{
    public FloatingBarChart(IReadOnlyCollection<FloatingBarDataSet> dataSets) : base(dataSets, ChartType.Bar) { }
}

public record HorizontalFloatingBarChart
    : RangeChart<HorizontalFloatingBarChart, HorizontalFloatingBarDataSet>
{
    public HorizontalFloatingBarChart(IReadOnlyCollection<HorizontalFloatingBarDataSet> dataSets)
        : base(dataSets, ChartType.Bar)
    {
        Options = Options.WithIndexAxis("y");
    }
}

public static class WaterfallChartExtensions
{
    public static BarChart ToWaterfallChart(this BarChart chart, List<double> deltas, Func<WaterfallStylingBuilder, WaterfallStylingBuilder> stylingOptions = null)
        => chart
            .ToWaterfallChart<FloatingBarDataSet, FloatingBarDataSetBuilder>(deltas, stylingOptions)
            .WithOptions(o => o
                .Stacked("x")
                .HideAxis("y")
                .HideGrid("x")
            );

    public static BarChart ToHorizontalWaterfallChart(this BarChart chart, List<double> deltas, Func<WaterfallStylingBuilder, WaterfallStylingBuilder> stylingOptions = null)
        => chart
            .ToWaterfallChart<HorizontalFloatingBarDataSet, HorizontalFloatingBarDataSetBuilder>(deltas, stylingOptions)
            .WithOptions(o => o
                .Stacked("y")
                //.HideAxis("x")
                .Grace<CartesianLinearScale>("x", "10%")
                // TODO V10: understand why the line below helps and find less random approach (2023/10/08, Ekaterina Mishina)
                .SuggestedMax("x", 10) // this helps in case of all negative values
                .ShortenAxisNumbers("x")
                .WithIndexAxis("y")
            );

    internal record WaterfallChartDataModel(List<double> deltas)
    {
        internal ImmutableList<(double[] range, string label, double? delta)> IncrementRanges { get; init; } = [];
        internal ImmutableList<(double[] range, string label, double? delta)> DecrementRanges { get; init; } = [];
        internal ImmutableList<(double[] range, string label, double? delta)> TotalRanges { get; init; } = [];
        internal ImmutableList<double?> FirstDottedValues { get; init; } = [];
        internal ImmutableList<double?> SecondDottedValues { get; init; } = [];
        internal ImmutableList<double?> ThirdDottedValues { get; init; } = [];
    }

    internal static WaterfallChartDataModel CalculateModel(this List<double> deltas, string[] labels, HashSet<int> totalIndexes)
    {
        var incrementRanges = new List<(double[] range, string label, double? delta)>(deltas.Count);
        var decrementRanges = new List<(double[] range, string label, double? delta)>(deltas.Count);
        var totalRanges = new List<(double[] range, string label, double? delta)>(deltas.Count);

        var firstDottedValues = new List<double?>(deltas.Count);
        var secondDottedValues = new List<double?>(deltas.Count);
        var thirdDottedValues = new List<double?>(deltas.Count);

        if (labels?.Length != deltas.Count)
            throw new ArgumentException("Labels length does not match data");

        var total = 0.0;
        var resetTotal = true;

        for (var index = 0; index < deltas.Count; index++)
        {
            var delta = deltas[index];
            var prevTotal = total;
            if (resetTotal)
                total = 0.0;

            var isTotal = totalIndexes != null && totalIndexes.Contains(index);

            if (isTotal)
            {
                totalRanges.Add(delta >= 0
                                    ? (new[] { 0, delta }, labels[index], delta)
                                    : (new[] { delta, 0 }, labels[index], delta));
                incrementRanges.Add((null, labels[index], null));
                decrementRanges.Add((null, labels[index], null));
                total = delta;
            }
            else
            {
                totalRanges.Add((null, labels[index], null));
                if (delta >= 0)
                {
                    incrementRanges.Add((new[] { total, total + delta }, labels[index], delta));
                    decrementRanges.Add((null, labels[index], null));
                }
                else
                {
                    decrementRanges.Add((new[] { total + delta, total }, labels[index], delta));
                    incrementRanges.Add((null, labels[index], null));
                }

                total += delta;
            }

            var beforeReset = index == deltas.Count - 1 || isTotal;

            if (index == 0)
            {
                firstDottedValues.Add(total);
                secondDottedValues.Add(null);
                thirdDottedValues.Add(null);
            }
            else
            {
                switch (index % 3)
                {
                    case 0:
                        if (!beforeReset)
                            firstDottedValues.Add(total);
                        else
                            firstDottedValues.Add(null);
                        secondDottedValues.Add(null);
                        thirdDottedValues.Add(prevTotal);
                        break;
                    case 1:
                        firstDottedValues.Add(prevTotal);
                        if (!beforeReset)
                            secondDottedValues.Add(total);
                        else
                            secondDottedValues.Add(null);
                        thirdDottedValues.Add(null);
                        break;
                    case 2:
                        firstDottedValues.Add(null);
                        secondDottedValues.Add(prevTotal);
                        if (!beforeReset)
                            thirdDottedValues.Add(total);
                        else
                            thirdDottedValues.Add(null);
                        break;
                }
            }

            resetTotal = beforeReset;
        }

        return new(deltas)
        {
            IncrementRanges = incrementRanges.ToImmutableList(),
            DecrementRanges = decrementRanges.ToImmutableList(),
            TotalRanges = totalRanges.ToImmutableList(),
            FirstDottedValues = firstDottedValues.ToImmutableList(),
            SecondDottedValues = secondDottedValues.ToImmutableList(),
            ThirdDottedValues = thirdDottedValues.ToImmutableList(),
        };
    }

    private static BarChart ToWaterfallChart<TDataSet, TDataSetBuilder>(this BarChart chart, List<double> deltas, Func<WaterfallStylingBuilder, WaterfallStylingBuilder> stylingOptions = null)
        where TDataSet : BarDataSetBase, IDataSetWithStack, new()
        where TDataSetBuilder : FloatingBarDataSetBuilderBase<TDataSetBuilder, TDataSet>, new()
    {
        var stylingBuilder = new WaterfallStylingBuilder();
        stylingBuilder = stylingOptions?.Invoke(stylingBuilder) ?? stylingBuilder;
        var styling = stylingBuilder.Build();

        var tmp = chart;
        if (tmp.Data.Labels is null)
            tmp = tmp.WithLabels(Enumerable.Range(1, deltas.Count).Select(i => i.ToString()).ToArray());

        var labels = tmp.Data.Labels?.ToArray();

        // TODO V10: need to follow up on passing totalIndexes through (2024/08/26, Dmitry Kalabin)
        var dataModel = deltas.CalculateModel(labels, totalIndexes: null);

        // TODO V10: extend to provide a way to specify labels (2024/08/26, Dmitry Kalabin)
        var incrementsLabel = ChartConst.Hidden;
        var decrementsLabel = ChartConst.Hidden;
        var totalLabel = ChartConst.Hidden;

        // TODO V10: take care about barDataSetModifier (2024/08/26, Dmitry Kalabin)
        Func<TDataSetBuilder, TDataSetBuilder> barDataSetModifier = o => o;

        var incrementRanges = dataModel.IncrementRanges.Select(d => new IncrementBar(d.range, d.label, d.delta, styling)).ToList();
        var decrementRanges = dataModel.DecrementRanges.Select(d => new DecrementBar(d.range, d.label, d.delta, styling)).ToList();
        var totalRanges = dataModel.TotalRanges.Select(d => new TotalBar(d.range, d.label, d.delta, styling)).ToList();

        var dataset1 = (TDataSet)new TDataSetBuilder()
            .WithDataRange(incrementRanges, incrementsLabel, dsb => (barDataSetModifier is null ? dsb : barDataSetModifier(dsb))
                                                                .WithParsing()
                                                                //.WithBarThickness("flex")
                                                                .WithBackgroundColor(ChartColor.FromHexString(styling.IncrementColor))
                                                                .WithHoverBackgroundColor(ChartColor.FromHexString(styling.IncrementColor)))
            .Build();
        var dataset2 = (TDataSet)new TDataSetBuilder()
            .WithDataRange(decrementRanges, decrementsLabel, dsb => (barDataSetModifier is null ? dsb : barDataSetModifier(dsb))
                                                                .WithParsing()
                                                                //.WithBarThickness("flex")
                                                                .WithBackgroundColor(ChartColor.FromHexString(styling.DecrementColor))
                                                                .WithHoverBackgroundColor(ChartColor.FromHexString(styling.DecrementColor)))
            .Build();
        var dataset3 = (TDataSet)new TDataSetBuilder()
            .WithDataRange(totalRanges, totalLabel, dsb => (barDataSetModifier is null ? dsb : barDataSetModifier(dsb))
                                                                .WithParsing()
                                                                //.WithBarThickness("flex")
                                                                .WithBackgroundColor(ChartColor.FromHexString(styling.TotalColor))
                                                                .WithHoverBackgroundColor(ChartColor.FromHexString(styling.TotalColor)))
            .Build();

        tmp = tmp with { DataSets = [dataset1, dataset2, dataset3] };

        // TODO V10: take care about includeConnectors and connectorDataSetModifier (2024/08/26, Dmitry Kalabin)
        var includeConnectors = true;
        Func<LineDataSetBuilder, LineDataSetBuilder> connectorDataSetModifier = d => d.ThinLine();

        if (includeConnectors)
        {
            LineDataSetBuilder Builder(LineDataSetBuilder b, IEnumerable<double?> data)
            {
                var builder = b.WithData(data).WithLabel(ChartConst.Hidden).WithDataLabels(new DataLabels { Display = false }).SetType(ChartType.Line);
                return connectorDataSetModifier != null
                           ? connectorDataSetModifier(builder)
                           : builder;
            }

            tmp = tmp
                  .WithDataSet(Builder(new(), dataModel.FirstDottedValues).Build())
                  .WithDataSet(Builder(new(), dataModel.SecondDottedValues).Build())
                  .WithDataSet(Builder(new(), dataModel.ThirdDottedValues).Build());
        }

        var palette = new[] { styling.IncrementColor, styling.DecrementColor, styling.TotalColor, styling.TotalColor, styling.TotalColor, styling.TotalColor };
        tmp = tmp.ApplyFinalStyling(palette);

        return tmp;
    }

    private static BarChart ApplyFinalStyling(this BarChart chart, string[] palette)
    {
        var tmp = chart;
        tmp = tmp
            .WithLegend(lm => lm with
            {
                Labels = (lm.Labels ?? new LegendLabel()) with
                {
                    Filter = $"item => item.text !== '{ChartConst.Hidden}'"
                }
            })
            .WithDataLabels(o => o with
            {
                Color = $"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.DataLabelColor).ToCamelCase()}",
                Align = $"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.DataLabelAlignment).ToCamelCase()}",
                Anchor = $"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.DataLabelAlignment).ToCamelCase()}",
                Display = true,//$"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.Range).ToCamelCase()} != null",
                Formatter = $"(value, context) => context.dataset.data[context.dataIndex]?.{nameof(WaterfallBar.DataLabel).ToCamelCase()}"
            })
            .WithColorPalette(palette);

        tmp = tmp.WithOptions(o => o.WithPlugins(p => p with { Tooltip = (p.Tooltip ?? new ToolTip()) with { Enabled = false } }));
        return tmp;
    }
}

public abstract record WaterfallChartBase<TChart, TDataSet, TDataSetBuilder> : RangeChart<TChart, TDataSet>
    where TChart : WaterfallChartBase<TChart, TDataSet, TDataSetBuilder>
    where TDataSet : BarDataSetBase, IDataSetWithStack, new()
    where TDataSetBuilder : FloatingBarDataSetBuilderBase<TDataSetBuilder, TDataSet>, new()
{
    protected WaterfallChartBase(IReadOnlyCollection<TDataSet> dataSets, ChartType chartType)
        : base(dataSets, chartType)
    {
    }

    private List<double> deltas;
    private readonly HashSet<int> totalIndexes = new();
    private string incrementsLabel = ChartConst.Hidden;
    private string decrementsLabel = ChartConst.Hidden;
    private string totalLabel = ChartConst.Hidden;
    private bool datasetsReady;
    private bool includeConnectors;
    private Func<LineDataSetBuilder, LineDataSetBuilder> connectorDataSetModifier = d => d.ThinLine();
    private Func<TDataSetBuilder, TDataSetBuilder> barDataSetModifier;
    private WaterfallStylingBuilder StylingBuilder { get; set; } = new();

    public override Models.Chart ToChart()
    {
        if (datasetsReady)
            return base.ToChart();
        var styling = StylingBuilder.Build();
        var incrementRanges = new List<IncrementBar>(deltas.Count);
        var decrementRanges = new List<DecrementBar>(deltas.Count);
        var totalRanges = new List<TotalBar>(deltas.Count);

        var firstDottedValues = new List<double?>(deltas.Count);
        var secondDottedValues = new List<double?>(deltas.Count);
        var thirdDottedValues = new List<double?>(deltas.Count);

        var tmp = this;
        if (Data.Labels is null)
            tmp = tmp.WithLabels(Enumerable.Range(1, deltas.Count).Select(i => i.ToString()).ToArray());

        var labels = Data.Labels?.ToArray();
        if (labels?.Length != deltas.Count)
            throw new ArgumentException("Labels length does not match data");

        var total = 0.0;
        var resetTotal = true;

        for (var index = 0; index < deltas.Count; index++)
        {
            var delta = deltas[index];
            var prevTotal = total;
            if (resetTotal)
                total = 0.0;

            var isTotal = totalIndexes != null && totalIndexes.Contains(index);

            if (isTotal)
            {
                totalRanges.Add(delta >= 0
                                    ? new TotalBar(new[] { 0, delta }, labels[index], delta, styling)
                                    : new TotalBar(new[] { delta, 0 }, labels[index], delta, styling));
                incrementRanges.Add(new IncrementBar(null, labels[index], null, styling));
                decrementRanges.Add(new DecrementBar(null, labels[index], null, styling));
                total = delta;
            }
            else
            {
                totalRanges.Add(new TotalBar(null, labels[index], null, styling));
                if (delta >= 0)
                {
                    incrementRanges.Add(new IncrementBar(new[] { total, total + delta }, labels[index], delta, styling));
                    decrementRanges.Add(new DecrementBar(null, labels[index], null, styling));
                }
                else
                {
                    decrementRanges.Add(new DecrementBar(new[] { total + delta, total }, labels[index], delta, styling));
                    incrementRanges.Add(new IncrementBar(null, labels[index], null, styling));
                }

                total += delta;
            }

            var beforeReset = index == deltas.Count - 1 || isTotal;

            if (index == 0)
            {
                firstDottedValues.Add(total);
                secondDottedValues.Add(null);
                thirdDottedValues.Add(null);
            }
            else
            {
                switch (index % 3)
                {
                    case 0:
                        if (!beforeReset)
                            firstDottedValues.Add(total);
                        else
                            firstDottedValues.Add(null);
                        secondDottedValues.Add(null);
                        thirdDottedValues.Add(prevTotal);
                        break;
                    case 1:
                        firstDottedValues.Add(prevTotal);
                        if (!beforeReset)
                            secondDottedValues.Add(total);
                        else
                            secondDottedValues.Add(null);
                        thirdDottedValues.Add(null);
                        break;
                    case 2:
                        firstDottedValues.Add(null);
                        secondDottedValues.Add(prevTotal);
                        if (!beforeReset)
                            thirdDottedValues.Add(total);
                        else
                            thirdDottedValues.Add(null);
                        break;
                }
            }

            resetTotal = beforeReset;
        }

        datasetsReady = true;

        var dataset1 = (TDataSet)new TDataSetBuilder()
            .WithDataRange(incrementRanges, incrementsLabel, dsb => (barDataSetModifier is null ? dsb : barDataSetModifier(dsb))
                                                                .WithParsing()
                                                                //.WithBarThickness("flex")
                                                                .WithBackgroundColor(ChartColor.FromHexString(styling.IncrementColor))
                                                                .WithHoverBackgroundColor(ChartColor.FromHexString(styling.IncrementColor)))
            .Build();
        var dataset2 = (TDataSet)new TDataSetBuilder()
            .WithDataRange(decrementRanges, decrementsLabel, dsb => (barDataSetModifier is null ? dsb : barDataSetModifier(dsb))
                                                                .WithParsing()
                                                                //.WithBarThickness("flex")
                                                                .WithBackgroundColor(ChartColor.FromHexString(styling.DecrementColor))
                                                                .WithHoverBackgroundColor(ChartColor.FromHexString(styling.DecrementColor)))
            .Build();
        var dataset3 = (TDataSet)new TDataSetBuilder()
            .WithDataRange(totalRanges, totalLabel, dsb => (barDataSetModifier is null ? dsb : barDataSetModifier(dsb))
                                                                .WithParsing()
                                                                //.WithBarThickness("flex")
                                                                .WithBackgroundColor(ChartColor.FromHexString(styling.TotalColor))
                                                                .WithHoverBackgroundColor(ChartColor.FromHexString(styling.TotalColor)))
            .Build();

        tmp = tmp with { DataSets = [dataset1, dataset2, dataset3] };  // HACK V10: This looks extreemely bad to combine this immutability style "with" constraint with all the rest of the logic to just mutate single instance (2024/08/22, Dmitry Kalabin)

        if (includeConnectors)
        {
            LineDataSetBuilder Builder(LineDataSetBuilder b, IEnumerable<double?> data)
            {
                var builder = b.WithData(data).WithLabel(ChartConst.Hidden).WithDataLabels(new DataLabels { Display = false }).SetType(ChartType.Line);
                return connectorDataSetModifier != null
                           ? connectorDataSetModifier(builder)
                           : builder;
            }

            tmp = tmp
                  .WithDataSet(Builder(new(), firstDottedValues).Build())
                  .WithDataSet(Builder(new(), secondDottedValues).Build())
                  .WithDataSet(Builder(new(), thirdDottedValues).Build());
        }

        var palette = new[] { styling.IncrementColor, styling.DecrementColor, styling.TotalColor, styling.TotalColor, styling.TotalColor, styling.TotalColor };
        tmp = tmp.WithLegend(lm => lm with
        {
            Labels = (lm.Labels ?? new LegendLabel()) with
            {
                Filter = $"item => item.text !== '{ChartConst.Hidden}'"
            }
        })
                 .WithDataLabels(o => o with
                 {
                     Color = $"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.DataLabelColor).ToCamelCase()}",
                     Align = $"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.DataLabelAlignment).ToCamelCase()}",
                     Anchor = $"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.DataLabelAlignment).ToCamelCase()}",
                     Display = true,//$"context => context.dataset.data[context.dataIndex].{nameof(WaterfallBar.Range).ToCamelCase()} != null",
                     Formatter = $"(value, context) => context.dataset.data[context.dataIndex]?.{nameof(WaterfallBar.DataLabel).ToCamelCase()}"
                 })
                 .WithColorPalette(palette);

        tmp = tmp.WithOptions(o => o.WithPlugins(p => p with { Tooltip = (p.Tooltip ?? new ToolTip()) with { Enabled = false } }));

        return tmp.ToChart();
    }

    public TChart WithLegendItems(string incrementsLabel = null, string decrementsLabel = null, string totalLabel = null)
    {
        this.incrementsLabel = incrementsLabel;
        this.decrementsLabel = decrementsLabel;
        this.totalLabel = totalLabel;
        return (TChart)this;
    }

    public TChart WithStylingOptions(Func<WaterfallStylingBuilder, WaterfallStylingBuilder> func)
    {
        StylingBuilder = func(StylingBuilder);
        return (TChart)this;
    }

    /// <summary>
    /// Show lines that connect bars.
    /// </summary>
    /// <param name="connectorLineModifier">Override default dataset modifier (by default it's b => b.Dashed()</param>
    /// <returns>The builder</returns>
    public TChart WithConnectors(Func<LineDataSetBuilder, LineDataSetBuilder> connectorLineModifier = null)
    {
        if (connectorLineModifier != null)
            connectorDataSetModifier = connectorLineModifier;
        includeConnectors = true;
        return (TChart)this;
    }

    public TChart WithTotalsAtPositions(HashSet<int> totalIndexes)
    {
        this.totalIndexes.UnionWith(totalIndexes);
        return (TChart)this;
    }

    public TChart WithTotalsAtPositions(IEnumerable<int> totalIndexes)
        => WithTotalsAtPositions(totalIndexes.ToHashSet());

    public TChart WithTotalsAtPositions(params int[] totalIndexes)
        => WithTotalsAtPositions(totalIndexes.ToHashSet());

    public TChart WithDeltas(List<double> deltas)
    {
        this.deltas = deltas;
        return (TChart)this;
    }
    public TChart WithBarDataSetOptions(Func<TDataSetBuilder, TDataSetBuilder> barDataSetModifier)
    {
        this.barDataSetModifier = barDataSetModifier;
        return (TChart)this;
    }

    public TChart WithDeltas(IEnumerable<double> deltas)
        => WithDeltas(deltas.ToList());

    public TChart WithDeltas(params double[] deltas)
        => WithDeltas(deltas.AsEnumerable());

    /// <summary>
    /// Add one more value that will be a sum and mark it as total
    /// </summary>
    /// <returns>The builder</returns>
    public TChart WithLastAsTotal()
    {
        deltas = deltas.Append(deltas.Sum()).ToList();
        totalIndexes.Add(deltas.Count - 1);
        return (TChart)this;
    }
}

public record WaterfallChart
    : WaterfallChartBase<WaterfallChart, FloatingBarDataSet, FloatingBarDataSetBuilder>/*(ChartModel, (OptionsBuilder ?? new RangeOptionsBuilder()).Stacked("x")
                                                                                                                                                               .HideAxis("y")
                                                                                                                                                               .HideGrid("x"))*/
{
    public WaterfallChart(IReadOnlyCollection<FloatingBarDataSet> dataSets) : base(dataSets, ChartType.Bar)
    {
        Options = Options
            .Stacked("x")
            .HideAxis("y")
            .HideGrid("x");
    }

    //public WaterfallChart() : this(new Chart(ChartType.Bar)) { }
}

public record HorizontalWaterfallChart//(Chart ChartModel = null, RangeOptionsBuilder OptionsBuilder = null)
    : WaterfallChartBase<HorizontalWaterfallChart, HorizontalFloatingBarDataSet, HorizontalFloatingBarDataSetBuilder>/*(ChartModel, (OptionsBuilder ?? new RangeOptionsBuilder())
                                                                                                                                                .Stacked("y")
                                                                                                                                                //.HideAxis("x")
                                                                                                                                                .Grace<CartesianLinearScale>("x","10%")
                                                                                                                                                // TODO V10: understand why the line below helps and find less random approach (2023/10/08, Ekaterina Mishina)
                                                                                                                                                .SuggestedMax("x", 10) // this helps in case of all negative values
                                                                                                                                                .ShortenAxisNumbers("x")
                                                                                                                                                )*/
{
    public HorizontalWaterfallChart(IReadOnlyCollection<HorizontalFloatingBarDataSet> dataSets) : base(dataSets, ChartType.Bar)
    {
        Options = Options
            .Stacked("y")
            //.HideAxis("x")
            .Grace<CartesianLinearScale>("x", "10%")
            // TODO V10: understand why the line below helps and find less random approach (2023/10/08, Ekaterina Mishina)
            .SuggestedMax("x", 10) // this helps in case of all negative values
            .ShortenAxisNumbers("x")
            .WithIndexAxis("y")
        ;
    }

    //public HorizontalWaterfallChartBuilder()
    //    : this(new Chart(ChartType.Bar))
    //{
    //    OptionsBuilder = OptionsBuilder.WithIndexAxis("y");
    //}
}
