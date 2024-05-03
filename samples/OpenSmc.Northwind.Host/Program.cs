﻿using static OpenSmc.Application.SignalR.SignalRExtensions;

namespace OpenSmc.Northwind.Host;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureApplicationSignalR();

        builder.Host
            .UseOrleans(static siloBuilder =>
            {
                siloBuilder.UseLocalhostClustering();
            });

        await using var app = builder.Build();

        app
            .UseRouting()
            .UseApplicationSignalR();

        await app.RunAsync();
    }
}
