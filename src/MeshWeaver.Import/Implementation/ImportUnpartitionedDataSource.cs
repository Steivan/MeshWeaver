﻿using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MeshWeaver.Data;
using MeshWeaver.Import.Configuration;

namespace MeshWeaver.Import.Implementation;

public record ImportUnpartitionedDataSource(Source Source, IWorkspace Workspace)
    : GenericUnpartitionedDataSource<ImportUnpartitionedDataSource>(Source, Workspace)
{
    private ImportRequest ImportRequest { get; init; } = new(Source);

    public ImportUnpartitionedDataSource WithRequest(Func<ImportRequest, ImportRequest> config) =>
        this with
        {
            ImportRequest = config.Invoke(ImportRequest)
        };


    protected override async Task<EntityStore> GetInitialValue(ISynchronizationStream<EntityStore> stream, CancellationToken cancellationToken)
    {
        var importManager = Workspace.Hub.ServiceProvider.GetRequiredService<ImportManager>();
        var instances = await importManager.ImportInstancesAsync(
            ImportRequest,
            new(Data.Serialization.ActivityCategory.Import, Workspace.Hub),
            cancellationToken
        );
        return new(instances.GroupBy(i => i.GetType())
            .Select(t =>
            {
                var typeSource = Workspace.DataContext.GetTypeSource(t.Key);
                if (typeSource == null)
                    return default;
                return new KeyValuePair<string, InstanceCollection>(
                    typeSource.CollectionName, new(t.ToDictionary(typeSource.TypeDefinition.GetKey)));
            })
            .Where(x => x.Key is not null)
            .ToDictionary());

    }

    private ImmutableList<
        Func<ImportConfiguration, ImportConfiguration>
    > Configurations { get; init; } =
        ImmutableList<Func<ImportConfiguration, ImportConfiguration>>.Empty;

    public ImportUnpartitionedDataSource WithImportConfiguration(
        Func<ImportConfiguration, ImportConfiguration> config
    ) => this with { Configurations = Configurations.Add(config) };
}
