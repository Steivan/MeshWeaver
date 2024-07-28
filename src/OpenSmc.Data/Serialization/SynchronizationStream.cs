﻿using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using OpenSmc.Messaging;
using OpenSmc.Reflection;

namespace OpenSmc.Data.Serialization;

public record SynchronizationStream<TStream, TReference> : ISynchronizationStream<TStream, TReference>
    where TReference : WorkspaceReference
{
    public StreamReference StreamReference { get; }

    /// <summary>
    /// Owner of the stream, e.g. the Hub Address or Id of datasource.
    /// </summary>
    public object Owner { get; init; }

    /// <summary>
    /// The subscriber of the stream, e.g. the Hub Address or Id of the subscriber.
    /// </summary>
    public object Subscriber { get; init; }

    /// <summary>
    /// The projected reference of the stream, e.g. a collection (CollectionReference),
    /// a layout area (LayoutAreaReference), etc.
    /// </summary>
    public TReference Reference { get; init; }

    /// <summary>
    /// My current state deserialized as snapshot
    /// </summary>
    private ChangeItem<TStream> current;

    /// <summary>
    /// My current state deserialized as stream
    /// </summary>
    protected readonly ReplaySubject<ChangeItem<TStream>> Store = new(1);

    private readonly ImmutableArray<(
        Func<IMessageDelivery<WorkspaceMessage>, bool> Applies,
        Func<IMessageDelivery<WorkspaceMessage>, IMessageDelivery> Process
    )> messageHandlers = ImmutableArray<(
        Func<IMessageDelivery<WorkspaceMessage>, bool> Applies,
        Func<IMessageDelivery<WorkspaceMessage>, IMessageDelivery> Process
    )>.Empty;

    private readonly TaskCompletionSource<TStream> initialized = new();

    public ISynchronizationStream<TReduced> GetClient<TReduced>(
        WorkspaceReference<TReduced> reference,
        object host
    )
    {
        throw new NotImplementedException();
    }

    public Task<TStream> Initialized => initialized.Task;

    public InitializationMode InitializationMode { get; }

    Task ISynchronizationStream.Initialized => initialized.Task;

    object ISynchronizationStream.Reference => Reference;

    public ISynchronizationStream<TReduced> Reduce<TReduced>(
        WorkspaceReference<TReduced> reference,
        object subscriber
    ) =>
        (ISynchronizationStream<TReduced>)
            ReduceMethod
                .MakeGenericMethod(typeof(TReduced), reference.GetType())
                .Invoke(this, [reference, subscriber]);

    private static readonly MethodInfo ReduceMethod = ReflectionHelper.GetMethodGeneric<
        SynchronizationStream<TStream, TReference>
    >(x => x.Reduce<object, WorkspaceReference<object>>(null, null));

    public ISynchronizationStream<TReduced, TReference2> Reduce<TReduced, TReference2>(
        TReference2 reference,
        object subscriber
    )
        where TReference2 : WorkspaceReference =>
        ReduceManager.ReduceStream<TReduced, TReference2>(this, reference, subscriber);

    public virtual IDisposable Subscribe(IObserver<ChangeItem<TStream>> observer)
    {
        return Store.Subscribe(observer);
    }

    public readonly List<IDisposable> Disposables = new();

    private bool isDisposing;
    private readonly object disposeLock = new();

    public void Dispose()
    {
        lock (disposeLock)
        {
            if (isDisposing)
                return;
            isDisposing = true;
        }
        foreach (var disposeAction in Disposables)
            disposeAction.Dispose();

        Store.Dispose();
    }

    public ChangeItem<TStream> Current
    {
        get => current;
    }

    public IMessageHub Hub { get; init; }
    public ReduceManager<TStream> ReduceManager { get; init; }

    private void SetCurrent(ChangeItem<TStream> value)
    {
        current = value;
        if(!IsInitialized)
            switch (InitializationMode)
            {
                case InitializationMode.Automatic:
                    initialized.SetResult(value.Value);
                    break;
                default:
                    return;
            }


        Store.OnNext(value);
    }

    private bool IsInitialized => initialized.Task.IsCompleted;

    public virtual void Initialize(ChangeItem<TStream> initial)
    {
        if (initial == null)
            throw new ArgumentNullException(nameof(initial));
        if (Current != null)
            throw new InvalidOperationException("Already initialized");

        current = initial;
        Store.OnNext(initial);
        initialized.SetResult(current.Value);
    }

    public void OnCompleted()
    {
        Store.OnCompleted();
    }

    public void OnError(Exception error)
    {
        Store.OnError(error);
    }

    public void AddDisposable(IDisposable disposable) => Disposables.Add(disposable);

    IMessageDelivery ISynchronizationStream.DeliverMessage(
        IMessageDelivery<WorkspaceMessage> delivery
    )
    {
        return messageHandlers
                .Where(x => x.Applies(delivery))
                .Select(x => x.Process)
                .FirstOrDefault()
                ?.Invoke(delivery) ?? delivery;
    }

    public void Update(Func<TStream, ChangeItem<TStream>> update) => updateSubject.OnNext(update);

    public void OnNext(ChangeItem<TStream> value)
    {
        incomingInstancesSubject.OnNext(value);
    }

    public virtual DataChangeResponse RequestChange(Func<TStream, ChangeItem<TStream>> update)
    {
        updateSubject.OnNext(update);
        return new DataChangeResponse(Hub.Version, DataChangeStatus.Committed, null);
    }


    public SynchronizationStream(object Owner,
        object Subscriber,
        IMessageHub Hub,
        TReference Reference,
        ReduceManager<TStream> ReduceManager,
        InitializationMode InitializationMode)
    {
        this.Hub = Hub;
        this.ReduceManager = ReduceManager;
        StreamReference = new(Owner, Reference);
        this.Owner = Owner;
        this.Subscriber = Subscriber;
        this.Reference = Reference;
        this.InitializationMode = InitializationMode;
        // Merge the updateSubject and incomingInstancesSubject
        var merged = updateSubject
            .Select(updateFunc => updateFunc.Invoke(current == null ? default : current.Value))
            .Merge(incomingInstancesSubject);

        // Subscribe to the merged observable and push the results to the combinedSubject
        AddDisposable(merged.Subscribe(SetCurrent));

    }

    private readonly Subject<ChangeItem<TStream>> incomingInstancesSubject = new();

    public SynchronizationStream()
    {
    }




    private readonly Subject<Func<TStream, ChangeItem<TStream>>> updateSubject = new();


}
