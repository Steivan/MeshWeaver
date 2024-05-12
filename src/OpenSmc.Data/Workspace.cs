using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection.Metadata.Ecma335;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenSmc.Activities;
using OpenSmc.Data.Serialization;
using OpenSmc.Messaging;

namespace OpenSmc.Data;

public class Workspace : IWorkspace
{
    private readonly ReplaySubject<ChangeItem<WorkspaceState>> state = new();

    public Workspace(IMessageHub hub, ILogger<Workspace> logger, IActivityService activityService)
    {
        Hub = hub;
        this.activityService = activityService;
        this.logger = logger;
        ReduceManager = CreateReduceManager();
        myChangeStream = new(
            Hub.Address,
            new WorkspaceStateReference(),
            state,
            Hub,
            ReduceManager,
            null
        );
    }

    public WorkspaceReference Reference { get; } = new WorkspaceStateReference();
    private WorkspaceState State => myChangeStream.Current;

    private readonly ConcurrentDictionary<string, ITypeSource> typeSources = new();

    public IObservable<ChangeItem<WorkspaceState>> Stream => myChangeStream;

    public IReadOnlyCollection<Type> MappedTypes => State.MappedTypes.ToArray();

    private record AddressAndReference(object Address, object Reference);

    private ConcurrentDictionary<WorkspaceReference, IChangeStream> streams = new();
    private ConcurrentDictionary<AddressAndReference, IChangeStream> externalStreams = new();

    /// <summary>
    /// Change stream belonging to the workspace.
    /// </summary>
    private readonly ChangeStream<WorkspaceState, WorkspaceState> myChangeStream;
    private readonly IActivityService activityService;

    public IChangeStream<TReduced, WorkspaceState> GetExternalChangeStream<TReduced>(
        object address,
        WorkspaceReference<TReduced> reference,
        Func<TReduced, WorkspaceState> backfeed
    ) =>
        (IChangeStream<TReduced, WorkspaceState>)
            externalStreams.GetOrAdd(
                new(address, reference),
                _ => CreateExternalChangeStream(address, reference, backfeed)
            );

    public IChangeStream<TReduced, WorkspaceState> GetChangeStream<TReduced>(
        WorkspaceReference<TReduced> reference,
        Func<TReduced, WorkspaceState> backfeed
    ) =>
        (IChangeStream<TReduced, WorkspaceState>)
            streams.GetOrAdd(reference, _ => CreateChangeStream(reference, backfeed));

    private IChangeStream CreateChangeStream<TReduced>(
        WorkspaceReference<TReduced> reference,
        Func<TReduced, WorkspaceState> backfeed
    )
    {
        var ret = ReduceManager.ReduceStream(myChangeStream, reference);
        var changeStream = new ChangeStream<TReduced, WorkspaceState>(
            Hub.Address,
            reference,
            ret,
            Hub,
            ReduceManager.CreateDerived<TReduced>(),
            backfeed
        );

        return changeStream;
    }

    private IChangeStream CreateExternalChangeStream<TReduced>(
        object address,
        WorkspaceReference<TReduced> reference,
        Func<TReduced, WorkspaceState> backfeed
    )
    {
        var stream = GetChangeStream(reference, backfeed);

        stream.AddDisposable(
            new Disposables.AnonymousDisposable(
                () => Hub.Post(new UnsubscribeDataRequest(reference), o => o.WithTarget(address))
            )
        );

        stream.AddDisposable(
            stream.Subscribe<PatchChangeRequest>(e => Hub.Post(e, o => o.WithTarget(address)))
        );
        stream.AddDisposable(
            stream.Subscribe<DataChangedEvent>(e => Hub.Post(e, o => o.WithTarget(address)))
        );

        stream.AddDisposable(stream.Synchronization.Subscribe(x => RequestChange(x)));

        Hub.Post(new SubscribeRequest(reference), o => o.WithTarget(address));

        return stream;
    }

    public void Update(IEnumerable<object> instances, UpdateOptions updateOptions) =>
        RequestChange(
            new UpdateDataRequest(instances.ToArray()) { Options = updateOptions },
            Hub.Address,
            Reference
        );

    public void Delete(IEnumerable<object> instances) =>
        RequestChange(new DeleteDataRequest(instances.ToArray()), Hub.Address, Reference);

    public Task Initialized => DataContext.Initialized;

    public ReduceManager<WorkspaceState> ReduceManager { get; }

    private ReduceManager<WorkspaceState> CreateReduceManager()
    {
        return new ReduceManager<WorkspaceState>()
            .AddWorkspaceReference<EntityReference, object>(
                (ws, reference) => ws.Store.ReduceImpl(reference),
                null
            )
            .AddWorkspaceReference<PartitionedCollectionsReference, EntityStore>(
                (ws, reference) => ws.ReduceImpl(reference),
                CreateState
            )
            .AddWorkspaceReference<CollectionReference, InstanceCollection>(
                (ws, reference) => ws.Store.ReduceImpl(reference),
                null
            )
            .AddWorkspaceReference<CollectionsReference, EntityStore>(
                (ws, reference) => ws.Store.ReduceImpl(reference),
                CreateState
            )
            .AddWorkspaceReference<WorkspaceStoreReference, EntityStore>(
                (ws, reference) => ws.Store,
                CreateState
            )
            .AddWorkspaceReference<WorkspaceStateReference, WorkspaceState>(
                (ws, reference) => ws,
                ws => ws
            );
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
        dataContext.Initialize();

        logger.LogDebug("Initialized workspace in address {address}", Id);

        foreach (var ts in DataContext.DataSources.Values.SelectMany(ds => ds.TypeSources))
            typeSources[ts.CollectionName] = ts;
    }

    public WorkspaceState CreateState(EntityStore entityStore)
    {
        return new(Hub, entityStore, typeSources, ReduceManager);
    }

    public void Rollback()
    {
        //TODO Roland Bürgi 2024-05-06: Not sure yet how to implement
    }

    public DataChangeResponse RequestChange(
        DataChangeRequest change,
        object changedBy,
        WorkspaceReference reference
    )
    {
        var log = new ActivityLog(ActivityCategory.DataUpdate);
        myChangeStream.Synchronize(state => new ChangeItem<WorkspaceState>(
            Hub.Address,
            reference ?? Reference,
            state.Change(change) with
            {
                Version = Hub.Version
            },
            changedBy,
            Hub.Version
        ));
        return new DataChangeResponse(Hub.Version, DataChangeStatus.Committed, log.Finish());
    }

    private bool isDisposing;

    public async ValueTask DisposeAsync()
    {
        if (isDisposing)
            return;
        isDisposing = true;

        foreach (var subscription in externalStreams.Values)
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

    public IChangeStream<TReduced, WorkspaceState> Subscribe<TReduced>(
        object address,
        WorkspaceReference<TReduced> reference,
        Func<TReduced, WorkspaceState> backfeed
    )
    {
        var stream = GetExternalChangeStream(address, reference, backfeed);
        externalStreams.TryAdd(new(address, reference), stream);
        return stream;
    }

    public void Unsubscribe(object address, WorkspaceReference reference)
    {
        if (externalStreams.TryRemove(new(address, reference), out var existing))
            existing.Dispose();
    }

    public IMessageDelivery DeliverMessage(IMessageDelivery<IWorkspaceMessage> delivery)
    {
        if (
            externalStreams.TryGetValue(
                new(delivery.Sender, delivery.Message.Reference),
                out var stream
            )
        )
            return stream.DeliverMessage(delivery);

        return delivery.Ignored();
    }

    public DataChangeResponse RequestChange(ChangeItem<WorkspaceState> changeItem)
    {
        myChangeStream.Synchronize(s => Update(s, changeItem));
        return new DataChangeResponse(Hub.Version, DataChangeStatus.Committed, changeItem.Log);
    }

    public void Synchronize(ChangeItem<WorkspaceState> changeItem)
    {
        myChangeStream.Synchronize(s => GetUpdatedState(s, changeItem));
    }

    private ChangeItem<WorkspaceState> Update(
        WorkspaceState s,
        ChangeItem<WorkspaceState> changeItem
    )
    {
        activityService.Start(ActivityCategory.DataUpdate);
        activityService.LogInformation(
            "Updating workspace state from resource {changedBy}",
            changeItem.ChangedBy
        );
        var newElement = GetUpdatedState(s, changeItem);
        return newElement with { Log = changeItem.Log.Finish() };
    }

    private ChangeItem<WorkspaceState> GetUpdatedState(
        WorkspaceState existing,
        ChangeItem<WorkspaceState> changeItem
    )
    {
        return changeItem with
        {
            Value = existing with
            {
                Store = existing.Store.Merge(changeItem.Value.Store),
                Version = Hub.Version
            }
        };
    }
}
