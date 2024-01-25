﻿using OpenSmc.Serialization;

namespace OpenSmc.Application.Scope;

public class ScopePropertyChangedEventTransformation 
{
    private readonly IScopeRegistry scopeRegistry;
    private readonly ISerializationService serializationService;

    public ScopePropertyChangedEventTransformation(IScopeRegistry scopeRegistry, ISerializationService serializationService)
    {
        this.scopeRegistry = scopeRegistry;
        this.serializationService = serializationService;
    }

    public async Task<object> GetAsync(ScopePropertyChangedEvent @event)
    {
        var s = scopeRegistry.GetScope(@event.ScopeId) as IScope;
        if (s == null)
            return null;
        var property = s.GetScopeType().GetScopeProperties().SelectMany(x => x.Properties).First(x => x.Name == @event.Property);
        var serialized = await serializationService.SerializePropertyAsync(@event.Value, s, property);
        return new ScopePropertyChanged(@event.ScopeId.AsString(), @event.Property.ToCamelCase(), serialized, ConvertEnum(@event.Status));
    }

    private static PropertyChangeStatus ConvertEnum(ScopeChangedStatus status)
    {
        return Enum.TryParse(typeof(PropertyChangeStatus), status.ToString(), out var item) ? (PropertyChangeStatus)item! : throw new NotSupportedException();
    }
}