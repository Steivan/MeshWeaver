﻿using System.Collections.Immutable;
using Json.Patch;
using Microsoft.Extensions.DependencyInjection;
using MeshWeaver.Activities;
using MeshWeaver.Data.Persistence;
using MeshWeaver.Data.Serialization;
using MeshWeaver.Domain;
using MeshWeaver.Messaging;

namespace MeshWeaver.Data;

public static class DataPluginExtensions
{
    public static MessageHubConfiguration AddData(this MessageHubConfiguration config) =>
        config.AddData(x => x);

    public static MessageHubConfiguration AddData(
        this MessageHubConfiguration config,
        Func<DataContext, DataContext> dataPluginConfiguration
    )
    {
        var existingLambdas = config.GetListOfLambdas();
        var ret = config
            .AddActivities()
            .WithServices(sc => sc.AddScoped<IWorkspace, Workspace>())
            .WithSerialization(serialization =>
                serialization.WithOptions(options =>
                {
                    if (!options.Converters.Any(c => c is EntityStoreConverter))
                        options.Converters.Insert(
                            0,
                            new EntityStoreConverter(
                                serialization.Hub.ServiceProvider.GetRequiredService<ITypeRegistry>()
                            )
                        );
                    if (!options.Converters.Any(c => c is InstancesInCollectionConverter))
                        options.Converters.Insert(
                            0,
                            new InstancesInCollectionConverter(
                                serialization.Hub.ServiceProvider.GetRequiredService<ITypeRegistry>()
                            )
                        );
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
                typeof(JsonPointerReference),
                typeof(JsonPatch),
                typeof(JsonElementReference),
                typeof(DataChangedEvent),
                typeof(UpdateDataRequest),
                typeof(DeleteDataRequest),
                typeof(DataChangeResponse),
                typeof(SubscribeRequest),
                typeof(UnsubscribeDataRequest)
            )
            .RegisterDataEvents()
            ;

        return ret;
    }

    internal static ImmutableList<Func<DataContext, DataContext>> GetListOfLambdas(
        this MessageHubConfiguration config
    )
    {
        return config.Get<ImmutableList<Func<DataContext, DataContext>>>()
            ?? ImmutableList<Func<DataContext, DataContext>>.Empty;
    }

    internal static DataContext GetDataConfiguration(this IWorkspace workspace)
    {
        var dataPluginConfig = workspace.Hub.Configuration.GetListOfLambdas();
        var ret = new DataContext(workspace);
        foreach (var func in dataPluginConfig)
            ret = func.Invoke(ret);
        return ret;
    }

    public static DataContext FromPartitionedHubs(
        this DataContext dataContext,
        object id,
        Func<PartitionedHubDataSource, PartitionedHubDataSource> configuration
    ) =>
        dataContext.WithDataSourceBuilder(
            id,
            hub => configuration.Invoke(new PartitionedHubDataSource(id, dataContext.Workspace))
        );

    public static DataContext FromHub(
        this DataContext dataContext,
        object address,
        Func<HubDataSource, HubDataSource> configuration
    ) =>
        dataContext.WithDataSourceBuilder(
            address,
            hub => configuration.Invoke(new HubDataSource(address, dataContext.Workspace))
        );

    public static DataContext FromConfigurableDataSource(
        this DataContext dataContext,
        object address,
        Func<GenericDataSource, GenericDataSource> configuration
    ) =>
        dataContext.WithDataSourceBuilder(
            address,
            hub => configuration.Invoke(new GenericDataSource(address, dataContext.Workspace))
        );
    private static MessageHubConfiguration RegisterDataEvents(this MessageHubConfiguration configuration) =>
        configuration
            .WithHandler<UpdateDataRequest>((hub, request) =>
            {
                hub.GetWorkspace().RequestChange(request.Message, request);
                return request.Processed();
            })
            .WithHandler<SubscribeRequest>((hub, request) =>
            {
                hub.GetWorkspace().SubscribeToClient(request.Sender, (dynamic)request.Message.Reference);
                return request.Processed();
            })
            .WithHandler<UnsubscribeDataRequest>((hub, request) =>
            {
                hub.GetWorkspace().Unsubscribe(request.Sender, request.Message.Reference);
                return request.Processed();
            })
            .WithHandler<DataChangedRequest>((hub, request) =>
            {
                hub.GetWorkspace().RequestChange(request.Message, request);
                return request.Processed();
            });



}
