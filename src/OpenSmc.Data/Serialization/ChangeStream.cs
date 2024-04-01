﻿using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Patch;
using OpenSmc.Serialization;

namespace OpenSmc.Data.Serialization;

public record ChangeItem<TReference>(object Address, WorkspaceReference Reference, TReference Value, object ChangedBy);

public record ChangeStream<TReference> : IDisposable, IObserver<DataChangedEvent>, IObservable<DataChangedEvent>
{
    private readonly bool isExternalStream;
    public readonly ReplaySubject<ChangeItem<TReference>> Store = new(1);
    private readonly Subject<DataChangedEvent> dataChangedStream = new();
    public readonly Subject<PatchChangeRequest> Changes = new();
    private IDisposable updateSubscription;

    protected JsonNode LastSynchronized { get; set; }

    public IDisposable Subscribe(IObserver<ChangeItem<TReference>> observer)
    {
        return Store.Subscribe(observer);
    }
    public IDisposable Subscribe(IObserver<DataChangedEvent> observer)
    {
        if (LastSynchronized != null)
            observer.OnNext(GetFullDataChange(LastSynchronized));
        return dataChangedStream.Subscribe(observer);
    }


    public readonly List<IDisposable> Disposables = new();

    public ChangeStream(IWorkspace Workspace,
        object Address,
        WorkspaceReference<TReference> Reference,
        JsonSerializerOptions Options,
        Func<long> GetVersion,
        bool isExternalStream)
    {
        this.isExternalStream = isExternalStream;
        this.Workspace = Workspace;
        this.Address = Address;
        this.Reference = Reference;
        this.Options = Options;
        this.GetVersion = GetVersion;

        Disposables.Add(isExternalStream
            ? Workspace.GetStream(Reference).DistinctUntilChanged().Subscribe(Update)
            : Workspace.GetStream(Reference).DistinctUntilChanged().Subscribe(Synchronize));
    }

    public void Dispose()
    {
        foreach (var disposeAction in Disposables)
            disposeAction.Dispose();

        Store.Dispose();
        updateSubscription?.Dispose();
    }

    public void Update(TReference newStore)
    {
        var newJson = JsonSerializer.SerializeToNode(newStore, Options);
        var patch = LastSynchronized.CreatePatch(newJson);
        if (patch.Operations.Any())
            Changes.OnNext(new PatchChangeRequest(Address, Reference, patch));
        LastSynchronized = newJson;
    }


    private void Synchronize(DataChangedEvent request)
    {
        var newStoreSerialized = request.ChangeType switch
        {
            ChangeType.Patch => LastSynchronized = JsonSerializer.Deserialize<JsonPatch>(((RawJson)request.Change).Content).Apply(LastSynchronized)
                .Result,
            ChangeType.Full => LastSynchronized = JsonNode.Parse(((RawJson)request.Change).Content),
            _ => throw new ArgumentOutOfRangeException()
        };

        var newStore = newStoreSerialized.Deserialize<TReference>(Options);
        Store.OnNext(new(Address, Reference, newStore, request.ChangedBy));
    }

    public ChangeStream<TReference> Initialize(TReference initial)
    {
        if (initial == null)
            throw new ArgumentNullException(nameof(initial));
        Store.OnNext(new(Address, Reference, initial, null));
        LastSynchronized = JsonSerializer.SerializeToNode(initial, Options);
        dataChangedStream.OnNext(GetFullDataChange(LastSynchronized));
        return this;
    }

    private void Synchronize(TReference value)
    {
        var node = JsonSerializer.SerializeToNode(value, Options);


        var dataChanged = LastSynchronized == null
            ? GetFullDataChange(node)
            : GetPatch(node);

        if (dataChanged != null)
            dataChangedStream.OnNext(dataChanged);
        LastSynchronized = node;

    }

    private DataChangedEvent GetPatch(JsonNode node)
    {
        var jsonPatch = LastSynchronized.CreatePatch(node);
        if (!jsonPatch.Operations.Any())
            return null;
        return new DataChangedEvent(Address, Reference, GetVersion(), new RawJson(JsonSerializer.Serialize(jsonPatch)), ChangeType.Patch, Address);
    }

    private DataChangedEvent GetFullDataChange(JsonNode node)
    {
        return new DataChangedEvent(Address, Reference, GetVersion(), new RawJson(node!.ToJsonString()), ChangeType.Full, null);
    }


    internal Func<long> GetVersion { get; init; }
    public IWorkspace Workspace { get; init; }
    public object Address { get; init; }
    public WorkspaceReference<TReference> Reference { get; init; }
    public JsonSerializerOptions Options { get; init; }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }



    public void OnNext(DataChangedEvent value)
        => Synchronize(value);





    public IDisposable Subscribe(IObserver<PatchChangeRequest> observer) => Changes.Subscribe(observer);
}