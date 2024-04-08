﻿using System.Collections.Immutable;
using Json.Patch;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Data.Persistence;
using OpenSmc.Messaging;
using OpenSmc.Messaging.Serialization;
using OpenSmc.Messaging.Serialization.Newtonsoft;

namespace OpenSmc.Data;

public static class DataPluginExtensions
{
    public static MessageHubConfiguration AddData(this MessageHubConfiguration config, Func<DataContext, DataContext> dataPluginConfiguration)
    {
        var existingLambdas = config.GetListOfLambdas();
        var ret = config
            .WithServices(sc => sc.AddScoped<IWorkspace, DataPlugin>())
            .WithSerialization(serialization => serialization.WithOptions(options => options.Converters.Insert(0, new JsonPatchConverter())))
            .WithSerialization(serialization => serialization.WithOptions(options => options.Converters.Insert(0, new DataChangedEventConverter())))
            .Set(existingLambdas.Add(dataPluginConfiguration))
            .WithTypes(typeof(EntityStore), typeof(InstanceCollection), typeof(EntityReference), typeof(CollectionReference), typeof(CollectionsReference), typeof(EntireWorkspace), typeof(JsonPathReference), typeof(JsonPatch))
            .AddPlugin<DataPlugin>(plugin => plugin.WithFactory(() => (DataPlugin)plugin.Hub.ServiceProvider.GetRequiredService<IWorkspace>()));

        return ret;
    }

    internal static ImmutableList<Func<DataContext, DataContext>> GetListOfLambdas(this MessageHubConfiguration config)
    {
        return config.Get<ImmutableList<Func<DataContext, DataContext>>>() ?? ImmutableList<Func<DataContext, DataContext>>.Empty;
    }

    internal static DataContext GetDataConfiguration(this IMessageHub hub, ReduceManager reduceManager)
    {
        var dataPluginConfig = hub.Configuration.GetListOfLambdas();
        var ret = new DataContext(hub, hub.ServiceProvider.GetRequiredService<IWorkspace>()){ReduceManager = reduceManager};
        foreach (var func in dataPluginConfig)
            ret = func.Invoke(ret);
        return ret;
    }




    public static DataContext FromPartitionedHubs(this DataContext dataSource, object id,
        Func<PartitionedHubDataSource, PartitionedHubDataSource> configuration)
        => dataSource.WithDataSourceBuilder(id, hub => configuration.Invoke(new PartitionedHubDataSource(id, hub))
        );

    public static DataContext FromHub(this DataContext dataSource, object address)
        => FromHub(dataSource, address, ds => ds.SynchronizeAll());

    public static DataContext FromHub(this DataContext dataSource, object address,
        Func<HubDataSource, HubDataSource> configuration)
        => dataSource.WithDataSourceBuilder(address, hub => configuration.Invoke(new HubDataSource(address, hub))
        );
    public static DataContext FromConfigurableDataSource(this DataContext dataSource, object address,
        Func<GenericDataSource, GenericDataSource> configuration)
        => dataSource.WithDataSourceBuilder(address, hub => configuration.Invoke(new GenericDataSource(address, hub))
        );


}