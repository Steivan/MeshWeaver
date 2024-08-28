﻿using MeshWeaver.Hosting;
using MeshWeaver.Mesh.Contract;
using MeshWeaver.Messaging;
using MeshWeaver.Orleans.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Serialization;

namespace MeshWeaver.Orleans.Server;

public static  class OrleansServerRegistryExtensions
{
    public static void AddOrleansMeshServer<TAddress>(this WebApplicationBuilder builder, 
        TAddress address,
        Func<MeshConfiguration, MeshConfiguration> meshConfiguration = null,
        Func<MessageHubConfiguration, MessageHubConfiguration> hubConfiguration = null,
        Action<ISiloBuilder> siloConfiguration = null)
    {
        builder.AddOrleansMeshInternal(address, hubConfiguration, meshConfiguration);
        
        builder.UseOrleans(orleansBuilder =>
        {

            if(siloConfiguration != null)
                siloConfiguration.Invoke(orleansBuilder);

            orleansBuilder.Services.AddSerializer(serializerBuilder =>
            {
                serializerBuilder.AddJsonSerializer(
                    type => true,
                    type => true,
                    ob =>
                        ob.PostConfigure<IMessageHub>(
                            (o, hub) => o.SerializerOptions = hub.JsonSerializerOptions
                        )
                );
            });
        });

        builder.Services.AddSingleton<IHostedService, CatalogInitializationHostedService>();
    }
}

public class CatalogInitializationHostedService(IMessageHub hub) : IHostedService
{
    private readonly IMeshCatalog catalog = hub.ServiceProvider.GetRequiredService<IMeshCatalog>();
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await hub.ServiceProvider.GetRequiredService<IRoutingService>().RegisterHubAsync(hub);
        await catalog.InitializeAsync(cancellationToken);

    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
