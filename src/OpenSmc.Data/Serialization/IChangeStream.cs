﻿using OpenSmc.Messaging;

namespace OpenSmc.Data.Serialization;

public interface IChangeStream : IDisposable
{
    object Id { get; }
    object Reference { get; }

    internal IMessageDelivery DeliverMessage(IMessageDelivery<WorkspaceMessage> delivery);
    void AddDisposable(IDisposable disposable);

    Task Initialized { get; }

    IMessageHub Hub { get; }
    public void Post(WorkspaceMessage message) =>
        Hub.Post(message with { Id = Id, Reference = Reference }, o => o.WithTarget(Id));
}

public interface IChangeStream<TStream>
    : IChangeStream,
        IObservable<ChangeItem<TStream>>,
        IObserver<ChangeItem<TStream>>
{
    void Update(Func<TStream, ChangeItem<TStream>> update);
    void Initialize(TStream value);
    IObservable<IChangeItem> Reduce(WorkspaceReference reference) => Reduce((dynamic)reference);

    IChangeStream<TReduced> Reduce<TReduced>(WorkspaceReference<TReduced> reference);

    new Task<TStream> Initialized { get; }

    ReduceManager<TStream> ReduceManager { get; }
}

public interface IChangeStream<TStream, out TReference> : IChangeStream<TStream>
{
    new TReference Reference { get; }
}
