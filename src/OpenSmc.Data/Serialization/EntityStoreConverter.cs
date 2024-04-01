﻿using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.More;

namespace OpenSmc.Data.Serialization;

public class EntityStoreConverter : JsonConverter<EntityStore>
{
    public override EntityStore Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        return Deserialize(doc.RootElement.AsNode(), options);
    }

    public override void Write(Utf8JsonWriter writer, EntityStore value, JsonSerializerOptions options)
    {
        Serialize(value, options).WriteTo(writer);
    }

    private JsonNode Serialize(EntityStore store, JsonSerializerOptions options)
    {
        var ret = new JsonObject(
            store.Instances.ToDictionary(
                x => x.Key,
                x => JsonSerializer.SerializeToNode(x.Value, options)
            ));
        return ret;
    }



    public EntityStore Deserialize(JsonNode serializedWorkspace, JsonSerializerOptions options)
    {
        if (serializedWorkspace is not JsonObject obj)
            throw new ArgumentException("Invalid serialized workspace");

        var newStore =
            new EntityStore(obj.Select(kvp => DeserializeCollection(kvp.Key, kvp.Value, options)).ToImmutableDictionary());

        return newStore;
    }

    private KeyValuePair<string, InstanceCollection> DeserializeCollection(string collection, JsonNode node, JsonSerializerOptions options)
    {
        return
            new(
                collection,
                node.Deserialize<InstanceCollection>(options)
            );
    }
}