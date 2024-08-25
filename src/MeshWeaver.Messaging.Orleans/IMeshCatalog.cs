﻿using MeshWeaver.Application;
using System.Collections.Immutable;
using MeshWeaver.Messaging;
using MeshWeaver.Orleans.Contract;
using Microsoft.Extensions.DependencyInjection;
using Orleans;

namespace MeshWeaver.Orleans;

public interface IMeshCatalog
{
    void Configure(Func<MeshNodeInfoConfiguration, MeshNodeInfoConfiguration> config);
    string GetMeshNodeId(object address);
    Task<MeshNode> GetNodeAsync(object address);
    public Task UpdateMeshNode(MeshNode node);
}

public class MeshCatalog(IServiceProvider serviceProvider) : IMeshCatalog
{
    private readonly IGrainFactory grainFactory = serviceProvider.GetRequiredService<IGrainFactory>();

    private MeshNodeInfoConfiguration Configuration { get; set; } = StandardConfiguration(serviceProvider);

    public static MeshNodeInfoConfiguration StandardConfiguration(IServiceProvider serviceProvider)
    {
        return new MeshNodeInfoConfiguration()
            .WithModuleMapping(o => o is not ApplicationAddress ? null : SerializationExtensions.GetId(o))
            .WithModuleMapping(SerializationExtensions.GetTypeName);
    }

    // TODO V10: Put this somewhere outside in the config and read in constructor (25.08.2024, Roland Bürgi)
    public void Configure(Func<MeshNodeInfoConfiguration, MeshNodeInfoConfiguration> config)
    {
        Configuration = config(Configuration);
    }
    public string GetMeshNodeId(object address)
        => Configuration.ModuleLoaders
            .Select(loader => loader(address))
               .FirstOrDefault(x => x != null);

    public Task<MeshNode> GetNodeAsync(object address)=> grainFactory.GetGrain<IMeshNodeGrain>(GetMeshNodeId(address)).Get();

    public Task UpdateMeshNode(MeshNode node) => grainFactory.GetGrain<IMeshCatalogGrain>(node.Id).Update(node);
}
public record MeshNodeInfoConfiguration
{
    internal ImmutableList<Func<object, string>> ModuleLoaders { get; init; }
        = [];

    public MeshNodeInfoConfiguration WithModuleMapping(Func<object, string> moduleInfoProvider)
        => this with { ModuleLoaders = ModuleLoaders.Insert(0, moduleInfoProvider) };

}

