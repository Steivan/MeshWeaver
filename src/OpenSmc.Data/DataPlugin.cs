﻿using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Data.Persistence;
using OpenSmc.Messaging;
using OpenSmc.Reflection;

namespace OpenSmc.Data;

public static class DataPluginExtensions
{
    public static MessageHubConfiguration AddData(this MessageHubConfiguration config, Func<DataContext, DataContext> dataPluginConfiguration)
    {
        var dataPluginConfig = config.GetListOfLambdas();
        return config
            .WithServices(sc => sc.AddSingleton<IWorkspace, DataPlugin>())
            .Set(dataPluginConfig.Add(dataPluginConfiguration))
            .AddPlugin(hub => (DataPlugin)hub.ServiceProvider.GetRequiredService<IWorkspace>());
    }

    private static ImmutableList<Func<DataContext, DataContext>> GetListOfLambdas(this MessageHubConfiguration config)
    {
        return config.Get<ImmutableList<Func<DataContext, DataContext>>>() ?? ImmutableList<Func<DataContext, DataContext>>.Empty;
    }

    internal static DataContext GetDataConfiguration(this IMessageHub hub)
    {
        var dataPluginConfig = hub.Configuration.GetListOfLambdas();
        var ret = new DataContext(hub);
        foreach (var func in dataPluginConfig)
            ret = func.Invoke(ret);
        return ret.Build();
    }

    public static async Task<IReadOnlyCollection<T>> GetAll<T>(this IMessageHub hub, object dataSourceId, CancellationToken cancellationToken) where T : class
    {
        // this is usually not to be written ==> just test code.
        var persistenceHub = hub.GetHostedHub(new PersistenceAddress(hub.Address), null);
        return (await persistenceHub.AwaitResponse(new GetManyRequest<T>(), cancellationToken)).Message.Items;
    }

    internal static bool IsGetRequest(this Type type) 
        => type.IsGenericType && GetRequestTypes.Contains(type.GetGenericTypeDefinition());

    private static readonly HashSet<Type> GetRequestTypes = [typeof(GetRequest<>), typeof(GetManyRequest<>)];

    public static TypeSource<T> WithBackingCollection<T>(this TypeSource<T> typeSource, IDictionary<object, T> backingCollection)
        => typeSource
            .WithInitialization(() => Task.FromResult((IReadOnlyCollection<T>)backingCollection.Values.ToArray()))
            .WithAdd(items =>
            {
                foreach (var i in items)
                    backingCollection.Add(typeSource.GetKey(i), i);
            })
            .WithUpdate(items =>
            {
                foreach (var i in items)
                    backingCollection[typeSource.GetKey(i)] = i;
            })
            .WithDelete(items =>
            {
                foreach (var i in items)
                    backingCollection.Remove(typeSource.GetKey(i));
            });

}

/* TODO List: 
 *  a) move code DataPlugin to opensmc -- done
 *  b) create an immutable variant of the workspace
 *  c) make workspace methods fully sync
 *  d) offload saves & deletes to a different hub
 *  e) configure Ifrs Hubs
 */

public class DataPlugin : MessageHubPlugin<DataPluginState>, 
    IWorkspace,
    IMessageHandler<UpdateDataRequest>,
    IMessageHandler<DeleteDataRequest>
{
    private readonly IMessageHub persistenceHub;

    public DataContext Context { get; }
    private readonly TaskCompletionSource initialize = new();
    public Task Initialize => initialize.Task;
    public DataPlugin(IMessageHub hub) : base(hub)
    {
        Context = hub.GetDataConfiguration();
        Register(HandleGetRequest);              // This takes care of all Read (CRUD)
        persistenceHub = hub.GetHostedHub(new PersistenceAddress(hub.Address), conf => conf.AddPlugin(h => new DataPersistencePlugin(h, Context)));
    }


    public override async Task StartAsync()  // This loads the persisted state
    {
        await base.StartAsync();

        var response = await persistenceHub.AwaitResponse(new GetDataStateRequest());
        InitializeState(new (response.Message));
        initialize.SetResult();
    }

    IMessageDelivery IMessageHandler<UpdateDataRequest>.HandleMessage(IMessageDelivery<UpdateDataRequest> request)
    {
        UpdateImpl(request.Message.Elements, request.Message.Options);
        Commit();
        Hub.Post(new DataChanged(Hub.Version), o => o.ResponseFor(request));
        return request.Processed();
    }

    private void UpdateImpl(IReadOnlyCollection<object> items, UpdateOptions options)
    {
        UpdateState(s =>
            s with
            {
                Current = s.Current.Modify(items, (ws, i) => ws.Update(i)),
                UncommittedEvents = s.UncommittedEvents.Add(new UpdateDataRequest(items) { Options = options })
            }
        ); // update the state in memory (workspace)
    }

    IMessageDelivery IMessageHandler<DeleteDataRequest>.HandleMessage(IMessageDelivery<DeleteDataRequest> request)
    {
        DeleteImpl(request.Message.Elements);
        Commit();
        Hub.Post(new DataChanged(Hub.Version), o => o.ResponseFor(request));
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
        return request;
    }

    private static readonly MethodInfo GetElementsMethod = ReflectionHelper.GetMethodGeneric<DataPlugin>(x => x.GetElements<object>(null));

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
        Hub.Post(new DataChanged(Hub.Version), o => o.WithTarget(MessageTargets.Subscribers));
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

    public IReadOnlyCollection<T> GetItems<T>() where T : class
    {
        return State.Current.GetItems<T>();
    }
}

