﻿using OpenSmc.Messaging;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Data.Serialization;

namespace OpenSmc.Data;

public sealed record DataContext(IMessageHub Hub, IWorkspace Workspace) : IAsyncDisposable
{
    internal ImmutableDictionary<object,IDataSource> DataSources { get; private set; } = ImmutableDictionary<object, IDataSource>.Empty;


    public IDataSource GetDataSource(object id) => DataSources.GetValueOrDefault(id);

    public IEnumerable<Type> MappedTypes => DataSources.Values.SelectMany(ds => ds.MappedTypes);

    public DataContext WithDataSourceBuilder(object id, DataSourceBuilder dataSourceBuilder) => this with
    {
        DataSourceBuilders = DataSourceBuilders.Add(id, dataSourceBuilder),
    };

    public ImmutableDictionary<object, DataSourceBuilder> DataSourceBuilders { get; set; } = ImmutableDictionary<object, DataSourceBuilder>.Empty;
    internal ReduceManager ReduceManager { get; init; }

    public DataContext AddWorkspaceReferenceStream<TReference>(Func<IObservable<WorkspaceState>, TReference, IObservable<object>> referenceDefinition)
        where TReference : WorkspaceReference
        => this with { ReduceManager = ReduceManager.AddWorkspaceReferenceStream(referenceDefinition) };
    public DataContext AddWorkspaceReference<TReference>(Func<WorkspaceState, TReference, object> referenceDefinition)
        where TReference : WorkspaceReference
        => this with { ReduceManager = ReduceManager.AddWorkspaceReference(referenceDefinition) };

    public delegate IDataSource DataSourceBuilder(IMessageHub hub); 

    public IReadOnlyCollection<ChangeStream<EntityStore>> Initialize()
    {
        DataSources = DataSourceBuilders
            .ToImmutableDictionary(kvp => kvp.Key,
                kvp => kvp.Value.Invoke(Hub));

        var streams = DataSources
            .Values
            .SelectMany(ds => ds.Initialize())
            .ToArray();

        return streams;
    }




    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in DataSources.Values)
        {
            await dataSource.DisposeAsync();
        }
    }
}