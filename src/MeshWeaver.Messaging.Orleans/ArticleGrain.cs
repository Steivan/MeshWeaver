﻿using MeshWeaver.Mesh.Contract;
using MeshWeaver.Messaging;
using MeshWeaver.Orleans.Client;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace MeshWeaver.Orleans.Server;

[StorageProvider(ProviderName = StorageProviders.MeshCatalog)]
public class ArticleGrain(ILogger<ArticleGrain> logger) : Grain<ArticleEntry>, IArticleGrain
{
    public Task<ArticleEntry> Get()
    {
        logger.LogDebug("Retrieving Article Entry {Article}", this.GetPrimaryKeyString());
        return Task.FromResult(State);
    }

    public async Task Update(ArticleEntry entry)
    {
        logger.LogDebug("Updating Article to {entry}", entry);
        State = entry;
        await WriteStateAsync();
    }
}
