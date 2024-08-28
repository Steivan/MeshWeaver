﻿using MeshWeaver.Messaging;
using MeshWeaver.Orleans.Client;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace MeshWeaver.Orleans.Server;

[StorageProvider(ProviderName = Storage)]
public record ArticleGrain : IArticleGrain
{
    private readonly IPersistentState<ArticleEntry> state;
    private readonly ILogger<ArticleGrain> logger;

    public ArticleGrain([PersistentState(OrleansClientExtensions.Storage, storageName: Storage)] IPersistentState<ArticleEntry> state, ILogger<ArticleGrain> logger )
    {
        this.state = state;
        this.logger = logger;
        if(state.RecordExists)
            logger.LogDebug("Loaded Article Entry {Article}", state.State);
        else
            logger.LogDebug("Article {Article} not found", this.GetPrimaryKeyString());

    }
    public const string Storage = "articles";
    public Task<ArticleEntry> Get()
    {
        logger.LogDebug("Retrieving Article Entry {Article}", this.GetPrimaryKeyString());
        return Task.FromResult(state.State);
    }

    public async Task Update(ArticleEntry entry)
    {
        logger.LogDebug("Updating Article to {entry}", entry);
        state.State = entry;
        await state.WriteStateAsync();
    }
}
