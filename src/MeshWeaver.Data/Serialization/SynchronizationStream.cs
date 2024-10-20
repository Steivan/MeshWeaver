﻿using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Subjects;
using System.Reflection;
using MeshWeaver.Disposables;
using MeshWeaver.Messaging;
using MeshWeaver.Reflection;

namespace MeshWeaver.Data.Serialization;

public record SynchronizationStream<TStream> : ISynchronizationStream<TStream>
{
    /// <summary>
    /// The stream reference, i.e. the unique identifier of the stream.
    /// </summary>
    public StreamIdentity StreamIdentity { get; }

    /// <summary>
    /// The subscriber of the stream, e.g. the Hub Address or Id of the subscriber.
    /// </summary>
    public object Subscriber { get; init; }

    /// <summary>
    /// The owner of the stream. Changes are to be made as update request to the owner.
    /// </summary>
    public object Owner => StreamIdentity.Owner;

    /// <summary>
    /// The projected reference of the stream, e.g. a collection (CollectionReference),
    /// a layout area (LayoutAreaReference), etc.
    /// </summary>
    public object Reference { get; init; }

    /// <summary>
    /// My current state deserialized as snapshot
    /// </summary>
    private ChangeItem<TStream> current;


    /// <summary>
    /// My current state deserialized as stream
    /// </summary>
    protected readonly ReplaySubject<ChangeItem<TStream>> Store = new(1);

    object ISynchronizationStream.Reference => Reference;

    public ISynchronizationStream<TReduced> Reduce<TReduced>(
        WorkspaceReference<TReduced> reference,
        object subscriber, 
        Func<SynchronizationStream<TReduced>.StreamConfiguration, SynchronizationStream<TReduced>.StreamConfiguration> config
    ) =>
        (ISynchronizationStream<TReduced>)
            ReduceMethod
                .MakeGenericMethod(typeof(TReduced), reference.GetType())
                .Invoke(this, [reference, subscriber, config]);

    private static readonly MethodInfo ReduceMethod = ReflectionHelper.GetMethodGeneric<
        SynchronizationStream<TStream>
    >(x => x.Reduce<object, WorkspaceReference<object>>(null, null, null));

    public ISynchronizationStream<TReduced> Reduce<TReduced, TReference2>(
        TReference2 reference,
        object subscriber)
        where TReference2 : WorkspaceReference =>
        Reduce<TReduced, TReference2>(reference, subscriber, x => x);


    public ISynchronizationStream<TReduced> Reduce<TReduced>(WorkspaceReference<TReduced> reference, object subscriber)
        => Reduce(reference, subscriber, x => x);

    public ISynchronizationStream<TReduced> Reduce<TReduced, TReference2>(
        TReference2 reference,
        object subscriber,
        Func<SynchronizationStream<TReduced>.StreamConfiguration, SynchronizationStream<TReduced>.StreamConfiguration> config)
        where TReference2 : WorkspaceReference =>
        ReduceManager.ReduceStream(this, reference, subscriber, config);

    public virtual IDisposable Subscribe(IObserver<ChangeItem<TStream>> observer)
    {
        try
        {
            return Store.Subscribe(observer);
        }
        catch (ObjectDisposedException)
        {
            return new AnonymousDisposable(() => { });
        }
    }

    public readonly ConcurrentBag<IDisposable> Disposables = new();
    public readonly ConcurrentBag<IAsyncDisposable> AsyncDisposables = new();

    private bool isDisposed;
    private readonly object disposeLock = new();

    public async ValueTask DisposeAsync()
    {
        lock (disposeLock)
        {
            if (isDisposed)
                return;
            isDisposed = true;
        }

        await synchronizationStreamHub.DisposeAsync();
        foreach (var disposeAction in AsyncDisposables)
            await disposeAction.DisposeAsync();

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
        if (isDisposed || value == null)
            return;
        current = value;
        if (!isDisposed)
            Store.OnNext(value);
    }
    public void UpdateAsync(Func<TStream, ChangeItem<TStream>> update) =>
        InvokeAsync(() => SetCurrent(update.Invoke(Current is null ? default : Current.Value)));
    public void Update(Func<TStream, ChangeItem<TStream>> update) =>
        SetCurrent(update.Invoke(Current is null ? default : Current.Value));

    public void Initialize(Func<CancellationToken, Task<TStream>> init)
    {
        InvokeAsync(async ct => SetCurrent(new ChangeItem<TStream>(Owner, Reference, await init.Invoke(ct), null, ChangeType.Full, Hub.Version)));
    }

    public void Initialize(TStream startWith)
    {
        SetCurrent(new ChangeItem<TStream>(Owner, Reference, startWith, null, ChangeType.Full, Hub.Version));
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
    public void AddDisposable(IAsyncDisposable disposable) => AsyncDisposables.Add(disposable);

    IMessageDelivery ISynchronizationStream.DeliverMessage(
        IMessageDelivery delivery
    ) =>
        synchronizationStreamHub.DeliverMessage(delivery.ForwardTo(synchronizationStreamHub.Address));


    public void OnNext(ChangeItem<TStream> value)
    {
        InvokeAsync(() => SetCurrent(value));
    }

    public virtual void RequestChange(Func<TStream, ChangeItem<TStream>> update)
    {
        // TODO V10: Here we need to inject validations (29.07.2024, Roland Bürgi)
        UpdateAsync(update);
    }

    public record StreamConfiguration(ISynchronizationStream<TStream> Stream)
    {
        internal ImmutableList<Func<MessageHubConfiguration, MessageHubConfiguration>> HubConfigurations { get; init; } =
            [];
        public StreamConfiguration ConfigureHub(Func<MessageHubConfiguration, MessageHubConfiguration> configuration) =>
        this with { HubConfigurations = HubConfigurations.Add(configuration) };
    }

    public SynchronizationStream(
        StreamIdentity StreamIdentity,
        object Subscriber,
        IMessageHub Hub,
        object Reference,
        ReduceManager<TStream> ReduceManager,
        Func<StreamConfiguration, StreamConfiguration> configuration)
    {
        this.Hub = Hub;
        this.ReduceManager = ReduceManager;
        this.StreamIdentity = StreamIdentity;
        this.Subscriber = Subscriber;
        this.Reference = Reference;
        this.Configuration = configuration?.Invoke(new StreamConfiguration(this)) ?? new StreamConfiguration(this);
        synchronizationStreamHub = Hub.GetHostedHub(new SynchronizationStreamAddress(Hub.Address), config => Configuration.HubConfigurations.Aggregate(config,(c,cc) => cc.Invoke(c)));
    }

    private StreamConfiguration Configuration { get; }

    private readonly IMessageHub synchronizationStreamHub;
    public void InvokeAsync(Action action)
        => synchronizationStreamHub.InvokeAsync(action);

    public void InvokeAsync(Func<CancellationToken, Task> action)
        => synchronizationStreamHub.InvokeAsync(action);








    private record SynchronizationStreamAddress(object Host) : IHostedAddress
    {
        /// <summary>
        /// This id is not meant to be accessed.
        /// Rather, it brings uniqueness to multiple instances.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        public Guid Id { get; init; } = Guid.NewGuid();
    }



}
