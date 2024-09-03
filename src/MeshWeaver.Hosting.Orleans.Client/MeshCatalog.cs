﻿using System.Reflection;
using System.Runtime.Loader;
using MeshWeaver.Mesh.Contract;
using MeshWeaver.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace MeshWeaver.Hosting.Orleans.Client
{
    public class MeshCatalog(IMessageHub hub) : IMeshCatalog
    {
        private readonly IDisposable deferral = hub.Defer(_ => true);
        private readonly IGrainFactory grainFactory = hub.ServiceProvider.GetRequiredService<IGrainFactory>();

        private MeshConfiguration Configuration { get; } = hub.Configuration.GetMeshContext();


        public string GetNodeId(object address)
            => Configuration.AddressToMeshNodeMappers
                .Select(loader => loader(address))
                .FirstOrDefault(x => x != null);

        public Task<MeshNode> GetNodeAsync(object address)=> 
            GetNodeAsync(GetNodeId(address));

        public Task<MeshNode> GetNodeAsync(string id)
            => grainFactory.GetGrain<IMeshNodeGrain>(id).Get();

        public Task UpdateAsync(MeshNode node) => grainFactory.GetGrain<IMeshNodeGrain>(node.Id).Update(node);
        public async Task InstallModuleAsync(string fullPathToDll)
        {
            var assemblyLoadContext = new AssemblyLoadContext("ModuleRegistry");
            var assembly = assemblyLoadContext.LoadFromAssemblyPath(fullPathToDll);
            foreach (var node in assembly.GetCustomAttributes<MeshNodeAttribute>().Select(a => a.Node))
                await grainFactory.GetGrain<IMeshNodeGrain>(node.Id).Update(node);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            foreach (var assemblyLocation in Configuration.InstallAtStartup)
            {
                var basePath = Path.GetDirectoryName(assemblyLocation);
                var loadContext = new CollectibleAssemblyLoadContext(basePath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyLocation);
                foreach (var node in assembly.GetCustomAttributes<MeshNodeAttribute>().Select(a => a.Node))
                    await grainFactory.GetGrain<IMeshNodeGrain>(node.Id).Update(node);
                loadContext.Unload();
            }

            deferral.Dispose();
        }

        public Task<ArticleEntry> GetArticleAsync(string id)
            => grainFactory.GetGrain<IArticleGrain>(id).Get();

        public Task UpdateArticleAsync(ArticleEntry article)
            => grainFactory.GetGrain<IArticleGrain>(article.Id).Update(article);
    }

    public class CollectibleAssemblyLoadContext(string basePath) : AssemblyLoadContext(true){

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var assemblyPath = Path.Combine(basePath, $"{assemblyName.Name}.dll");
            if (File.Exists(assemblyPath))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }
}
