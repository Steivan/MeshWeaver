﻿using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using OpenSmc.Data;
using OpenSmc.FileStorage;

namespace OpenSmc.Import.Options
{
    public record StreamImportOptions : ImportOptions
    {
        public Stream Stream { get; init; }

        public StreamImportOptions(Stream stream, 
                                   string format, 
                                   IWorkspace targetDataSource, 
                                   IFileReadStorage storage,
                                   ImmutableList<Func<object, ValidationContext, Task<bool>>> validations,
                                   ImmutableDictionary<Type, TableMapping> tableMappings, 
                                   ImmutableHashSet<string> ignoredTables, ImmutableHashSet<Type> ignoredTypes, 
                                   ImmutableDictionary<string, IEnumerable<string>> ignoredColumns, 
                                   bool snapshotModeEnabled)
            : base(format, targetDataSource, storage, validations, tableMappings, ignoredTables, ignoredTypes, ignoredColumns, snapshotModeEnabled)
        {
            Stream = stream;
        }
    }
}
