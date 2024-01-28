﻿using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Serialization;

namespace OpenSmc.Messaging;

public static class MessageDeliverySerializationExtension
{
    public static IMessageDelivery Package(this IMessageDelivery delivery)
    {
        var senderHub = (IMessageHub)delivery.Context;
        var serializationService = senderHub.ServiceProvider.GetService<ISerializationService>();
        if (serializationService == null) 
            return delivery;

        return SerializeDelivery(serializationService, delivery);
    }

    public static IMessageDelivery SerializeDelivery(this ISerializationService serializationService, IMessageDelivery delivery)
    {
        try
        {
            var rawJson = serializationService.SerializeAsync(delivery.Message);
            return delivery.WithMessage(rawJson);
        }
        catch (Exception e)
        {
            return delivery.Failed($"Error serializing: \n{e}");
        }
    }

    public static IMessageDelivery DeserializeDelivery(this ISerializationService serializationService, IMessageDelivery delivery)
    {
        if (delivery.Message is not RawJson rawJson)
            return delivery;

        var deserializedMessage = serializationService.Deserialize(rawJson.Content);
        return delivery.WithMessage(deserializedMessage);
    }
}