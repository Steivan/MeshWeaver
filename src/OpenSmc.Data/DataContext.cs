﻿using System.Collections.Immutable;
using System.Security.Cryptography;
using OpenSmc.Data.Serialization;
using OpenSmc.Messaging;

namespace OpenSmc.Data;

public sealed record DataContext(IMessageHub Hub, IWorkspace Workspace) : IAsyncDisposable
{
    internal ImmutableDictionary<object, IDataSource> DataSources { get; private set; } =
        ImmutableDictionary<object, IDataSource>.Empty;

    public IDataSource GetDataSource(object id) => DataSources.GetValueOrDefault(id);

    public IEnumerable<Type> MappedTypes => DataSources.Values.SelectMany(ds => ds.MappedTypes);

    public DataContext WithDataSourceBuilder(object id, DataSourceBuilder dataSourceBuilder) =>
        this with
        {
            DataSourceBuilders = DataSourceBuilders.Add(id, dataSourceBuilder),
        };

    public Task Initialized => Task.WhenAll(DataSources.Values.Select(ds => ds.Initialized));
    public ImmutableDictionary<object, DataSourceBuilder> DataSourceBuilders { get; set; } =
        ImmutableDictionary<object, DataSourceBuilder>.Empty;
    internal ReduceManager<WorkspaceState> ReduceManager { get; init; }
    internal TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromHours(60);

    public DataContext WithInitializationTieout(TimeSpan timeout) =>
        this with
        {
            InitializationTimeout = timeout
        };

    public DataContext AddWorkspaceReferenceStream<TReference, TStream>(
        Func<
            IObservable<ChangeItem<WorkspaceState>>,
            TReference,
            IObservable<ChangeItem<TStream>>
        > referenceDefinition,
        Func<TStream, WorkspaceState> backTransformation
    )
        where TReference : WorkspaceReference<TStream> =>
        this with
        {
            ReduceManager = ReduceManager.AddWorkspaceReferenceStream(
                referenceDefinition,
                backTransformation
            )
        };

    public DataContext AddWorkspaceReference<TReference, TStream>(
        Func<WorkspaceState, TReference, TStream> referenceDefinition,
        Func<TStream, WorkspaceState> backTransformation
    )
        where TReference : WorkspaceReference<TStream> =>
        this with
        {
            ReduceManager = ReduceManager.AddWorkspaceReference(
                referenceDefinition,
                backTransformation
            )
        };

    public delegate IDataSource DataSourceBuilder(IMessageHub hub);

    public void Initialize()
    {
        DataSources = DataSourceBuilders.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Invoke(Hub)
        );

        foreach (var dataSource in DataSources.Values)
            dataSource.Initialize();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in DataSources.Values)
        {
            await dataSource.DisposeAsync();
        }
    }
}
