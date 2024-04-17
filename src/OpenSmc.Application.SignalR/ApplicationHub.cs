﻿using JetBrains.Annotations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OpenSmc.Application.Orleans;
using OpenSmc.Messaging;
using OpenSmc.Serialization;
using Orleans.Streams;

namespace OpenSmc.Application.SignalR;

public class ApplicationHub(IClusterClient clusterClient, IHubContext<ApplicationHub> hubContext, ILogger<ApplicationHub> logger) : Hub
{
    private StreamSubscriptionHandle<IMessageDelivery> subscriptionHandle; // HACK V10: it doesn't work this way and need to be saved somewhere externally (for example within the component retrieved from DI) (2023/09/27, Dmitry Kalabin)

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        logger.LogDebug("Attempt to disconnect for connection {ConnectionId} with exception {exception}", Context.ConnectionId, exception);
        await subscriptionHandle.UnsubscribeAsync(); // TODO V10: change to handle multiple subscriptions per ConnectionId (2024/04/17, Dmitry Kalabin)
        await base.OnDisconnectedAsync(exception);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogDebug("Attempt to make new SignalR connection {ConnectionId} ", Context.ConnectionId);

        await base.OnConnectedAsync();

        var streamProvider = clusterClient.GetStreamProvider(ApplicationStreamProviders.AppStreamProvider);
        var stream = streamProvider.GetStream<IMessageDelivery>(ApplicationStreamNamespaces.Ui, TestUiIds.HardcodedUiId);

        // TODO V10: change to handle subscriptions per ConnectionId (2024/04/17, Dmitry Kalabin)
        subscriptionHandle = await stream
            .SubscribeAsync(async(delivery, _) => 
                {
                    logger.LogTrace("Received {Event}, sending to client.", delivery);
                });
    }

    [UsedImplicitly]
    public void DeliverMessage(MessageDelivery<RawJson> delivery)
    {
        logger.LogTrace("Received incoming message in SignalR Hub to deliver: {delivery}", delivery);
        var grainId = "{ApplicationAddress should be here}"; // TODO V10: put appropriate ApplicationAddress here (2024/04/15, Dmitry Kalabin)
        var grain = clusterClient.GetGrain<IApplicationGrain>(grainId);

        // HACK V10: get rid of this hardcoding as soon as deserialization for SignalR would work (2024/04/15, Dmitry Kalabin)
        var workaroundDelivery = delivery with { Sender = new UiAddress(TestUiIds.HardcodedUiId), Target = new ApplicationAddress(TestApplication.Name, TestApplication.Environment), };

        var task = grain.DeliverMessage(workaroundDelivery); // TODO V10: This is async and we might think about passing this through a Hub to make it better (2024/04/15, Dmitry Kalabin)
        var result = task.Result;
    }
}
