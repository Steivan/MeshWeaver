﻿using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;

namespace OpenSmc.Messaging.Hub;

public static class MessageForwardingExtensions
{
    public static MessageHubConfiguration WithMessageForwarding(this MessageHubConfiguration configuration, Func<RoutedHubConfiguration, RoutedHubConfiguration> routedHubBuilder)
    {

        return configuration.WithBuildupAction(hub =>
        {
            hub.WithMessageForwarding(routedHubBuilder);
        });
    }
    public static void WithMessageForwarding(this IMessageHub hub, Func<RoutedHubConfiguration, RoutedHubConfiguration> routedHubBuilder)
    {
        var routedHubConfiguration = routedHubBuilder.Invoke(new());
        routedHubConfiguration = routedHubConfiguration.Buildup(hub);
        hub.Post(new UpdateRequest<RoutedHubConfiguration>(routedHubConfiguration));
    }

    public static bool IsAddress<TAddress>(this object address)
    {
        if (address is TAddress)
            return true;

        if (address is IHostedAddress ha)
            return IsAddress<TAddress>(ha.Host);

        return false;
    }
}


public record MessageForwardingDefinition
{
    private ImmutableList<(SyncDelivery Delivery, Predicate<object> Filter)> Routes { get; init; } = ImmutableList<(SyncDelivery, Predicate<object>)>.Empty;
    private SyncDelivery DefaultDelivery { get; set; }

    public MessageForwardingDefinition WithRoutedAddress<TRoutedAddress>(SyncDelivery delivery)
    {
        return WithRoutedAddress(delivery, a => a.IsAddress<TRoutedAddress>());
    }
    public MessageForwardingDefinition WithRoutedAddress(SyncDelivery delivery, Predicate<object> addressFilter)
    {
        return this with { Routes = Routes.Add((delivery, addressFilter)) };
    }

    public MessageForwardingDefinition WithDefaultRoute(SyncDelivery delivery)
    {
        return this with { DefaultDelivery = delivery };
    }

    public MessageHubConfiguration Build<TAddress>(MessageHubConfiguration configuration)
    {

        if (DefaultDelivery == null)
        {
            var mainRouterHub = configuration.ServiceProvider.GetService<IMessageHub>();

            if (mainRouterHub != null)
            {
                DefaultDelivery = mainRouterHub.DeliverMessage;
            }
        }

        return configuration;

    }
}
