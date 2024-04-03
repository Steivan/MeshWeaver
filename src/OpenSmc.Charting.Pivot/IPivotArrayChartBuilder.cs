﻿using OpenSmc.Charting.Models.Options;

namespace OpenSmc.Charting.Pivot;

public interface IPivotArrayChartBuilder : IPivotChartBuilder
{
    IPivotArrayChartBuilder WithSmoothedLines(params string[] linesToSmooth);
    IPivotArrayChartBuilder WithSmoothedLines(Dictionary<string, double> smoothDictionary);
    IPivotArrayChartBuilder WithFilledArea(params string[] rows);
    IPivotArrayChartBuilder WithLegend(Func<Legend, Legend> func);

}