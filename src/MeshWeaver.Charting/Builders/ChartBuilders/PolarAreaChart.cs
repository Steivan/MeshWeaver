﻿using MeshWeaver.Charting.Enums;
using MeshWeaver.Charting.Models;

namespace MeshWeaver.Charting.Builders.ChartBuilders;

public record PolarAreaChart
    : ArrayChart<PolarAreaChart, PolarDataSet>
{
    public PolarAreaChart(IReadOnlyCollection<PolarDataSet> dataSets) : base(dataSets, ChartType.PolarArea) { }
}
