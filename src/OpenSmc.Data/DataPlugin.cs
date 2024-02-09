﻿using Microsoft.Extensions.DependencyInjection;
using OpenSmc.DataSource.Abstractions;
using OpenSmc.Messaging;
using OpenSmc.Reflection;

namespace OpenSmc.Data;

public static class DataPluginExtensions
{
    public static MessageHubConfiguration AddData(this MessageHubConfiguration config, Func<DataConfiguration, DataConfiguration> dataPluginConfiguration)
    {
        var dataPluginConfig = config.Get<DataConfiguration>() ?? new();
        return config
            .WithServices(sc => sc.AddSingleton<IWorkspace, DataPlugin>())
            .Set(dataPluginConfiguration(dataPluginConfig))
            .AddPlugin(hub => (DataPlugin)hub.ServiceProvider.GetRequiredService<IWorkspace>());
    }

    internal static MessageHubConfiguration WithPersistencePlugin(this MessageHubConfiguration config, DataConfiguration dataConfiguration) => config.AddPlugin(hub => new DataPersistencePlugin(hub, dataConfiguration));
}

/* TODO List: 
 *  a) move code DataPlugin to opensmc -- done
 *  b) create an immutable variant of the workspace
 *  c) make workspace methods fully sync
 *  d) offload saves & deletes to a different hub
 *  e) configure Ifrs Hubs
 */

public class DataPlugin : MessageHubPlugin<WorkspaceState>, 
    IWorkspace,
    IMessageHandler<UpdateDataRequest>,
    IMessageHandler<DeleteDataRequest>,
    IMessageHandler<DeleteByIdRequest>
{
    private readonly DataConfiguration dataConfiguration;
    public record DataPersistencyAddress(object Host) : IHostedAddress;
    private readonly IMessageHub persistenceHub;

    public DataPlugin(IMessageHub hub) : base(hub)
    {
        var dataPersistencyAddress = new DataPersistencyAddress(Hub.Address);
        dataConfiguration = hub.Configuration.Get<DataConfiguration>() ?? new();
        Register(HandleGetRequest);              // This takes care of all Read (CRUD)
        persistenceHub = hub.GetHostedHub(dataPersistencyAddress, conf => conf.WithPersistencePlugin(dataConfiguration));
    }

    public override async Task StartAsync()  // This takes care of the Create (CRUD)
    {
        await base.StartAsync();

        var response = await persistenceHub.AwaitResponse(new GetDataStateRequest());
        InitializeState(response.Message);
    }

    IMessageDelivery IMessageHandler<UpdateDataRequest>.HandleMessage(IMessageDelivery<UpdateDataRequest> request)
    {
        return UpdateImpl(request, request.Message.Elements, request.Message.Options);
    }

    private IMessageDelivery UpdateImpl(IMessageDelivery<UpdateDataRequest> request, IReadOnlyCollection<object> items, UpdateOptions options)
    {
        UpdateState(s => s.Update(items, dataConfiguration)); // update the state in memory (workspace)
        Hub.Post(new DataChanged(items), o => o.ResponseFor(request).WithTarget(MessageTargets.Subscribers));      // notify all subscribers that the data has changed
        persistenceHub.Post(new UpdateDataRequest(items){Options = options});
        return request?.Processed();
    }

    IMessageDelivery IMessageHandler<DeleteDataRequest>.HandleMessage(IMessageDelivery<DeleteDataRequest> request)
    {
        var items = request.Message.Elements;
        return DeleteImpl(request, items);
    }

    private IMessageDelivery DeleteImpl(IMessageDelivery<DeleteDataRequest> request, IReadOnlyCollection<object> items)
    {
        UpdateState(s => s.Delete(items, dataConfiguration));
        Hub.Post(new DataDeleted(items), o => o.ResponseFor(request).WithTarget(MessageTargets.Subscribers));
        persistenceHub.Post(new DeleteDataRequest(items));
        return request?.Processed();
    }

    IMessageDelivery IMessageHandler<DeleteByIdRequest>.HandleMessage(IMessageDelivery<DeleteByIdRequest> request)
    {
        var ids = request.Message.Ids;
        var items = ids
            .Select(id =>
                State.Data.TryGetValue(id.Key, out var inner)
                    ? id.Value.Select(ii => inner.Remove(ii))
                    : Enumerable.Empty<object>()).Aggregate((x, y) => x.Concat(y)).ToArray();
        UpdateState(s => s.Delete(items, dataConfiguration));
        Hub.Post(new DataDeleted(items), o => o.ResponseFor(request).WithTarget(MessageTargets.Subscribers));
        persistenceHub.Post(new DeleteDataRequest(items));
        return request?.Processed();

    }

    private IMessageDelivery HandleGetRequest(IMessageDelivery request)
    {
        var type = request.Message.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(GetManyRequest<>))
        {
            var elementType = type.GetGenericArguments().First();
            var getElementsMethod = ReflectionHelper.GetMethodGeneric<DataPlugin>(x => x.GetElements<object>(null));
            return (IMessageDelivery)getElementsMethod.MakeGenericMethod(elementType).InvokeAsFunction(this, request);
        }
        return request;
    }

    private IMessageDelivery GetElements<T>(IMessageDelivery<GetManyRequest<T>> request) where T : class
    {
        var (items, count) = State.GetItems<T>();
        var message = request.Message;
        if (message.PageSize is not null)
            items = items.Skip(message.Page * message.PageSize.Value).Take(message.PageSize.Value);
        var queryResult = items.ToArray();
        var response = new GetManyResponse<T>(count, queryResult);
        Hub.Post(response, o => o.ResponseFor(request));
        return request.Processed();
    }

    public override bool IsDeferred(IMessageDelivery delivery)
        => delivery.Message.GetType().Namespace == typeof(GetManyRequest<>).Namespace;

    public void Update(IReadOnlyCollection<object> instances, UpdateOptions options)
    {
        UpdateImpl(null, instances, options);
    }

    public void Delete(IReadOnlyCollection<object> instances)
    {
        throw new NotImplementedException();
    }

    public void DeleteByIds(IDictionary<Type, IEnumerable<object>> instances)
    {
        throw new NotImplementedException();
    }

    public void Commit(Func<CommitOptionsBuilder, CommitOptionsBuilder> options = default)
    {
        throw new NotImplementedException();
    }

    public IQueryable<T> Query<T>()
    {
        throw new NotImplementedException();
    }
}
