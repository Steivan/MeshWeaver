﻿using System;
using System.Collections.Immutable;
using System.Reflection;
using OpenSmc.Data.Persistence;
using OpenSmc.Messaging;
using OpenSmc.Reflection;

namespace OpenSmc.Data;

public class DataPlugin : MessageHubPlugin<DataPluginState>, 
    IWorkspace,
    IMessageHandler<UpdateDataRequest>,
    IMessageHandler<DeleteDataRequest>
{
    private readonly IMessageHub persistenceHub;
    private readonly TaskCompletionSource initialize = new();

    public DataContext DataContext { get; }
    public Task Initializing => initialize.Task;

    public DataPlugin(IMessageHub hub) : base(hub)
    {
        DataContext = hub.GetDataConfiguration();
        Register(HandleGetRequest); // This takes care of all Read (CRUD)
        persistenceHub = hub.GetHostedHub(new PersistenceAddress(hub.Address), conf => conf.AddPlugin(h => new DataPersistencePlugin(h, DataContext)));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)  // This loads the persisted state
    {
        await base.StartAsync(cancellationToken);

        var response = await persistenceHub.AwaitResponse(new GetDataStateRequest(), cancellationToken);
        InitializeState(new (response.Message));
        initialize.SetResult();
        await DataContext.InitializationAsync(Hub, State.Current, cancellationToken);
    }

    IMessageDelivery IMessageHandler<UpdateDataRequest>.HandleMessage(IMessageDelivery<UpdateDataRequest> request)
    {
        UpdateImpl(request.Message.Elements, request.Message.Options);
        Commit();
        Hub.Post(new DataChangedEvent(Hub.Version), o => o.ResponseFor(request));
        return request.Processed();
    }

    private void UpdateImpl(IReadOnlyCollection<object> items, UpdateOptions options)
    {
        UpdateState(s =>
            s with
            {
                Current = s.Current.Modify(items, (ws, i) => ws.Update(i, options?.SnapshotModeEnabled ?? false)),
                UncommittedEvents = s.UncommittedEvents.Add(new UpdateDataRequest(items) { Options = options })
            }
        ); // update the state in memory (workspace)
    }

    IMessageDelivery IMessageHandler<DeleteDataRequest>.HandleMessage(IMessageDelivery<DeleteDataRequest> request)
    {
        DeleteImpl(request.Message.Elements);
        Commit();
        Hub.Post(new DataChangedEvent(Hub.Version), o => o.ResponseFor(request));
        return request.Processed();

    }

    private void DeleteImpl(IReadOnlyCollection<object> items)
    {
        UpdateState(s =>
            s with
            {
                Current = s.Current.Modify(items, (ws, i) => ws.Delete(i)),
                UncommittedEvents = s.UncommittedEvents.Add(new DeleteDataRequest(items))
            }
            );

    }

    private IMessageDelivery HandleGetRequest(IMessageDelivery request)
    {
        var type = request.Message.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(GetManyRequest<>))
        {
            var elementType = type.GetGenericArguments().First();
            return (IMessageDelivery)GetElementsMethod.MakeGenericMethod(elementType).InvokeAsFunction(this, request);
        }
        else if (type == typeof(GetManyRequest))
        {
            var message = request.Message as GetManyRequest;
            var dict = message.Types.ToImmutableDictionary(type => type, type => (IReadOnlyCollection<object>)GetItemsMethod.MakeGenericMethod(type).InvokeAsFunction(State.Current));
            Hub.Post(new GetManyResponse(dict), o => o.ResponseFor(request));
            return request.Processed();
        }
        return request;
    }

    private static readonly MethodInfo GetElementsMethod = ReflectionHelper.GetMethodGeneric<DataPlugin>(x => x.GetElements<object>(null));

    private static readonly MethodInfo GetItemsMethod = ReflectionHelper.GetMethodGeneric<CombinedWorkspaceState>(x => x.GetItems<object>());

    // ReSharper disable once UnusedMethodReturnValue.Local
    private IMessageDelivery GetElements<T>(IMessageDelivery<GetManyRequest<T>> request) where T : class
    {
        var items = State.Current.GetItems<T>();
        var message = request.Message;
        var queryResult = items;
        if (message.PageSize is not null)
            queryResult = queryResult.Skip(message.Page * message.PageSize.Value).Take(message.PageSize.Value).ToArray();
        var response = new GetManyResponse<T>(items.Count, queryResult);
        Hub.Post(response, o => o.ResponseFor(request));
        return request.Processed();
    }

    public override bool IsDeferred(IMessageDelivery delivery)
        => base.IsDeferred(delivery) || delivery.Message.GetType().IsGetRequest();

    public void Update(IReadOnlyCollection<object> instances, UpdateOptions options)
    {
        UpdateImpl(instances, options);
    }

    public void Delete(IReadOnlyCollection<object> instances)
    {
        DeleteImpl(instances);

    }

    public void Commit()
    {
        if (State.UncommittedEvents.Count == 0)
            return;
        persistenceHub.Post(new UpdateDataStateRequest(State.UncommittedEvents));
        Hub.Post(new DataChangedEvent(Hub.Version), o => o.WithTarget(MessageTargets.Subscribers));
        UpdateState(s => s with {UncommittedEvents = ImmutableList<DataChangeRequest>.Empty});
    }

    public void Rollback()
    {
        UpdateState(s => s with
        {
            Current = s.PreviouslySaved,
            UncommittedEvents = ImmutableList<DataChangeRequest>.Empty
        });
    }

    public IReadOnlyCollection<T> GetData<T>() where T : class
    {
        return State.Current.GetItems<T>();
    }
}

