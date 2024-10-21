﻿using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeshWeaver.Activities;
using Microsoft.Extensions.Logging;
using MeshWeaver.Data.Serialization;
using MeshWeaver.Disposables;
using MeshWeaver.Messaging;
using MeshWeaver.Reflection;
using MeshWeaver.ShortGuid;

namespace MeshWeaver.Data;

public class Workspace : IWorkspace
{
    public Workspace(IMessageHub hub, ILogger<Workspace> logger)
    {
        Hub = hub;
        this.logger = logger;
        logger.LogDebug("Creating data context of address {address}", Id);
        DataContext = this.GetDataConfiguration();

        //stream.OnNext(new(stream.Owner, stream.Reference, new(), null, ChangeType.NoUpdate, null, stream.Hub.Version));
        logger.LogDebug("Started initialization of data context of address {address}", Id);
        DataContext.Initialize();


    }

    private readonly ILogger<Workspace> logger;



    public IReadOnlyCollection<Type> MappedTypes => DataContext.MappedTypes.ToArray();

    private readonly ConcurrentDictionary<
        (object Subscriber, object Reference),
        IAsyncDisposable
    > subscriptions = new();
    private readonly ConcurrentDictionary<
        (object Subscriber, object Reference),
        ISynchronizationStream
    > remoteStreams = new();


    public IObservable<IReadOnlyCollection<T>> GetStream<T>()
    {
        var collection = DataContext.GetTypeSource(typeof(T));
        if (collection == null)
            return null;
        return GetStream(typeof(T))
            .Select(x => x.Value.Collections.SingleOrDefault().Value?.Instances.Values.Cast<T>().ToArray());
    }

    public ISynchronizationStream<TReduced> GetRemoteStream<TReduced>(
        object id,
        WorkspaceReference<TReduced> reference
    ) =>
        (ISynchronizationStream<TReduced>)
            GetSynchronizationStreamMethod
                .MakeGenericMethod(typeof(TReduced), reference.GetType())
                .Invoke(this, [id, reference]);


    private static readonly MethodInfo GetSynchronizationStreamMethod =
        ReflectionHelper.GetMethodGeneric<Workspace>(x =>
            x.GetRemoteStream<object, WorkspaceReference<object>>(default, default)
        );

    public ISynchronizationStream<TReduced> GetRemoteStream<TReduced, TReference>(
        TReference reference
    )
        where TReference : WorkspaceReference =>
        GetRemoteStream<TReduced, TReference>(Hub.Address, reference);

    public ISynchronizationStream<TReduced> GetRemoteStream<TReduced, TReference>(
        object owner,
        TReference reference
    )
        where TReference : WorkspaceReference =>
        Hub.Address.Equals(owner)
            ? throw new ArgumentException("Owner cannot be the same as the subscriber.")
            : GetExternalClientSynchronizationStream<TReduced, TReference>(owner, reference);

    public ISynchronizationStream<TReduced> GetStreamFor<TReduced, TReference>(
        object subscriber,
        TReference reference
    )
        where TReference : WorkspaceReference =>
        Hub.Address.Equals(subscriber)
            ? throw new ArgumentException("Owner cannot be the same as the subscriber.")
            : GetInternalSynchronizationStream<TReduced, TReference>(reference, subscriber);


    private static readonly MethodInfo GetInternalSynchronizationStreamMethod =
        ReflectionHelper.GetMethodGeneric<Workspace>(ws =>
            ws.GetInternalSynchronizationStream<object, WorkspaceReference>(null, null));
    private ISynchronizationStream<TReduced> GetInternalSynchronizationStream<
        TReduced,
        TReference
    >(TReference reference, object subscriber)
        where TReference : WorkspaceReference =>
        ReduceManager.ReduceStream<TReduced, TReference>(this, reference, subscriber);

    public ISynchronizationStream<TReduced> GetStreamFor<TReduced>(WorkspaceReference<TReduced> reference,
        object subscriber) =>
        (ISynchronizationStream<TReduced>)GetInternalSynchronizationStreamMethod
            .MakeGenericMethod(typeof(TReduced), reference.GetType())
            .Invoke(this, [reference, subscriber]);

    private ISynchronizationStream<TReduced> GetExternalClientSynchronizationStream<
        TReduced,
        TReference
    >(object address, TReference reference)
        where TReference : WorkspaceReference =>
        (ISynchronizationStream<TReduced>)
            remoteStreams.GetOrAdd(
                (address, reference),
                _ => CreateExternalClient<TReduced, TReference>(address, reference)
            );

    private ISynchronizationStream CreateSynchronizationStream<TReduced, TReference>(
        object subscriber,
        TReference reference
    )
        where TReference : WorkspaceReference
    {
        // link to deserialized world. Will also potentially link to workspace.

        var fromWorkspace = this.ReduceInternal<TReduced, TReference>(reference, subscriber);
        var reduced =
            fromWorkspace
            ?? throw new DataSourceConfigurationException(
                $"No reducer defined for {typeof(TReference).Name} from  {typeof(TReference).Name}"
            );


        reduced.AddDisposable(
            reduced.Hub.Register<DataChangedEvent>(
                delivery =>
                {
                    reduced.DeliverMessage(delivery);
                    return delivery.Forwarded();
                },
                x => reduced.StreamId.Equals(x.Message.StreamId)
            )
        );
        reduced.AddDisposable(
            reduced.ToDataChanged()
                .Where(c => !reduced.StreamId.Equals(c.ChangedBy))
                .Subscribe(e =>
                {
                    logger.LogDebug("Owner {owner} sending change notification to subscriber {subscriber}", reduced.Owner, reduced.Subscriber);
                    Hub.Post(e, o => o.WithTarget(reduced.Subscriber));
                })
            );

        reduced.AddDisposable(
            new AnonymousDisposable(() => subscriptions.Remove(new(subscriber, reference), out _))
        );

        return reduced;
    }


    private ISynchronizationStream CreateExternalClient<TReduced, TReference>(
        object owner,
        TReference reference
    )
        where TReference : WorkspaceReference
    {
        // link to deserialized world. Will also potentially link to workspace.
        if (owner is JsonObject obj)
            owner = obj.Deserialize<object>(Hub.JsonSerializerOptions);
        var partition = reference is IPartitionedWorkspaceReference p ? p.Partition : null;

        TaskCompletionSource<TReduced> tcs2 = null;
        var reduced = new SynchronizationStream<TReduced>(
                new(owner, partition),
                Guid.NewGuid().AsString(),
                Hub,
                reference,
                ReduceManager.ReduceTo<TReduced>(),
                GetJsonConfig<TReduced>
            );
        reduced.Initialize(ct => (tcs2 = new(ct)).Task);
        reduced.AddDisposable(
            reduced.ToDataChanged()
                .Where(x => !reduced.Hub.Address.Equals(x.ChangedBy))
                .Subscribe(e =>
                {
                    logger.LogDebug("Subscriber {subscriber} sending change notification to owner {owner}",
                        reduced.Subscriber, reduced.Owner);

                    Hub.Post(e, o => o.WithTarget(reduced.Owner));
                })
        );


        var request = Hub.Post(new SubscribeRequest(reference), o => o.WithTarget(owner));
        var first = true;
        reduced.AddDisposable(
            reduced.Hub.Register<DataChangedEvent>(
                delivery =>
                {
                    if (first)
                    {
                        first = false;
                        var jsonElement = JsonDocument.Parse(delivery.Message.Change.Content).RootElement;
                        tcs2?.SetResult(jsonElement.Deserialize<TReduced>(reduced.Hub.JsonSerializerOptions));
                        return request.Processed();
                    }
                    reduced.DeliverMessage(delivery); 
                    return delivery.Forwarded();
                },
                d => reduced.StreamId.Equals(d.Message.StreamId)
            )
        );

        reduced.AddDisposable(
            new AnonymousDisposable(() => remoteStreams.Remove((reduced.Owner, reference), out _))
        );
        reduced.AddDisposable(
            new AnonymousDisposable(
                () => Hub.Post(new UnsubscribeDataRequest(reference), o => o.WithTarget(owner))
            )
        );

        //if (ret is ISynchronizationStream<EntityStore> entityStream)
        //    reduced.AddDisposable(
        //        entityStream
        //            .Where(x => reduced.Hub.Address.Equals(x.ChangedBy))
        //            .ToDataChangeRequest()

        //            .Subscribe(e =>
        //            {
        //                logger.LogDebug("Subscriber {subscriber} sending change notification to owner {owner}",
        //                    reduced.Subscriber, reduced.Owner);

        //                Hub.Post(e, o => o.WithTarget(reduced.Owner));
        //            })
        //    );



        return reduced;
    }

    private ISynchronizationStream<JsonElement> GetJsonStream<TReduced, TReference>(object subscriber, TReference reference, object partition)
        where TReference : WorkspaceReference
    {
        ISynchronizationStream<JsonElement> json;
        json = new SynchronizationStream<JsonElement>(
            new(subscriber, partition),
            Guid.NewGuid().AsString(),
            Hub,
            reference,
            ReduceManager.ReduceTo<JsonElement>(),
            GetJsonConfig
        );
        return json;
    }

    private static  SynchronizationStream<TStream>.StreamConfiguration GetJsonConfig<TStream>(
        SynchronizationStream<TStream>.StreamConfiguration stream) =>
        stream.ConfigureHub(config =>
            config.WithHandler<DataChangedEvent>(
                (hub, delivery) =>
                {
                    var currentJson = hub.Configuration.Get<JsonElement?>();
                    (currentJson, var patch) = delivery.Message.UpdateJsonElement(currentJson, hub.JsonSerializerOptions);
                    hub.Configuration.Set(currentJson);
                    stream.Stream.Update(
                        state =>
                        stream.Stream.ToChangeItem(state,
                            currentJson.Value,
                            patch)
                    );
                    return delivery.Processed();
                }
            )
    );

    public void Update(IReadOnlyCollection<object> instances, UpdateOptions updateOptions, Activity activity) =>
        RequestChange(
            new DataChangeRequest()
            {
                Updates = instances.ToImmutableList(),
                Options = updateOptions,
                ChangedBy = Hub.Address
            }, activity
        );



    public void Delete(IReadOnlyCollection<object> instances, Activity activity) =>
        RequestChange(
            new DataChangeRequest { Deletions = instances.ToImmutableList(), ChangedBy = Hub.Address }, activity
        );

    public ISynchronizationStream<TReduced> GetStream<TReduced>(WorkspaceReference<TReduced> reference, object subscriber)
    {
        return (ISynchronizationStream<TReduced>)ReduceInternalMethod
            .MakeGenericMethod(typeof(TReduced), reference.GetType())
            .InvokeAsFunction(this, reference, subscriber);
    }


    private static readonly MethodInfo ReduceInternalMethod =
        ReflectionHelper.GetMethodGeneric<Workspace>(x =>
            x.ReduceInternal<object, WorkspaceReference<object>>(null, null));
    private ISynchronizationStream<TReduced> ReduceInternal<TReduced, TReference>(TReference reference, object subscriber)
    where TReference : WorkspaceReference
    {
        return ReduceManager.ReduceStream<TReduced, TReference>(
            this,
            reference,
            subscriber
        );
    }

    public ISynchronizationStream<EntityStore> GetStream(params Type[] types)
        => ReduceInternal<EntityStore, CollectionsReference>(new CollectionsReference(types
            .Select(t =>
                DataContext.TypeRegistry.TryGetCollectionName(t, out var name)
                    ? name
                    : throw new ArgumentException($"Type {t.FullName} is unknown.")
            )
            .ToArray()
        ), null);

    public ReduceManager<EntityStore> ReduceManager => DataContext.ReduceManager;

    public IMessageHub Hub { get; }
    public object Id => Hub.Address;


    public DataContext DataContext { get; }

    public void RequestChange(DataChangeRequest change, Activity activity)
    {
        this.Change(change, activity);
    }

    private bool isDisposing;

    public async ValueTask DisposeAsync()
    {
        if (isDisposing)
            return;
        isDisposing = true;
        while (asyncDisposables.TryTake(out var d))
            await d.DisposeAsync();

        while (disposables.TryTake(out var d))
            d.Dispose();

        foreach (var subscription in remoteStreams.Values.Concat(subscriptions.Values))
            await subscription.DisposeAsync();


        await DataContext.DisposeAsync();
    }
    private readonly ConcurrentBag<IDisposable> disposables = new();
    private readonly ConcurrentBag<IAsyncDisposable> asyncDisposables = new();

    public void AddDisposable(IDisposable disposable)
    {
        disposables.Add(disposable);
    }

    public void AddDisposable(IAsyncDisposable disposable)
    {
        asyncDisposables.Add(disposable);
    }

    public ISynchronizationStream<EntityStore> GetStream(StreamIdentity identity)
    {
        var ds = DataContext.GetDataSource(identity.Owner);
        return ds.GetStream(identity.Partition);
    }


    protected IMessageDelivery HandleCommitResponse(IMessageDelivery<DataChangeResponse> response)
    {
        if (response.Message.Status == DataChangeStatus.Committed)
            return response.Processed();
        // TODO V10: Here we have to put logic to revert the state if commit has failed. (26.02.2024, Roland Bürgi)
        return response.Ignored();
    }

    void IWorkspace.SubscribeToClient<TReduced>(
        object address,
        WorkspaceReference<TReduced> reference
    ) =>
        s_subscribeToClientMethod
            .MakeGenericMethod(typeof(TReduced), reference.GetType())
            .Invoke(this, [address, reference]);


    private static readonly MethodInfo s_subscribeToClientMethod =
        ReflectionHelper.GetMethodGeneric<Workspace>(x =>
            x.SubscribeToClient<object, WorkspaceReference<object>>(default, default)
        );

    private void SubscribeToClient<TReduced, TReference>(object address, TReference reference)
        where TReference : WorkspaceReference<TReduced>
    {
        subscriptions.GetOrAdd(
            new(address, reference),
            _ => CreateSynchronizationStream<TReduced, TReference>(address, reference)
        );
    }

    public async Task UnsubscribeAsync(object address, WorkspaceReference reference)
    {
        if (subscriptions.TryRemove(new(address, reference), out var existing))
            await existing.DisposeAsync();
    }

}
