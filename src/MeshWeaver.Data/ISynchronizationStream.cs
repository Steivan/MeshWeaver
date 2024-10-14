﻿using MeshWeaver.Messaging;

namespace MeshWeaver.Data;

public interface ISynchronizationStream : IDisposable
{
    object Owner { get; }
    object Reference { get; }
    object Subscriber { get; }
    StreamIdentity StreamIdentity { get; }
    internal IMessageDelivery DeliverMessage(IMessageDelivery<WorkspaceMessage> delivery);
    void AddDisposable(IDisposable disposable);

    ISynchronizationStream Reduce(WorkspaceReference reference, object subscriber = null) =>
        Reduce((dynamic)reference, subscriber);
    ISynchronizationStream<TReduced> Reduce<TReduced>(
        WorkspaceReference<TReduced> reference,
        object subscriber
    );

    ISynchronizationStream<TReduced> Reduce<TReduced, TReference2>(
        TReference2 reference,
        object subscriber
    )
        where TReference2 : WorkspaceReference;

    IMessageHub Hub { get; }

    public void Post(WorkspaceMessage message) =>
        Hub.Post(message with { Owner = Owner, Reference = Reference }, o => o.WithTarget(Owner));
}

public interface ISynchronizationStream<TStream>
    : ISynchronizationStream,
        IObservable<ChangeItem<TStream>>,
        IObserver<ChangeItem<TStream>>
{
    ChangeItem<TStream> Current { get; }
    void Update(Func<TStream, ChangeItem<TStream>> update);
    void InitializeAsync(Func<CancellationToken,Task<TStream>> update);

    ReduceManager<TStream> ReduceManager { get; }
    void RequestChange(Func<TStream, ChangeItem<TStream>> update);
    void InvokeAsync(Action action);
    void InvokeAsync(Func<CancellationToken, Task> action);
}


public enum InitializationMode
{
    Automatic,
    Manual
}