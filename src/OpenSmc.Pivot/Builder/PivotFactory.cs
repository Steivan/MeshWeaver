﻿using OpenSmc.Collections;
using OpenSmc.DataCubes;
using OpenSmc.Pivot.Aggregations;

namespace OpenSmc.Pivot.Builder;

public static class PivotFactory
{
    public static PivotBuilder<T, T, T> ForObjects<T>(IEnumerable<T> objects)
    {
        return new PivotBuilder<T, T, T>(objects).WithAggregation(a => a.Sum());
    }

    public static DataCubePivotBuilder<
        IDataCube<TElement>,
        TElement,
        TElement,
        TElement
    > ForDataCubes<TElement>(IEnumerable<IDataCube<TElement>> cubes)
    {
        var pivotBuilder = new DataCubePivotBuilder<
            IDataCube<TElement>,
            TElement,
            TElement,
            TElement
        >(cubes);
        pivotBuilder = pivotBuilder with
        {
            Aggregations = new Aggregations<DataSlice<TElement>, TElement>
            {
                Aggregation = slices =>
                    AggregationsExtensions.Aggregation(slices.Select(s => s.Data).ToArray()),
                AggregationOfAggregates = AggregationsExtensions.Aggregation
            }
        };

        return pivotBuilder;
    }

    public static DataCubePivotBuilder<
        IDataCube<TElement>,
        TElement,
        TElement,
        TElement
    > ForDataCube<TElement>(IDataCube<TElement> cube)
    {
        return ForDataCubes(cube.RepeatOnce());
    }
}
