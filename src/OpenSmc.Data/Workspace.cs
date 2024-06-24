﻿using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenSmc.Activities;
using OpenSmc.Data.Serialization;
using OpenSmc.Disposables;
using OpenSmc.Messaging;
using OpenSmc.Reflection;

namespace OpenSmc.Data;

public class Workspace : IWorkspace
{
    public Workspace(IMessageHub hub, ILogger<Workspace> logger, IActivityService activityService)
    {
        Hub = hub;
        this.activityService = activityService;
        this.logger = logger;

        logger.LogDebug("Creating data context of address {address}", Id);
        DataContext = this.GetDataConfiguration();

        stream = new SynchronizationStream<WorkspaceState, WorkspaceReference>(
            Hub.Address,
            Hub.Address,
            Hub,
            new WorkspaceStateReference(),
            DataContext.ReduceManager,
            InitializationMode.Manual
        );
        logger.LogDebug("Started initialization of data context of address {address}", Id);
        DataContext.Initialize();

        DataContext.Initialized.ContinueWith(task =>
        {
            logger.LogDebug("Finished initialization of data context of address {address}", Id);
            stream.Initialize(
                new(Hub.Address, Reference, task.Result, Hub.Address, null, Hub.Version)
            );
            initialized.SetResult();
        });
    }

    public WorkspaceReference Reference { get; } = new WorkspaceStateReference();
    private ChangeItem<WorkspaceState> current;

    private ChangeItem<WorkspaceState> Current
    {
        get { return current; }
        set
        {
            current = value;
            stream.OnNext(value);
        }
    }

    private readonly ISynchronizationStream<WorkspaceState> stream;

    public IObservable<ChangeItem<WorkspaceState>> Stream => stream;

    public IReadOnlyCollection<Type> MappedTypes => Current.Value.MappedTypes.ToArray();

    private readonly ConcurrentDictionary<
        (object Subscriber, object Reference),
        IDisposable
    > subscriptions = new();
    private readonly ConcurrentDictionary<
        (object Subscriber, object Reference),
        ISynchronizationStream
    > remoteStreams = new();

    private readonly IActivityService activityService;

    public IObservable<IEnumerable<TCollection>> GetStream<TCollection>()
    {
        var collection = DataContext
            .DataSources.Select(ds => ds.Value.TypeSources.GetValueOrDefault(typeof(TCollection)))
            .FirstOrDefault(x => x != null);
        if (collection == null)
            return null;
        return GetStream(Hub.Address, new CollectionReference(collection.CollectionName))
            .Select(x => x.Value.Instances.Values.Cast<TCollection>());
    }

    public ISynchronizationStream<TReduced> GetStream<TReduced>(
        object id,
        WorkspaceReference<TReduced> reference
    ) =>
        (ISynchronizationStream<TReduced>)
            GetSynchronizationStreamMethod
                .MakeGenericMethod(typeof(TReduced), reference.GetType())
                .Invoke(this, [id, reference]);

    private static readonly MethodInfo GetSynchronizationStreamMethod =
        ReflectionHelper.GetMethodGeneric<Workspace>(x =>
            x.GetStream<object, WorkspaceReference<object>>(default, default)
        );

    public ISynchronizationStream<TReduced, TReference> GetStream<TReduced, TReference>(
        TReference reference
    )
        where TReference : WorkspaceReference =>
        GetStream<TReduced, TReference>(Hub.Address, reference);

    public ISynchronizationStream<TReduced, TReference> GetStream<TReduced, TReference>(
        object address,
        TReference reference
    )
        where TReference : WorkspaceReference =>
        Hub.Address.Equals(address)
            ? GetInternalSynchronizationStream<TReduced, TReference>(reference, address)
            : GetExternalClientSynchronizationStream<TReduced, TReference>(address, reference);

    private ISynchronizationStream<TReduced, TReference> GetInternalSynchronizationStream<
        TReduced,
        TReference
    >(TReference reference, object subscriber)
        where TReference : WorkspaceReference =>
        ReduceManager.ReduceStream<TReduced, TReference>(
            stream,
            reference,
            Hub.Address,
            subscriber
        );

    private ISynchronizationStream<TReduced, TReference> GetExternalClientSynchronizationStream<
        TReduced,
        TReference
    >(object address, TReference reference)
        where TReference : WorkspaceReference =>
        (ISynchronizationStream<TReduced, TReference>)
            remoteStreams.GetOrAdd(
                (address, reference),
                _ => CreateSynchronizationStream(address, Hub.Address, reference)
            );

    private ISynchronizationStream CreateSynchronizationStream<TReference>(
        object owner,
        object subscriber,
        TReference reference
    )
        where TReference : WorkspaceReference
    {
        // link to deserialized world. Will also potentially link to workspace.


        var ret = stream.Reduce(reference, owner, subscriber);
        var json = ret.Reduce(new JsonElementReference());

        if (subscriber.Equals(Hub.Address))
            RegisterSubscriber(reference, json);
        else
            RegisterOwner(reference, json);

        return ret;
    }

    private void RegisterOwner<TReference>(
        TReference reference,
        ISynchronizationStream<JsonElement> json
    )
        where TReference : WorkspaceReference
    {
        var subscriber = json.Subscriber;
        json.AddDisposable(
            json.Hub.Register<DataChangedEvent>(
                delivery =>
                {
                    var response = json.RequestChangeFromJson(
                        delivery.Message with
                        {
                            ChangedBy = delivery.Sender
                        }
                    );
                    json.Hub.Post(response, o => o.ResponseFor(delivery));
                    return delivery.Processed();
                },
                x =>
                    json.Owner.Equals(x.Message.Owner) && x.Message.Reference.Equals(json.Reference)
            )
        );
        json.AddDisposable(
            json.ToDataChangedStream()
                .Where(x => !json.RemoteAddress.Equals(x.ChangedBy))
                .Subscribe(e => Hub.Post(e, o => o.WithTarget(json.RemoteAddress)))
        );
        json.AddDisposable(
            new AnonymousDisposable(() => subscriptions.Remove(new(subscriber, reference), out _))
        );
    }

    private void RegisterSubscriber<TReference>(
        TReference reference,
        ISynchronizationStream<JsonElement> json
    )
        where TReference : WorkspaceReference
    {
        var owner = json.Owner;
        json.AddDisposable(
            json.Hub.Register<DataChangedEvent>(
                delivery =>
                {
                    json.NotifyChange(delivery.Message with { ChangedBy = delivery.Sender });
                    return delivery.Processed();
                },
                d =>
                    json.Owner.Equals(d.Message.Owner) && json.Reference.Equals(d.Message.Reference)
            )
        );
        json.AddDisposable(
            new AnonymousDisposable(
                () => remoteStreams.Remove((json.RemoteAddress, reference), out _)
            )
        );
        json.AddDisposable(
            new AnonymousDisposable(
                () => Hub.Post(new UnsubscribeDataRequest(reference), o => o.WithTarget(owner))
            )
        );

        json.AddDisposable(
            // this is the "client" ==> never needs to submit full state
            json.ToDataChangedStream()
                .Skip(1)
                .Subscribe(e =>
                {
                    Hub.Post(e, o => o.WithTarget(json.RemoteAddress));
                })
        );
        Hub.Post(new SubscribeRequest(reference), o => o.WithTarget(owner));
    }

    public void Update(IEnumerable<object> instances, UpdateOptions updateOptions) =>
        RequestChange(
            new UpdateDataRequest(instances.ToArray())
            {
                Options = updateOptions,
                ChangedBy = Hub.Address
            },
            Reference
        );

    public void Delete(IEnumerable<object> instances) =>
        RequestChange(
            new DeleteDataRequest(instances.ToArray()) { ChangedBy = Hub.Address },
            Reference
        );

    private readonly TaskCompletionSource initialized = new();
    public Task Initialized => initialized.Task;

    /* TODO HACK Roland Bürgi 2024-05-19: This is still unclean in the startup.
    Problem is that IWorkspace is injected in DI and DataContext is parsed only at startup.
    Need to bootstrap DataContext constructor time. */
    public ReduceManager<WorkspaceState> ReduceManager =>
        DataContext?.ReduceManager ?? DataContext.CreateReduceManager(Hub);

    public IMessageHub Hub { get; }
    public object Id => Hub.Address;
    private ILogger logger;

    WorkspaceState IWorkspace.State => Current.Value;

    public DataContext DataContext { get; private set; }

    public void Rollback()
    {
        //TODO Roland Bürgi 2024-05-06: Not sure yet how to implement
    }

    public DataChangeResponse RequestChange(DataChangedRequest change, WorkspaceReference reference)
    {
        var log = new ActivityLog(ActivityCategory.DataUpdate);
        Current = new ChangeItem<WorkspaceState>(
            Hub.Address,
            reference ?? Reference,
            Current.Value.Change(change) with
            {
                Version = Hub.Version
            },
            change.ChangedBy,
            null,
            Hub.Version
        );
        return new DataChangeResponse(Hub.Version, DataChangeStatus.Committed, log.Finish());
    }

    private bool isDisposing;

    public async ValueTask DisposeAsync()
    {
        if (isDisposing)
            return;
        isDisposing = true;

        foreach (var subscription in remoteStreams.Values.Concat(subscriptions.Values))
            subscription.Dispose();

        stream.Dispose();

        await DataContext.DisposeAsync();
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
            _ => CreateSynchronizationStream(Hub.Address, address, reference)
        );
    }

    public void Unsubscribe(object address, WorkspaceReference reference)
    {
        if (subscriptions.TryRemove(new(address, reference), out var existing))
            existing.Dispose();
    }

    public DataChangeResponse RequestChange(Func<WorkspaceState, ChangeItem<WorkspaceState>> update)
    {
        activityService.Start(ActivityCategory.DataUpdate);
        Current = update(Current.Value);
        return new DataChangeResponse(
            Hub.Version,
            DataChangeStatus.Committed,
            activityService.Finish()
        );
    }

    public void Synchronize(Func<WorkspaceState, ChangeItem<WorkspaceState>> change)
    {
        Current = change(Current.Value);
    }
}
