﻿using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Data.Persistence;
using OpenSmc.Messaging;
using OpenSmc.Reflection;
using OpenSmc.Serialization;

namespace OpenSmc.Data;
public record DataSourceUpdate(
    string Collection,
    object DataSource,
    IReadOnlyCollection<object> ToBeUpdated,
    IReadOnlyCollection<object> ToBeDeleted);

public interface ITypeSource
{
    Task InitializeAsync(CancellationToken cancellationToken);
    void Initialize(IEnumerable<EntityDescriptor> entities);
    Type ElementType { get; }
    string CollectionName { get; }
    object GetKey(object instance);
    ITypeSource WithKey(Func<object, object> key);
    IReadOnlyCollection<EntityDescriptor> GetData();
    IReadOnlyCollection<DataSourceUpdate> RequestChange(DataChangeRequest request);
    ITypeSource WithPartition<T>(Func<T, object> partition);
    ITypeSource WithPartition(Type type, Func<object, object> partition);
    object GetData(object id);
    void Update(DataSourceUpdate dataSourceUpdates);
    IEnumerable<DataSourceUpdate> Update(IEnumerable<EntityDescriptor> entities, bool snapshot = false);
    void Update(IEnumerable<DataSourceUpdate> dataSourceUpdates)
    {
        foreach (var dataSourceUpdate in dataSourceUpdates)
            Update(dataSourceUpdate);
    }

    object GetPartition(object instance);
}

public abstract record TypeSource<TTypeSource>(Type ElementType, object DataSource, string CollectionName, IMessageHub Hub) : ITypeSource
    where TTypeSource : TypeSource<TTypeSource>
{

    protected readonly ISerializationService SerializationService = Hub.ServiceProvider.GetRequiredService<ISerializationService>();
    protected ImmutableDictionary<object,ImmutableDictionary<object, object>> CurrentState { get; set; }


    ITypeSource ITypeSource.WithKey(Func<object, object> key)
        => This with { Key = key };
    public virtual object GetKey(object instance)
        => Key(instance);
    protected Func<object, object> Key { get; init; } = GetKeyFunction(ElementType);
    private static Func<object, object> GetKeyFunction(Type elementType)
    {
        var keyProperty = elementType.GetProperties().SingleOrDefault(p => p.HasAttribute<KeyAttribute>());
        if (keyProperty == null)
            keyProperty = elementType.GetProperties().SingleOrDefault(x => x.Name.ToLowerInvariant() == "id");
        if (keyProperty == null)
            return null;
        var prm = Expression.Parameter(typeof(object));
        return Expression
            .Lambda<Func<object, object>>(Expression.Convert(Expression.Property(Expression.Convert(prm, elementType), keyProperty), typeof(object)), prm)
            .Compile();
    }


    public IReadOnlyCollection<EntityDescriptor> GetData()
        => CurrentState.SelectMany(x => x.Value.Select(y => new EntityDescriptor(x.Key, CollectionName,y.Key,y.Value))).ToArray();

    
    protected TTypeSource This => (TTypeSource)this;






    public virtual Task<IReadOnlyCollection<EntityDescriptor>> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<EntityDescriptor>>(null);
    }


    public IReadOnlyCollection<DataSourceUpdate> RequestChange(DataChangeRequest request)
    {
        switch (request)
        {
            case UpdateDataRequest update:
                return Update(update.Elements.Select(ParseEntityDescriptor)).ToArray();

            case DeleteDataRequest delete:
                return Delete(delete.Elements.Select(ParseEntityDescriptor)).ToArray();

        }
        throw new ArgumentOutOfRangeException(nameof(request), request, null);
    }



    protected Func<object, object> PartitionFunction { get; init; } = _ => DataSource;
    public ITypeSource WithPartition<T>(Func<T, object> partition)
        => WithPartition(typeof(T), o => partition.Invoke((T)o));

    public ITypeSource WithPartition(Type type, Func<object, object> partition) 
        => this with { PartitionFunction = partition };

    protected EntityDescriptor ParseEntityDescriptor(object instance)
    {
        var id = GetKey(instance);
        var dataSource = GetDataSource(instance);
        return new(dataSource, CollectionName, id, instance);
    }


    public object GetData(object id) 
    {
        foreach (var instances in CurrentState.Values)
            if (instances.TryGetValue(id, out var ret))
                return ret;

        return default;
    }



    protected IEnumerable<DataSourceUpdate> Delete(IEnumerable<EntityDescriptor> entities)
    {
        foreach (var g in entities.GroupBy(e => e.DataSource))
        {
            var all = g.ToArray();
            if (!CurrentState.TryGetValue(g.Key, out var existing))
                continue;
            CurrentState = CurrentState.SetItem(g.Key, existing.RemoveRange(all.Select(a => a.Id)));
            var change = new DataSourceUpdate(CollectionName, g.Key,  Array.Empty<object>(), all.Select(a => a.Entity).ToArray());
            Update(change);
            yield return change;
        }
    }


    
    public IEnumerable<DataSourceUpdate> Update(IEnumerable<EntityDescriptor> entities, bool snapshot = false)
    {
        foreach (var g in entities.GroupBy(e => e.DataSource))
        {
            var toBeUpdated = ImmutableDictionary<object, object>.Empty;
            var entitiesByDataSource =
                CurrentState.GetValueOrDefault(g.Key) ?? ImmutableDictionary<object, object>.Empty;
            foreach (var entityDescriptor in g)
            {
                if (entitiesByDataSource.TryGetValue(entityDescriptor.Id, out var existingEntity)
                   && !existingEntity.Equals(entityDescriptor.Entity))
                    toBeUpdated = toBeUpdated.SetItem(entityDescriptor.Id, entityDescriptor.Entity);
                else
                    toBeUpdated = toBeUpdated.SetItem(entityDescriptor.Id, entityDescriptor.Entity);
            }

            var toBeDeleted = snapshot
                ? entitiesByDataSource.RemoveRange(entitiesByDataSource.Keys.Where(k => !toBeUpdated.ContainsKey(k)))
                : ImmutableDictionary<object, object>.Empty;

            CurrentState = CurrentState.SetItem(g.Key, entitiesByDataSource.RemoveRange(toBeDeleted.Keys).SetItems(toBeUpdated));
            var dataSourceUpdate = new DataSourceUpdate(CollectionName, g.Key,  toBeUpdated.Values.ToArray(),
                toBeDeleted.Values.ToArray());
            Update(dataSourceUpdate);
            yield return dataSourceUpdate;
        }


    }
    public void Update(DataSourceUpdate updates)
    {
        var toBeUpdated = new List<object>();
        var toBeAdded = new List<object>();
        foreach(var instance in updates.ToBeUpdated)
            if (!CurrentState.ContainsKey(GetKey(instance)))
                toBeAdded.Add(instance);
            else toBeUpdated.Add(instance);

        UpdateImpl(toBeUpdated);
        AddImpl(toBeAdded);
        DeleteImpl(updates.ToBeDeleted);
    }

    public object GetPartition(object instance)
        => PartitionFunction.Invoke(instance);

    protected abstract void UpdateImpl(IEnumerable<object> instances);
    protected abstract void AddImpl(IEnumerable<object> instances);
    protected abstract void DeleteImpl(IEnumerable<object> instances);


    private object GetDataSource(object instance)
    {
        return PartitionFunction.Invoke(instance);
    }


    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        CurrentState = (await GetAsync(cancellationToken))
            .GroupBy(x => x.DataSource)
            .ToImmutableDictionary(x => x.Key, x => x.ToImmutableDictionary(y => y.Id, y => y.Entity));
    }

    public void Initialize(IEnumerable<EntityDescriptor> entities)
    {
        CurrentState = entities.GroupBy(x => x.DataSource)
            .ToImmutableDictionary(x => x.Key, x => x.ToImmutableDictionary(y => y.Id, y => y.Entity));
    }
}

public record TypeSourceWithType<T>(object DataSource, IMessageHub Hub) : TypeSourceWithType<T, TypeSourceWithType<T>>(DataSource, Hub)
{
    protected override void UpdateImpl(IEnumerable<T> instances) => UpdateAction.Invoke(instances);
    protected Action<IEnumerable<T>> UpdateAction { get; init; } = _ => { };

    public TypeSourceWithType<T> WithUpdate(Action<IEnumerable<T>> update) => This with { UpdateAction = update };
    protected Action<IEnumerable<T>> AddAction { get; init; } = _ => { };


    protected override void AddImpl(IEnumerable<T> instances) => AddAction.Invoke(instances);

    public TypeSourceWithType<T> WithAdd(Action<IEnumerable<T>> add) => This with { AddAction = add };

    protected Action<IEnumerable<T>> DeleteAction { get; init; } = _ => { };
    protected override void DeleteImpl(IEnumerable<T> instances) => DeleteAction.Invoke(instances);


    public TypeSourceWithType<T> WithDelete(Action<IEnumerable<T>> delete) => This with { DeleteAction = delete };


    protected Func<CancellationToken, Task<IReadOnlyCollection<EntityDescriptor>>> GetAction { get; init; }
    public TypeSourceWithType<T> WithGet(Func<CancellationToken, Task<IReadOnlyCollection<EntityDescriptor>>> getAction) => This with { GetAction = getAction };

    public override Task<IReadOnlyCollection<EntityDescriptor>> GetAsync(CancellationToken cancellationToken)
        => GetAction?.Invoke(cancellationToken) ??
           Task.FromResult<IReadOnlyCollection<EntityDescriptor>>(Array.Empty<EntityDescriptor>());
    public virtual TypeSourceWithType<T> WithInitialData(Func<CancellationToken,Task<IEnumerable<T>>> initialData)
    {
        return this with
        {
            GetAction = async c => (await initialData(c)).Select(el => ParseEntityDescriptor(el)).ToArray()
        };
    }

}

public abstract record TypeSourceWithType<T, TTypeSource>(object DataSource, IMessageHub Hub) : TypeSource<TTypeSource>(typeof(T), DataSource, typeof(T).FullName, Hub )
where TTypeSource: TypeSourceWithType<T, TTypeSource>
{

    public TTypeSource WithKey(Func<T, object> key)
        => This with { Key = o => key.Invoke((T)o) };

    public TTypeSource WithCollectionName(string collectionName) =>
        This with { CollectionName = collectionName };

    protected override void AddImpl(IEnumerable<object> instances)
        => AddImpl(instances.Cast<T>());


    protected abstract void AddImpl(IEnumerable<T> instances);

    protected override void UpdateImpl(IEnumerable<object> instances)
    => UpdateImpl(instances.Cast<T>());

    protected abstract void UpdateImpl(IEnumerable<T> instances);

    protected override void DeleteImpl(IEnumerable<object> instances)
        => DeleteImpl(instances.Cast<T>());


    protected abstract void DeleteImpl(IEnumerable<T> instances);


    
    public TTypeSource WithPartition(Func<T, object> partition)
        => This with { PartitionFunction = o => partition.Invoke((T)o) };







}