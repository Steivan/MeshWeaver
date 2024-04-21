﻿using System.Collections.Immutable;
using OpenSmc.Domain.Abstractions;
using OpenSmc.Pivot.Models.Interfaces;

namespace OpenSmc.Pivot.Grouping
{
    public class NamedPivotGrouper<T, TGroup> : DirectPivotGrouper<T, TGroup>
        where TGroup : class, IGroup, new()
        where T : INamed
    {
        public NamedPivotGrouper(string name, Func<T, object> keySelector)
            : base(
                x =>
                    x.GroupBy(o =>
                    {
                        var id = keySelector(o);
                        return new TGroup
                        {
                            Id = id,
                            DisplayName = o.DisplayName,
                            GrouperId = name,
                            Coordinates = [id]
                        };
                    }),
                name
            ) { }
    }
}
