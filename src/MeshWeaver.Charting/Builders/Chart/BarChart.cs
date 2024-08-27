﻿using System.Text.Json.Serialization;
using MeshWeaver.Charting.Enums;
using MeshWeaver.Charting.Models;

namespace MeshWeaver.Charting.Builders.Chart;

public record BarChart
    : ArrayChart<BarChart, BarDataSet>
{
    public BarChart(IReadOnlyCollection<BarDataSet> dataSets) : base(dataSets, ChartType.Bar) { }

    public BarChart AsHorizontalBar()
    {
        return WithOptions(options => options.WithIndexAxis("y"));
    }

    [JsonIgnore]
    public bool IsHorizontal => Options.IndexAxis == "y";
}
