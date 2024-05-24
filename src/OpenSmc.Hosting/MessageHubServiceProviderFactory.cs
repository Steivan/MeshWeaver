﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenSmc.ServiceProvider;

namespace OpenSmc.Hosting;

internal class MessageHubServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    public IServiceCollection CreateBuilder(IServiceCollection services) => services;

    public IServiceProvider CreateServiceProvider(IServiceCollection services)
    {
        var serviceProvider = services.SetupModules();
        return serviceProvider;
    }
}

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureServiceProvider(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseServiceProviderFactory(new MessageHubServiceProviderFactory());
    }
}