﻿using MeshWeaver.Charting.Enums;
using MeshWeaver.Charting.Models.Options.Scales;

namespace MeshWeaver.Charting.Builders.OptionsBuilders;

public record TimeOptionsBuilder : OptionsBuilderBase<TimeOptionsBuilder>
{
    public TimeOptionsBuilder SetTimeUnit(TimeIntervals unit)
    {
        Options = Options with { Scales = Options.Scales ?? new Dictionary<string, Scale>() };

        if (!Options.Scales.TryGetValue("x", out var rawScale) || rawScale is not TimeScale)
            rawScale = new TimeScale();

        var scale = (TimeScale)rawScale;
        scale = scale with { Time = (scale.Time ?? new Time()) with { Unit = unit } };
        Options.Scales["x"] = scale;

        return this;
    }

    public TimeOptionsBuilder SetTimeFormat(string format)
    {
        Options = Options with { Scales = Options.Scales ?? new Dictionary<string, Scale>() };
        if (!Options.Scales.TryGetValue("x", out var rawScale) || rawScale is not TimeScale)
            rawScale = new TimeScale();

        var scale = (TimeScale)rawScale;

        if (scale.Time == null)
            throw new ArgumentException("Please set the Time Unit before setting the Time Format.");

        var timeConfig = scale.Time;
        timeConfig = timeConfig with { DisplayFormats = new TimeDisplayFormat(timeConfig.Unit, format) };
        scale = scale with { Time = timeConfig };
        Options.Scales["x"] = scale;

        return this;
    }
}