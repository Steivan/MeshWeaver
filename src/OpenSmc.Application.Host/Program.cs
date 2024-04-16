﻿using OpenSmc.Application.SignalR;
using OpenSmc.Messaging;
using Orleans.Serialization;
using static OpenSmc.Hosting.HostBuilderExtensions;

namespace OpenSmc.Application.Host;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureApplicationSignalR();

        builder.Host
            .ConfigureServiceProvider()
            .UseOrleans(static siloBuilder =>
            {
                siloBuilder.UseLocalhostClustering();
                siloBuilder.Services.AddSerializer(serializerBuilder =>
                {
                    serializerBuilder.AddJsonSerializer(isSupported: type => true);
                });
            });

        using var app = builder.Build();

        app
            .UseRouting()
            .UseApplicationSignalR();

        await app.RunAsync();
    }
}

public static class OrleansMessageHubExtensions
{
    public static IServiceCollection AddOrleansHub(this IServiceCollection services)
    {
        services.AddSingleton(sp => sp.GetOrleansHub());
        return services;
    }

    public static IMessageHub GetOrleansHub(this IServiceProvider serviceProvider) 
        => serviceProvider.CreateMessageHub(new OrleansAddress(), conf => 
            conf
        );
}

record OrleansAddress;
