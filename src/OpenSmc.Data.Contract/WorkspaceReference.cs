﻿using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace OpenSmc.Data;

public abstract record WorkspaceReference;

public abstract record WorkspaceReference<TReference> : WorkspaceReference;

public record EntityStore()
{
    public ImmutableDictionary<string, InstanceCollection> Instances { get; init; } = ImmutableDictionary<string, InstanceCollection>.Empty;
    public WorkspaceReference Reference { get; init; }

    public EntityStore Merge(EntityStore s2) =>
        this with
        {
            Instances = Instances.SetItems(s2.Instances.ToImmutableDictionary(kvp => kvp.Key,
                kvp => kvp.Value.Merge(Instances.GetValueOrDefault(kvp.Key))))
        };

    public EntityStore UpdateCollection(string collection, Func<InstanceCollection, InstanceCollection> update)
        => this with
        {
            Instances = Instances.SetItem(collection, update.Invoke(Instances.GetValueOrDefault(collection) ?? new InstanceCollection()))
        };

    public object Reduce(WorkspaceReference reference)
        => ReduceImpl((dynamic)reference);

    internal object ReduceImpl(WorkspaceReference reference)
        => throw new NotSupportedException($"Reducer type {reference.GetType().FullName} not supported");

    internal object ReduceImpl(EntityReference reference) => GetCollection(reference.Collection)?.GetData(reference.Id);
    internal object ReduceImpl(EntireWorkspace _) => this;

    internal InstanceCollection ReduceImpl(CollectionReference reference) =>
        GetCollection(reference.Collection); 
    

    internal EntityStore ReduceImpl(CollectionsReference reference) =>
        this with
        {
            Reference = reference,
            Instances = reference
                .Collections
                .Select(c => new KeyValuePair<string, InstanceCollection>(c, GetCollection(c)))
                .Where(x => x.Value != null)
                .ToImmutableDictionary()

        };


    public InstanceCollection GetCollection(string collection) => Instances.GetValueOrDefault(collection);

}

public record EntireWorkspace : WorkspaceReference<EntityStore>
{
    public string Path => "$";
    public override string ToString() => Path;
}

public record JsonPathReference(string Path) : WorkspaceReference<JsonNode>
{
    public override string ToString() => $"{Path}";
}

public record InstanceReference(object Id) : WorkspaceReference<object>
{
    public virtual string Path => $"$.['{Id}']";
    public override string ToString() => Path;

}

public record EntityReference(string Collection, object Id) : InstanceReference(Id)
{
    public override string Path => $"$.['{Collection}']['{Id}']";
    public override string ToString() => Path;

}


public record CollectionReference(string Collection) : WorkspaceReference<InstanceCollection>
{
    public string Path => $"$['{Collection}']";
    public override string ToString() => Path;

}


public record CollectionsReference(IReadOnlyCollection<string> Collections)
    : WorkspaceReference<EntityStore>
{
    public string Path => $"$[{Collections.Select(c => $"'{c}'").Aggregate((x,y) => $"{x},{y}")}]";
    public override string ToString() => Path;

}

public record PartitionedCollectionsReference(WorkspaceReference<EntityStore> Collections, object Partition)
    : WorkspaceReference<EntityStore>
{
    public string Path => $"{Collections}@{Partition}";
    public override string ToString() => Path;
}

