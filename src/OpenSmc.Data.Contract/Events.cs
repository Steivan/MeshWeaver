﻿using OpenSmc.Messaging;
using OpenSmc.Serialization;

namespace OpenSmc.Data;

public record GetManyRequest<T> : IRequest<GetResponse<T>>
{
    public int Page { get; init; }
    public int? PageSize { get; init; }
    public object Options { get; init; }
};

public abstract record GetManyResponseBase(int Total);
public record GetResponse<T>(int Total, IReadOnlyCollection<T> Items) : GetManyResponseBase(Total)
{
    public static GetResponse<T> Empty() => new(0, Array.Empty<T>());
}

public record UpdateDataRequest(IReadOnlyCollection<object> Elements) : DataChangeRequestWithElements(Elements)
{

    public UpdateOptions Options { get; init; }
}

public record DeleteDataRequest(IReadOnlyCollection<object> Elements) : DataChangeRequestWithElements(Elements);

public abstract record DataChangeRequestWithElements(IReadOnlyCollection<object> Elements) : DataChangeRequest;

public abstract record DataChangeRequest : IRequest<DataChangeResponse>;

public record DataChangeResponse(long Version, DataChangeStatus Status, DataChangedEvent Changes);

public enum DataChangeStatus{Committed, Failed}

public record CreateRequest<TObject>(TObject Element) : IRequest<DataChangedEvent> { public object Options { get; init; } };


/// <summary>
/// Starts data synchronization with data corresponding to the Json Path queries as specified in the constructor.
/// </summary>
/// <param name="JsonPaths">All the json paths to be synchronized. E.g. <code>"$.['MyNamespace.MyEntity']"</code></param>
public record SubscribeDataRequest(IReadOnlyDictionary<string,string> JsonPaths) : IRequest<DataChangedEvent>;

public record DataChangedEvent(long Version, RawJson Change, ChangeType Type);

public enum ChangeType{Full, Patch}


/// <summary>
/// Ids of the synchronization requests to be stopped (generated with request)
/// </summary>
/// <param name="Ids"></param>
public record UnsubscribeDataRequest(params string[] Ids);

public record PatchChangeRequest(object Change) : DataChangeRequest
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
}

public abstract record WorkspaceReference(object Address);
public record EntityReference(object Address, string Collection, object Id) : WorkspaceReference(Address);