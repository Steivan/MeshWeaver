using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenSmc.Data.Serialization;
using OpenSmc.Messaging;

namespace OpenSmc.Data;

public class Workspace : IWorkspace
{
    public Workspace(IMessageHub hub)
    {
        Hub = hub;
        logger = hub.ServiceProvider.GetRequiredService<ILogger<Workspace>>();
        myChangeStream = new(Hub.Address, new WorkspaceStateReference(), Hub, ReduceManager);
    }

    public WorkspaceReference Reference { get; } = new WorkspaceStateReference();
    private WorkspaceState State => myChangeStream.Current;

    private readonly ConcurrentDictionary<string, ITypeSource> typeSources = new();

    public IObservable<ChangeItem<WorkspaceState>> Stream => myChangeStream;

    public IReadOnlyCollection<Type> MappedTypes => State.MappedTypes.ToArray();
    private readonly ConcurrentDictionary<
        (object Address, object Reference),
        IChangeStream
    > streams = new();

    private readonly ConcurrentDictionary<
        (object Address, object Reference),
        IDisposable
    > subscriptions = new();

    /// <summary>
    /// Change stream belonging to the workspace.
    /// </summary>
    private readonly ChangeStream<WorkspaceState> myChangeStream;

    private void RegisterSubscription<TReference>(
        WorkspaceReference<TReference> reference,
        object address
    )
    {
        var key = (Hub.Address, reference);
        if (subscriptions.ContainsKey(key))
            return;
        var stream = GetChangeStream(Hub.Address, reference);
        subscriptions[key] = stream.Subscribe<DataChangedEvent>(e =>
        {
            if (!address.Equals(e.ChangedBy))
                Hub.Post(e, o => o.WithTarget(address));
        });
    }

    public ChangeStream<TReference> GetChangeStream<TReference>(
        object address,
        WorkspaceReference<TReference> reference
    )
    {
        return (ChangeStream<TReference>)
            streams.GetOrAdd((address, reference), _ => CreateChangeStream(address, reference));
    }

    private IChangeStream CreateChangeStream<TReference>(
        object address,
        WorkspaceReference<TReference> reference
    )
    {
        var ret = new ChangeStream<TReference>(
            address,
            reference,
            Hub,
            ReduceManager.CreateDerived<TReference>()
        );

        // registry for internal workstream ==> I own myChangeStream, it is my basis.
        if (Hub.Address.Equals(address))
        {
            Stream.Take(1).Subscribe(x => ret.Initialize(x.Value.Reduce(reference)));
            ret.AddDisposable(
                ((IObservable<ChangeItem<EntityStore>>)ret)
                    .Skip(1)
                    .Subscribe(x =>
                    {
                        if (!Hub.Address.Equals(x.ChangedBy))
                        {
                            Synchronize(x);
                        }
                    })
            );
        }
        // registry for external workstreams. DataChanged and Update are fed directly to the stream.
        else
        {
            Hub.Post(new SubscribeRequest(reference), o => o.WithTarget(address));

            ret.Disposables.Add(
                new Disposables.AnonymousDisposable(
                    () =>
                        Hub.Post(new UnsubscribeDataRequest(reference), o => o.WithTarget(address))
                )
            );
            ret.Disposables.Add(
                ret.Subscribe<PatchChangeRequest>(e => Hub.Post(e, o => o.WithTarget(address)))
            );
        }
        return ret;
    }

    public void Update(IEnumerable<object> instances, UpdateOptions updateOptions) =>
        RequestChange(
            new UpdateDataRequest(instances.ToArray()) { Options = updateOptions },
            Hub.Address,
            Reference
        );

    public void Delete(IEnumerable<object> instances) =>
        RequestChange(new DeleteDataRequest(instances.ToArray()), Hub.Address, Reference);

    private readonly TaskCompletionSource initializeTaskCompletionSource = new();
    public Task Initialized => initializeTaskCompletionSource.Task;

    public ReduceManager<WorkspaceState> ReduceManager { get; } = CreateReduceManager();

    private static ReduceManager<WorkspaceState> CreateReduceManager()
    {
        return new ReduceManager<WorkspaceState>()
            .AddWorkspaceReference<EntityReference, object>(
                (ws, reference) => ws.Store.ReduceImpl(reference)
            )
            .AddWorkspaceReference<PartitionedCollectionsReference, EntityStore>(
                (ws, reference) => ws.ReduceImpl(reference)
            )
            .AddWorkspaceReference<CollectionReference, InstanceCollection>(
                (ws, reference) => ws.Store.ReduceImpl(reference)
            )
            .AddWorkspaceReference<CollectionsReference, EntityStore>(
                (ws, reference) => ws.Store.ReduceImpl(reference)
            )
            .AddWorkspaceReference<WorkspaceStoreReference, EntityStore>(
                (ws, reference) => ws.Store
            )
            .AddWorkspaceReference<WorkspaceStateReference, WorkspaceState>((ws, reference) => ws);
    }

    public IMessageHub Hub { get; }
    public object Id => Hub.Address;
    private ILogger logger;

    WorkspaceState IWorkspace.State => State;

    public DataContext DataContext { get; private set; }

    public void Initialize() // This loads the persisted state
    {
        DataContext = Hub.GetDataConfiguration(ReduceManager);

        Initialize(DataContext);
    }

    private void Initialize(DataContext dataContext)
    {
        logger.LogDebug($"Starting data plugin at address {Id}");
        var dataContextStreams = dataContext.Initialize().ToArray();

        logger.LogDebug("Initialized workspace in address {address}", Id);

        foreach (var ts in DataContext.DataSources.Values.SelectMany(ds => ds.TypeSources))
            typeSources[ts.CollectionName] = ts;

        var initializeObserver = new InitializeObserver(
            dataContextStreams.ToDictionary(x => x.Id),
            store =>
            {
                myChangeStream.Initialize(CreateState(store));

                initializeTaskCompletionSource.SetResult();
            },
            dataContext.InitializationTimeout
        );

        foreach (var stream in dataContextStreams)
        {
            streams[(stream.Id, stream.Reference)] = stream;
            stream.Disposables.Add(
                ((IObservable<ChangeItem<EntityStore>>)stream)
                    .Skip(1)
                    .Where(x => !x.ChangedBy.Equals(Id) || !Reference.Equals(x.Reference))
                    .Subscribe<ChangeItem<EntityStore>>(Synchronize)
            );
            initializeObserver.Disposables.Add(stream.Subscribe(initializeObserver));
        }
    }

    public WorkspaceState CreateState(EntityStore entityStore)
    {
        return new(Hub, entityStore, typeSources, ReduceManager);
    }

    public void Synchronize(ChangeItem<EntityStore> item)
    {
        if (item.Address.Equals(Hub.Address) && item.Reference.Equals(Reference))
            return;
        myChangeStream.Synchronize(item.SetValue(State with { Store = item.Value }));
    }

    public void Rollback()
    {
        //TODO Roland Bürgi 2024-05-06: Not sure yet how to implement
    }

    public void RequestChange(
        DataChangeRequest change,
        object changedBy,
        WorkspaceReference reference
    )
    {
        myChangeStream.Update(state => new ChangeItem<WorkspaceState>(
            Hub.Address,
            reference ?? Reference,
            state.Change(change) with
            {
                Version = Hub.Version
            },
            changedBy,
            Hub.Version
        ));
    }

    private bool isDisposing;

    public async ValueTask DisposeAsync()
    {
        if (isDisposing)
            return;
        isDisposing = true;

        foreach (var subscription in subscriptions.Values)
            subscription.Dispose();

        foreach (var stream in streams.Values)
            stream.Dispose();

        myChangeStream.Dispose();

        await DataContext.DisposeAsync();
    }

    protected IMessageDelivery HandleCommitResponse(IMessageDelivery<DataChangeResponse> response)
    {
        if (response.Message.Status == DataChangeStatus.Committed)
            return response.Processed();
        // TODO V10: Here we have to put logic to revert the state if commit has failed. (26.02.2024, Roland Bürgi)
        return response.Ignored();
    }

    public void Subscribe(object address, WorkspaceReference reference)
    {
        RegisterSubscription((dynamic)reference, address);
    }

    public void Unsubscribe(object address, WorkspaceReference reference)
    {
        if (subscriptions.TryRemove((address, reference), out var existing))
            existing.Dispose();
    }

    public IMessageDelivery DeliverMessage(IMessageDelivery<IWorkspaceMessage> delivery)
    {
        if (
            streams.TryGetValue(
                (delivery.Message.Address, delivery.Message.Reference),
                out var stream
            )
        )
            return stream.DeliverMessage(delivery);

        return delivery.Ignored();
    }

    public void Update(ChangeItem<EntityStore> changeItem) =>
        myChangeStream.Update(s => changeItem.SetValue(s with { Store = changeItem.Value }));
}
