﻿using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Data.Persistence;
using OpenSmc.Reflection;
using OpenSmc.Serialization;

namespace OpenSmc.Data;

public interface ITypeSource : IDisposable
{
    Task<ImmutableDictionary<object, object>> InitializeAsync(CancellationToken cancellationToken);
    Type ElementType { get; }
    string CollectionName { get; }
    object GetKey(object instance);
    ITypeSource WithKey(Func<object, object> key);
    ITypeSource WithPartition<T>(Func<T, object> partition);
    ITypeSource WithPartition(Type type, Func<object, object> partition);
    ITypeSource WithInitialData(Func<CancellationToken, Task<IEnumerable<object>>> loadInstancesAsync);

    ITypeSource WithInitialData(IEnumerable<object> instances)
        => WithInitialData(() => instances);
    ITypeSource WithInitialData(Func<IEnumerable<object>> loadInstances)
        => WithInitialData(_ => Task.FromResult(loadInstances()));

    object GetPartition(object instance);
}

public abstract record TypeSource<TTypeSource> : ITypeSource
    where TTypeSource : TypeSource<TTypeSource>
{


    private readonly IDisposable subscription;

    protected TypeSource(Type ElementType, object DataSource, string CollectionName, IServiceProvider serviceProvider)
    {
        this.ElementType = ElementType;
        this.DataSource = DataSource;
        this.CollectionName = CollectionName;
        Key = GetKeyFunction(ElementType);
        PartitionFunction = _ => DataSource;
        var workspace = serviceProvider.GetRequiredService<IWorkspace>();
        subscription = workspace
            .Stream
            .Select(ws => ws.Reduce(new CollectionReference(CollectionName)))
            .DistinctUntilChanged()
            .Subscribe(UpdateImpl);

    }

    ITypeSource ITypeSource.WithKey(Func<object, object> key)
        => This with { Key = key };

    public virtual object GetKey(object instance)
        => Key(instance);

    protected Func<object, object> Key { get; init; }
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



    protected TTypeSource This => (TTypeSource)this;

    
    protected Func<object, object> PartitionFunction { get; init; }
    public ITypeSource WithPartition<T>(Func<T, object> partition)
        => WithPartition(typeof(T), o => partition.Invoke((T)o));

    public ITypeSource WithPartition(Type type, Func<object, object> partition)
        => this with { PartitionFunction = partition };

    public object GetPartition(object instance)
        => PartitionFunction.Invoke(instance);
    ITypeSource ITypeSource.WithInitialData(
        Func<CancellationToken, Task<IEnumerable<object>>> initialization)
        => WithInitialData(initialization);

    public TTypeSource WithInitialData(Func<CancellationToken, Task<IEnumerable<object>>> initialization)
        => This with { InitializationFunction = initialization };

    protected Func<CancellationToken, Task<IEnumerable<object>>> InitializationFunction { get; init; }
        = _ => Task.FromResult(Enumerable.Empty<object>());

    public Type ElementType { get; init; }
    public object DataSource { get; init; }
    public string CollectionName { get; init; }


    public virtual void UpdateImpl(InstancesInCollection instances)
    {
    }


    public virtual async Task<ImmutableDictionary<object, object>> InitializeAsync(CancellationToken cancellationToken)
    {
        var initialData = await InitializeDataAsync(cancellationToken);
        return initialData.ToImmutableDictionary(GetKey, x => x);
    }

    private Task<IEnumerable<object>> InitializeDataAsync(CancellationToken cancellationToken) 
        => InitializationFunction(cancellationToken);

    protected EntityDescriptor ParseToEntityDescriptor(object instance) 
        => new(CollectionName, GetKey(instance), instance);


    public void Dispose()
    {
        subscription.Dispose();
    }
}

public record DataChange(
    Type Type,
    IReadOnlyDictionary<object, object> Updates,
    IReadOnlyDictionary<object, object> Deletions);
public record TypeSourceWithType<T>(object DataSource, IServiceProvider ServiceProvider) : TypeSourceWithType<T, TypeSourceWithType<T>>(DataSource, ServiceProvider)
{
    public override void UpdateImpl(InstancesInCollection instances)
        => UpdateAction.Invoke(instances);

    protected Action<InstancesInCollection> UpdateAction { get; init; } = _ => { };

    public TypeSourceWithType<T> WithUpdate(Action<InstancesInCollection> update) => This with { UpdateAction = update };


    public TypeSourceWithType<T> WithInitialData(Func<CancellationToken, Task<IEnumerable<T>>> initialData)
        => WithInitialData(async  c=> (await initialData.Invoke(c)).Cast<object>());

    public TypeSourceWithType<T> WithInitialData(IEnumerable<T> initialData)
        => WithInitialData(_ => Task.FromResult(initialData.Cast<object>()));


}

public abstract record TypeSourceWithType<T, TTypeSource> : TypeSource<TTypeSource>
where TTypeSource: TypeSourceWithType<T, TTypeSource>
{
    protected TypeSourceWithType(object DataSource, IServiceProvider  serviceProvider) : base(typeof(T), DataSource, typeof(T).FullName, serviceProvider)
    {
        serviceProvider.GetRequiredService<ITypeRegistry>().WithType(typeof(T));
    }

    public TTypeSource WithKey(Func<T, object> key)
        => This with { Key = o => key.Invoke((T)o) };

    public TTypeSource WithCollectionName(string collectionName) =>
        This with { CollectionName = collectionName };

    
    public TTypeSource WithPartition(Func<T, object> partition)
        => This with { PartitionFunction = o => partition.Invoke((T)o) };


    public TTypeSource WithQuery(Func<string, T> query)
        => This with { QueryFunction = query };

    protected Func<string, T> QueryFunction { get; init; }

}