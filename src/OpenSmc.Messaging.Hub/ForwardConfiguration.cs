﻿using System.Collections.Immutable;

namespace OpenSmc.Messaging;

public record ForwardConfiguration(IMessageHub Hub)
{

    internal ImmutableList<AsyncDelivery> Handlers { get; init; } = ImmutableList<AsyncDelivery>.Empty;



    public ForwardConfiguration RouteAddressToHub<TAddress>(Func<TAddress, IMessageHub> hubFactory) =>
        RouteAddress<TAddress>((routedAddress, d) =>
        {
            var hub = hubFactory(routedAddress);
            hub.DeliverMessage(d);
            return d.Forwarded();
        });

    public ForwardConfiguration RouteAddress<TAddress>(SyncRouteDelivery<TAddress> handler) =>
        RouteAddress<TAddress>((routedAddress, d) => Task.FromResult(handler(routedAddress, d)));
        
    public ForwardConfiguration RouteAddress<TAddress>(AsyncRouteDelivery<TAddress> handler)
        => this with
        {
            Handlers = Handlers.Add(async delivery =>
            {
                if (delivery.State != MessageDeliveryState.Submitted || Hub.Address.Equals(delivery.Target))
                    return delivery;
                var routedAddress = FlattenAddressHierarchy(delivery.Target).OfType<TAddress>().FirstOrDefault();
                if (routedAddress == null)
                    return delivery;
                // TODO: should we take care of result from handler somehow?
                return await handler(routedAddress, delivery);
            }
            ),
        };


    public ForwardConfiguration RouteMessageToTarget<TMessage>(Func<IMessageDelivery, object> addressMap) =>
        RouteMessage<TMessage>(delivery =>
        {
            Hub.Post(delivery.Message, o => o.WithTarget(addressMap.Invoke(delivery)));
            return Task.FromResult(delivery.Forwarded());
        });


    public ForwardConfiguration RouteMessage<TMessage>(SyncDelivery handler) =>
        RouteMessage<TMessage>(d => Task.FromResult(handler(d)));

    public ForwardConfiguration RouteMessage<TMessage>(AsyncDelivery handler)
        => this with
        {
            Handlers = Handlers.Add(async delivery =>
                {
                    if (delivery.State != MessageDeliveryState.Submitted || delivery.Message is not TMessage)
                        return delivery;
                    return await handler(delivery);
                }
            ),
        };

    public ForwardConfiguration RouteAddressToHostedHub<TAddress>(Func<MessageHubConfiguration, MessageHubConfiguration> configuration)
        => RouteAddressToHub<TAddress>(a => Hub.GetHostedHub(a, configuration));

    public ForwardConfiguration RouteAddressToHostedHub<TAddress>(TAddress address, Func<MessageHubConfiguration, MessageHubConfiguration> configuration)
        => RouteAddressToHub<TAddress>(d => Hub.GetHostedHub(address, configuration));

    private IEnumerable<object> FlattenAddressHierarchy(object address)
    {
        while (address != null)
        {
            yield return address;
            address = (address as IHostedAddress)?.Host;
        }
    }
}



public interface IForwardConfigurationItem
{

    AsyncDelivery Route { get; }
    bool Filter(IMessageDelivery delivery);
}
public record ForwardConfigurationItem<TMessage> : IForwardConfigurationItem
{


    AsyncDelivery IForwardConfigurationItem.Route => d => Route((IMessageDelivery<TMessage>)d);


    bool IForwardConfigurationItem.Filter(IMessageDelivery delivery)
    {
        if (delivery.Message is not TMessage)
            return false;
        return Filter?.Invoke((IMessageDelivery<TMessage>)delivery) ?? true;
    }


    internal AsyncDelivery Route { get; init; }


    public ForwardConfigurationItem<TMessage> WithFilter(DeliveryFilter<TMessage> filter) => this with { Filter = filter };
    internal DeliveryFilter<TMessage> Filter { get; init; }

    public ForwardConfigurationItem<TMessage> WithInheritorsFromAssembliesOf(params Type[] types)
    {
        return this with { InheritorsFrom = types };
    }

    public ForwardConfigurationItem<TMessage> WithInheritance(bool inherit = true) => this with { ForwardInheritors = inherit };
    internal bool ForwardInheritors { get; init; } = true;
    public IReadOnlyCollection<Type> InheritorsFrom { get; init; } = Array.Empty<Type>();

}

