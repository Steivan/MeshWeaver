﻿using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using MeshWeaver.Activities;
using MeshWeaver.Data.Serialization;
using MeshWeaver.Messaging;
using Microsoft.Extensions.Logging;

namespace MeshWeaver.Data;

public static class WorkspaceOperations
{
    public static Activity Change(this IWorkspace workspace, DataChangedRequest dataChange)
    {
        if (dataChange.Elements == null)
            throw new ArgumentException($"No elements provided in the request");

        if (dataChange is UpdateDataRequest update)
            return workspace.MergeUpdate(update);

        if (dataChange is DeleteDataRequest delete)
            return workspace.MergeDelete(delete);
        throw new InvalidOperationException(
            $"No implementation for update request of type {dataChange.GetType().FullName}"
        );
    }

    public static Activity MergeUpdate(
        this IWorkspace workspace,
        UpdateDataRequest update
    )
    {
        var hub = workspace.Hub;
        var activity = GetActivity(hub);
        var (isValid, results) = workspace.ValidateUpdate(update.Elements);
        if (!isValid)
        {
            foreach (var validationResult in results.Where(r => r != ValidationResult.Success))
                activity.LogError("{members} invalid: {error}", validationResult.MemberNames,
                    validationResult.ErrorMessage);
            activity.ChangeStatus(ActivityStatus.Failed);
            return activity;
        }

        var storesByStream = 
            workspace.GroupByStream(update.Elements, activity, hub);

        foreach (var kvp in storesByStream)
        {
            var stream = workspace.GetStream(kvp.Key);
            stream.Update(s =>
            {
                var activityPart = activity.StartSubActivity(ActivityCategory.DataUpdate);
                try
                {
                    activityPart.LogInformation("Updating Data Stream {identity}", stream.StreamIdentity);
                    var ret = stream.ApplyChanges(s.MergeWithUpdates(kvp.Value, update.Options));
                    activityPart.LogInformation("Update of Data Stream {identity} succeeded.", stream.StreamIdentity);
                    activityPart.Complete();
                    return ret;
                }
                catch (Exception ex)
                {
                    activityPart.LogError("Error updating Stream {identity}: {exception}", stream.StreamIdentity,
                        ex.Message);
                    activityPart.Complete();
                    return null;
                }
            });
        }

        activity.Complete();

        return activity;

    }
    private static Activity MergeDelete(
        this IWorkspace workspace,
        DeleteDataRequest deletion
    )
    {
        var hub = workspace.Hub;
        var activity = GetActivity(hub);
        var (isValid, results) = workspace.ValidateDeletion(deletion.Elements);
        if (!isValid)
        {
            foreach (var validationResult in results.Where(r => r != ValidationResult.Success))
                activity.LogError("{members} invalid: {error}", validationResult.MemberNames,
                    validationResult.ErrorMessage);
            activity.ChangeStatus(ActivityStatus.Failed);
            return activity;
        }

        var storesByStream =
            workspace.GroupByStream(deletion.Elements, activity, hub);

        foreach (var kvp in storesByStream)
        {
            var stream = workspace.GetStream(kvp.Key);
            stream.Update(s =>
            {
                var activityPart = activity.StartSubActivity(ActivityCategory.DataUpdate);
                try
                {
                    activityPart.LogInformation("Updating Data Stream {identity}", stream.StreamIdentity);
                    var ret = stream.ApplyChanges(s.DeleteWithUpdates(kvp.Value));
                    activityPart.LogInformation("Update of Data Stream {identity} succeeded.", stream.StreamIdentity);
                    activityPart.Complete();
                    return ret;
                }
                catch (Exception ex)
                {
                    activityPart.LogError("Error updating Stream {identity}: {exception}", stream.StreamIdentity,
                        ex.Message);
                    activityPart.Complete();
                    return null;
                }
            });
        }

        activity.Complete();

        return activity;


    }


    private static ImmutableDictionary<StreamIdentity, EntityStore> GroupByStream(this IWorkspace workspace, IEnumerable<object> instances, Activity activity, IMessageHub hub)
    {
        return instances.GroupBy(e => e.GetType())
            .Select(group =>
            {
                var ts = workspace.DataContext.GetTypeSource(group.Key);
                if (ts is null)
                {
                    activity.LogWarning(
                        "Trying to update entities of type {type}, but this type is not mapped to the workspace of {address}",
                        group.Key.Name, hub.Address);
                    return default;
                }

                var ds = workspace.DataContext.DataSourcesByType.GetValueOrDefault(group.Key);
                if (ds is null)
                {
                    activity.LogWarning(
                        "Trying to update entities of type {type}, but this type is not mapped to a data source of {address}",
                        group.Key.Name, hub.Address);
                    return default;
                }

                return (TypeSource: ts,
                    Instances: new InstanceCollection(group.ToDictionary(ts.TypeDefinition.GetKey))
                    {
                        GetKey = ts.TypeDefinition.GetKey
                    }, DataSource: workspace.DataContext.GetDataSourceByType(ts.TypeDefinition.Type));
            })
            .GroupBy(x => x.DataSource)
            .Where(x => x.Key != null)
            .SelectMany(group =>
                group.SelectMany(g =>
                    g.TypeSource is not IPartitionedTypeSource partitionedTypeSource
                        ?
                        [
                            new KeyValuePair<StreamIdentity, EntityStore>(new(g.DataSource.Id, null), new())
                        ]
                        : GetPartitioned(g.Instances, g.DataSource, partitionedTypeSource)))
            .GroupBy(x => x.Key)
            .Select(x => new KeyValuePair<StreamIdentity, EntityStore>(x.Key, x.Aggregate(new EntityStore(), (s,t) => s.Merge(t.Value))))
            .ToImmutableDictionary();
    }

    private static IEnumerable<KeyValuePair<StreamIdentity, EntityStore>> GetPartitioned(
        InstanceCollection instances, IDataSource dataSource, IPartitionedTypeSource partitionedTypeSource)
    {
        return instances.Instances
            .GroupBy(i => partitionedTypeSource.GetPartition(i.Value))
            .Select(g =>
                new KeyValuePair<StreamIdentity, EntityStore>(
                    new(dataSource.Id, g.Key),
                    new()
                    {
                        Collections = ImmutableDictionary<string, InstanceCollection>.Empty.Add(
                            partitionedTypeSource.CollectionName,
                            instances with { Instances = g.ToImmutableDictionary() })
                    })
            );
    }

    private static Activity GetActivity(IMessageHub hub) =>
        new(ActivityCategory.DataUpdate, hub);

    public static EntityStore Merge(this EntityStore store, EntityStore updated) =>
        store.Merge(updated, UpdateOptions.Default);

    public static EntityStore Merge(this EntityStore store, EntityStore updated,
        Func<UpdateOptions, UpdateOptions> options) =>
        store with
        {
            Collections = store.Collections.SetItems(
                options.Invoke(new()).Snapshot
                    ? updated.Collections
                    : updated.Collections.Select(c => new KeyValuePair<string, InstanceCollection>(
                        c.Key,
                        store.Collections.GetValueOrDefault(c.Key)?.Merge(c.Value) ?? c.Value
                    ))
            )
        };

    public static EntityStore Merge(this EntityStore store, EntityStore updated, UpdateOptions options) =>
        store with
        {
            Collections = store.Collections.SetItems(
                options.Snapshot
                    ? updated.Collections
                    : updated.Collections.Select(c => new KeyValuePair<string, InstanceCollection>(
                        c.Key,
                        store.Collections.GetValueOrDefault(c.Key)?.Merge(c.Value) ?? c.Value
                    ))
            )
        };

    public static EntityStore Update(
        this EntityStore store,
        string collection,
        Func<InstanceCollection, InstanceCollection> update
    ) =>
        store.WithCollection(collection,
            update.Invoke
            (
                store.Collections.GetValueOrDefault(collection)
                ?? new InstanceCollection()
            )
        );

    public static EntityStore Update(this EntityStore store, WorkspaceReference reference, object value) =>
        store.Update(reference, value, x => x);

    public static EntityStore Update(
        this EntityStore store,
        WorkspaceReference reference,
        object value,
        Func<UpdateOptions, UpdateOptions> options
    )
    {
        return reference switch
        {
            EntityReference entityReference
                => store.Update(entityReference.Collection, c => c.Update(entityReference.Id, value)),
            CollectionReference collectionReference
                => store.Update(collectionReference.Name, _ => (InstanceCollection)value),
            CollectionsReference
                => store with { Collections = store.Collections.SetItems(((EntityStore)value).Collections) },
            PartitionedCollectionsReference partitioned
                => store.Update(partitioned.Reference, value, options),
            WorkspaceReference<EntityStore>
                => store.Merge((EntityStore)value, options),

            _
                => throw new NotSupportedException(
                    $"reducer type {reference.GetType().FullName} not supported"
                )
        };
    }

    public static IReadOnlyCollection<T> GetData<T>(this EntityStore store)
        => store.GetCollection(store.GetCollectionName?.Invoke(typeof(T)) ?? typeof(T).FullName).Get<T>().ToArray();

    public static T GetData<T>(this EntityStore store, object id)
        => (T)store.GetCollection(store.GetCollectionName?.Invoke(typeof(T)) ?? typeof(T).FullName)?.Instances
            .GetValueOrDefault(id);



    private static (bool IsValid, List<ValidationResult> Results) ValidateUpdate(
        this IWorkspace workspace,
        IReadOnlyCollection<object> instances
    )
    {
        var validationResults = new List<ValidationResult>();
        var isValid = true;
        foreach (var instance in instances)
        {

            var context = new ValidationContext(instance);
            isValid = isValid && Validator.TryValidateObject(instance, context, validationResults);
        }

        return (isValid, validationResults);
    }

    public static EntityStoreAndUpdates MergeWithUpdates(this EntityStore store, EntityStore updated, UpdateOptions options = null)
    {
        options ??= UpdateOptions.Default;
        var newStore = store.Merge(updated, options);
        return new EntityStoreAndUpdates(store,
            newStore
            .Collections.SelectMany(u =>
                store.ComputeChanges(u.Key, u.Value)));
    }

    private static (bool IsValid, List<ValidationResult> Results) ValidateDeletion(       
        this IWorkspace workspace,
        IReadOnlyCollection<object> instances)
    {
        // TODO V10: Implement proper validation logic. (14.10.2024, Roland Bürgi)
        return (true, new());
    }

}

