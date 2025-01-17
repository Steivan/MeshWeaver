﻿using MeshWeaver.Charting.Models;

namespace MeshWeaver.Charting.Builders.DataSetBuilders;

public record RadarDataSetBuilder : ArrayDataSetWithTensionFillPointRadiusAndRotation<RadarDataSetBuilder, RadarDataSet>;