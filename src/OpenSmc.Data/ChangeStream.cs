﻿using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Patch;
using OpenSmc.Serialization;

namespace OpenSmc.Data;

public record ChangeItem<TReference>(TReference Value, object ChangedBy);

public record ChangeStream<TReference> : IDisposable,
        IObservable<ChangeItem<TReference>>,
        IObserver<DataChangedEvent>,
        IObservable<DataChangedEvent>
{
    protected readonly ReplaySubject<ChangeItem<TReference>> Subject = new();
    protected readonly Subject<DataChangedEvent> DataChangedStream = new();
    public readonly Subject<ChangeItem<JsonPatch>> Changes = new();
    private IDisposable updateSubscription;

    protected JsonNode LastSynchronized { get; set; }

    public IDisposable Subscribe(IObserver<ChangeItem<TReference>> observer)
    {
        return Subject.Subscribe(observer);
    }


    public readonly List<IDisposable> Disposables = new();

    public ChangeStream(IWorkspace Workspace, object Address, WorkspaceReference<TReference> Reference, JsonSerializerOptions Options, Func<long> GetVersion)
    {
        this.Workspace = Workspace;
        this.Address = Address;
        this.Reference = Reference;
        this.Options = Options;
        this.GetVersion = GetVersion;

        Disposables.Add(Workspace.ChangeStream.Select(ws => new { Value = ws.Reduce(Reference), ws.LastChangedBy }).Subscribe(ws => Update(ws.Value, ws.LastChangedBy)));
        Disposables.Add(Workspace.Stream.Select(ws => new { Value = ws.Reduce(Reference), ws.LastChangedBy }).Subscribe(ws => Synchronize(ws.Value, ws.LastChangedBy)));
    }

    public void Dispose()
    {
        foreach (var disposeAction in Disposables)
            disposeAction.Dispose();

        Subject.Dispose();
        updateSubscription?.Dispose();
    }

    public void Update(TReference newStore, object changedBy)
    {
        var newJson = JsonSerializer.SerializeToNode(newStore, Options);
        var patch = LastSynchronized.CreatePatch(newJson);
        if (patch.Operations.Any())
            Changes.OnNext(new ChangeItem<JsonPatch>(patch,changedBy));
        LastSynchronized = newJson;
    }


    private void Synchronize(DataChangedEvent request)
    {
        var newStoreSerialized = request.ChangeType switch
        {
            ChangeType.Patch => LastSynchronized = JsonSerializer.Deserialize<JsonPatch>(request.Change.Content).Apply(LastSynchronized)
                .Result,
            ChangeType.Full => LastSynchronized = JsonNode.Parse(request.Change.Content),
            _ => throw new ArgumentOutOfRangeException()
        };

        var newStore = newStoreSerialized.Deserialize<TReference>(Options);
        Subject.OnNext(new(newStore, request.ChangedBy));
    }

    public void Initialize(TReference initial)
    {
        if(initial == null)
            throw new ArgumentNullException(nameof(initial));
        Subject.OnNext(new(initial, Address));
        LastSynchronized = JsonSerializer.SerializeToNode(initial, Options);
    }

    private void Synchronize(TReference value, object changedBy)
    {
        if (Address.Equals(changedBy))
            return;

        var node = JsonSerializer.SerializeToNode(value, Options);


        var dataChanged = LastSynchronized == null
            ? GetFullDataChange(node)
            : new DataChangedEvent(Address, Reference, GetVersion(), new(JsonSerializer.Serialize(LastSynchronized.CreatePatch(node))), ChangeType.Patch, changedBy ?? Address);

        DataChangedStream.OnNext(dataChanged);
        LastSynchronized = node;

    }

    private DataChangedEvent GetFullDataChange(JsonNode node)
    {
        return new DataChangedEvent(Address, Reference, GetVersion(), new RawJson(node!.ToJsonString()), ChangeType.Full, Address);
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

    
    public IDisposable Subscribe(IObserver<DataChangedEvent> observer)
    {
        if(LastSynchronized != null)
            observer.OnNext(GetFullDataChange(LastSynchronized));
        return DataChangedStream.Subscribe(observer);
    }



    public IDisposable Subscribe(IObserver<ChangeItem<JsonPatch>> observer) => Changes.Subscribe(observer);
}