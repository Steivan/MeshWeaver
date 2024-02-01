﻿using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using OpenSmc.Activities;
using OpenSmc.DataSetReader;
using OpenSmc.DataSource.Abstractions;
using OpenSmc.DataStructures;
using OpenSmc.DomainDesigner.Abstractions;
using OpenSmc.FileStorage;
using OpenSmc.Import.Mapping;
using OpenSmc.Import.Options;

namespace OpenSmc.Import.Builders
{
    public record StreamImportOptionsBuilder : ImportOptionsBuilder
    {
        private Stream Stream { get; }

        internal StreamImportOptionsBuilder(IActivityService activityService,
                                            IDataSetReaderVariable dataSetReaderVariable,
                                            IMappingService mappingService,
                                            IFileReadStorage storage,
                                            CancellationToken cancellationToken,
                                            DomainDescriptor domain,
                                            IDataSource targetSource,
                                            IServiceProvider serviceProvider,
                                            Dictionary<string, Func<ImportOptions, IDataSet, Task>> importFormatFunctions,
                                            ImmutableList<Func<object, ValidationContext, Task<bool>>> defaultValidations,
                                            Stream stream)
            : base(activityService, dataSetReaderVariable, mappingService, storage, cancellationToken, domain, targetSource, serviceProvider, importFormatFunctions, defaultValidations)
        {
            Stream = stream;
        }

        protected override async Task<(IDataSet DataSet, string Format)> GetDataSetAsync()
        {
            var optionsBuilder = DataSetReaderOptionsBuilderFunc(DataSetReaderVariable.ReadFromStream(Stream));
            var contentType = ((DataSetReaderOptionsBuilder)optionsBuilder).ContentType;
            return await optionsBuilder.WithContentType(contentType).ExecuteAsync();
        }


        public override StreamImportOptions GetImportOptions()
        {
            return new(Stream, Format, Domain, TargetDataSource, Storage, Validations,
                       TableMappingBuilders.ToImmutableDictionary(x => x.Key,
                                                                  x => x.Value.GetMapper()),
                           IgnoredTables, IgnoredTypes, IgnoredColumns, SnapshotModeEnabled);
        }
    }
}
