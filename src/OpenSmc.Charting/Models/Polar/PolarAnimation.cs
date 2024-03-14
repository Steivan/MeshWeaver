﻿using OpenSmc.Charting.Models.Options.Animation;

// ReSharper disable once CheckNamespace
namespace Systemorph.Charting.Models
{
    public record PolarAnimation : Animation
    {
        /// <summary>
        /// If true, will animate the rotation of the chart.
        /// </summary>
        public bool? AnimateRotate { get; init; }

        /// <summary>
        /// If true, will animate scaling the chart.
        /// </summary>
        public bool? AnimateScale { get; init; }
    }
}
