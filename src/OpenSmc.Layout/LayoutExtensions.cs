﻿using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Patch;
using Json.Path;
using Json.Pointer;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Application.Styles;
using OpenSmc.Blazor;
using OpenSmc.Data;
using OpenSmc.Data.Persistence;
using OpenSmc.Data.Serialization;
using OpenSmc.Layout.Client;
using OpenSmc.Layout.Composition;
using OpenSmc.Messaging;
using OpenSmc.Messaging.Serialization;

namespace OpenSmc.Layout;

public static class LayoutExtensions
{
    public static MessageHubConfiguration AddLayout(
        this MessageHubConfiguration config,
        Func<LayoutDefinition, LayoutDefinition> layoutDefinition
    )
    {
        return config
            .AddData(data =>
                data.Configure(reduction =>
                    reduction
                        .AddWorkspaceReferenceStream<LayoutAreaReference, EntityStore>(
                            (stream, reference, subscriber) =>
                                new LayoutAreaHost(stream, reference, subscriber).Render(
                                    stream.Hub.GetLayoutDefinition()
                                )
                        )
                        .AddBackTransformation<EntityStore>(
                            BackTransformLayoutArea,
                            (_, reference) => reference is LayoutAreaReference
                        )
                )
            )
            .AddLayoutTypes()
            .Set(config.GetListOfLambdas().Add(layoutDefinition));
    }

    private static ChangeItem<WorkspaceState> BackTransformLayoutArea(
        WorkspaceState current,
        ISynchronizationStream<WorkspaceState> stream,
        ChangeItem<EntityStore> change
    )
    {
        // TODO V10: Must check if types are mapped in workspace and if yes write back here. (25.06.2024, Roland Bürgi)
        return change.SetValue(current);
    }

    private static LayoutDefinition GetLayoutDefinition(this IMessageHub hub) =>
        hub
            .Configuration.GetListOfLambdas()
            .Aggregate(new LayoutDefinition(hub), (x, y) => y.Invoke(x));

    internal static ImmutableList<Func<LayoutDefinition, LayoutDefinition>> GetListOfLambdas(
        this MessageHubConfiguration config
    ) =>
        config.Get<ImmutableList<Func<LayoutDefinition, LayoutDefinition>>>()
        ?? ImmutableList<Func<LayoutDefinition, LayoutDefinition>>.Empty;

    public static MessageHubConfiguration AddLayoutTypes(
        this MessageHubConfiguration configuration
    ) =>
        configuration
            .WithTypes(
                typeof(UiControl)
                    .Assembly.GetTypes()
                    .Where(t =>
                        (typeof(IUiControl).IsAssignableFrom(t) || typeof(Skin).IsAssignableFrom(t))
                        && !t.IsAbstract
                    )
            )
            .WithTypes(
                typeof(LayoutAreaReference),
                typeof(DataGridColumn<>), // this is not a control
                typeof(Option<>), // this is not a control
                typeof(Icon)
            );

    public static IObservable<object> GetControlStream(
        this ISynchronizationStream<JsonElement> synchronizationItems,
        string area
    ) =>
        synchronizationItems.Select(i =>
            JsonPointer
                .Parse(LayoutAreaReference.GetControlPointer(area))
                .Evaluate(i.Value)
                ?.Deserialize<object>(synchronizationItems.Hub.JsonSerializerOptions)
        );

    public static IObservable<object> GetControlStream(
        this ISynchronizationStream<EntityStore> synchronizationItems,
        string area
    ) =>
        synchronizationItems.Select(i =>
            i.Value.Collections.GetValueOrDefault(LayoutAreaReference.Areas)
                ?.Instances.GetValueOrDefault(area)
        );

    public static async Task<object> GetControl(
        this ISynchronizationStream<JsonElement> synchronizationItems,
        string area
    ) => await synchronizationItems.GetControlStream(area).FirstAsync(x => x != null);

    public static async Task<object> GetControl(
        this ISynchronizationStream<EntityStore> synchronizationItems,
        string area
    ) => await synchronizationItems.GetControlStream(area).FirstAsync(x => x != null);

    public static IObservable<object> GetDataStream(
        this ISynchronizationStream<JsonElement> stream,
        JsonPointerReference reference
    ) =>
        stream
            .Reduce(reference, stream.Hub.Address)
            .Select(x => x.Value?.Deserialize<object>(stream.Hub.JsonSerializerOptions));

    public static IObservable<T> GetDataStream<T>(
        this ISynchronizationStream<JsonElement> stream,
        JsonPointerReference reference
    ) =>
        stream
            .Reduce(reference, stream.Hub.Address)
            .Select(x =>
                x.Value == null
                    ? default
                    : x.Value.Value.Deserialize<T>(stream.Hub.JsonSerializerOptions)
            );

    public static MessageHubConfiguration AddLayoutClient(
        this MessageHubConfiguration config,
        Func<LayoutClientConfiguration, LayoutClientConfiguration> configuration
    )
    {
        return config
            //.AddData(data => data.Configure(c => c.AddWorkspaceReferenceStream<LayoutAreaReference, EntityStore>((parent,reduced) => parent.Select(e => e.SetValue(e.Value.StoresByStream.GetValueOrDefault(reduced.StreamReference))))
            .AddData()
            .AddLayoutTypes()
            .WithServices(services => services.AddScoped<ILayoutClient, LayoutClient>())
            .Set(config.GetConfigurationFunctions().Add(configuration))
            .WithSerialization(serialization => serialization);
    }

    internal static ImmutableList<
        Func<LayoutClientConfiguration, LayoutClientConfiguration>
    > GetConfigurationFunctions(this MessageHubConfiguration config) =>
        config.Get<ImmutableList<Func<LayoutClientConfiguration, LayoutClientConfiguration>>>()
        ?? ImmutableList<Func<LayoutClientConfiguration, LayoutClientConfiguration>>.Empty;

    public static JsonObject SetPath(this JsonObject obj, string path, JsonNode value)
    {
        var jsonPath = JsonPath.Parse(path);
        var existingValue = jsonPath.Evaluate(obj);
        var op =
            existingValue.Matches?.Any() ?? false
                ? PatchOperation.Replace(JsonPointer.Parse(path), value)
                : PatchOperation.Add(JsonPointer.Parse(path), value);

        var patchDocument = new JsonPatch(op);
        return (JsonObject)patchDocument.Apply(obj).Result;
    }
}
