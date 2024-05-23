﻿using System.Collections.Immutable;
using Json.Patch;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Activities;
using OpenSmc.Data.Persistence;
using OpenSmc.Data.Serialization;
using OpenSmc.Messaging;
using OpenSmc.Messaging.Serialization;

namespace OpenSmc.Data;

public static class DataPluginExtensions
{
    public static MessageHubConfiguration AddData(
        this MessageHubConfiguration config,
        Func<DataContext, DataContext> dataPluginConfiguration
    )
    {
        var existingLambdas = config.GetListOfLambdas();
        var ret = config
            .AddActivities()
            .WithServices(sc => sc.AddScoped<IWorkspace, Workspace>().AddScoped<DataPlugin>())
            .WithSerialization(serialization =>
                serialization.WithOptions(options =>
                {
                    if (!options.Converters.Any(c => c is WorkspaceStateConverter))
                        options.Converters.Insert(
                            0,
                            new WorkspaceStateConverter(serialization.Hub)
                        );
                    if (!options.Converters.Any(c => c is EntityStoreConverter))
                        options.Converters.Insert(
                            0,
                            new EntityStoreConverter(
                                serialization.Hub.ServiceProvider.GetRequiredService<ITypeRegistry>()
                            )
                        );
                    if (!options.Converters.Any(c => c is InstancesInCollectionConverter))
                        options.Converters.Insert(0, new InstancesInCollectionConverter());
                    if (!options.Converters.Any(c => c is DataChangedEventConverter))
                        options.Converters.Insert(0, new DataChangedEventConverter());
                })
            )
            .Set(existingLambdas.Add(dataPluginConfiguration))
            .WithTypes(
                typeof(EntityStore),
                typeof(InstanceCollection),
                typeof(EntityReference),
                typeof(CollectionReference),
                typeof(CollectionsReference),
                typeof(WorkspaceStoreReference),
                typeof(JsonPathReference),
                typeof(JsonPatch),
                typeof(DataChangedEvent),
                typeof(UpdateDataRequest),
                typeof(DeleteDataRequest),
                typeof(PatchChangeRequest),
                typeof(DataChangeResponse),
                typeof(SubscribeRequest),
                typeof(UnsubscribeDataRequest)
            )
            .AddPlugin<DataPlugin>();

        return ret;
    }

    internal static ImmutableList<Func<DataContext, DataContext>> GetListOfLambdas(
        this MessageHubConfiguration config
    )
    {
        return config.Get<ImmutableList<Func<DataContext, DataContext>>>()
            ?? ImmutableList<Func<DataContext, DataContext>>.Empty;
    }

    internal static DataContext GetDataConfiguration(this IMessageHub hub)
    {
        var dataPluginConfig = hub.Configuration.GetListOfLambdas();
        var ret = new DataContext(hub);
        foreach (var func in dataPluginConfig)
            ret = func.Invoke(ret);
        return ret;
    }

    public static DataContext FromPartitionedHubs(
        this DataContext dataSource,
        object id,
        Func<PartitionedHubDataSource, PartitionedHubDataSource> configuration
    ) =>
        dataSource.WithDataSourceBuilder(
            id,
            hub => configuration.Invoke(new PartitionedHubDataSource(id, hub))
        );

    public static DataContext FromHub(
        this DataContext dataSource,
        object address,
        Func<HubDataSource, HubDataSource> configuration
    ) =>
        dataSource.WithDataSourceBuilder(
            address,
            hub => configuration.Invoke(new HubDataSource(address, hub))
        );

    public static DataContext FromConfigurableDataSource(
        this DataContext dataSource,
        object address,
        Func<GenericDataSource, GenericDataSource> configuration
    ) =>
        dataSource.WithDataSourceBuilder(
            address,
            hub => configuration.Invoke(new GenericDataSource(address, hub))
        );

    public static ChangeStreamHost<TStream, TReference> ToChangeStreamHost<TStream, TReference>(
        this IChangeStream<TStream, TReference> stream
    )
        where TReference : WorkspaceReference<TStream> => new(stream);

    public static ChangeStreamClient<TStream, TReference> ToChangeStreamClient<TStream, TReference>(
        this IChangeStream<TStream, TReference> stream
    )
        where TReference : WorkspaceReference<TStream> => new(stream);
}
