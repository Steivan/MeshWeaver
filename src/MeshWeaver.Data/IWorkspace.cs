﻿using MeshWeaver.Activities;
using MeshWeaver.Data.Serialization;
using MeshWeaver.Messaging;

namespace MeshWeaver.Data;

public interface IWorkspace : IAsyncDisposable
{
    IMessageHub Hub { get; }
    DataContext DataContext { get; }
    IReadOnlyCollection<Type> MappedTypes { get; }
    void Update(IReadOnlyCollection<object> instances, Activity activity) => Update(instances, new(), activity);
    void Update(IReadOnlyCollection<object> instances, UpdateOptions updateOptions, Activity activity);
    void Update(object instance, Activity activity) => Update([instance], activity);

    void Delete(IReadOnlyCollection<object> instances, Activity activity);
    void Delete(object instance, Activity activity) => Delete([instance], activity);

    public void RequestChange(DataChangeRequest change, Activity activity);

    ISynchronizationStream<EntityStore> GetStream(params Type[] types);
    ReduceManager<EntityStore> ReduceManager { get; }

    ISynchronizationStream<TReduced> GetRemoteStream<TReduced>(
        object owner,
        WorkspaceReference<TReduced> reference
    );
    ISynchronizationStream<TReduced> GetStream<TReduced>(
        WorkspaceReference<TReduced> reference,  
        Func<StreamConfiguration<TReduced>, StreamConfiguration<TReduced>> configuration = null);

    IObservable<IReadOnlyCollection<T>> GetStream<T>();

    ISynchronizationStream<TReduced> GetRemoteStream<TReduced, TReference>(
        object address,
        TReference reference
    )
        where TReference : WorkspaceReference;



    internal void SubscribeToClient(
        SubscribeRequest request
    );

    void AddDisposable(IDisposable disposable);
    void AddDisposable(IAsyncDisposable disposable);
    ISynchronizationStream<EntityStore> GetStream(StreamIdentity kvpKey);
}
