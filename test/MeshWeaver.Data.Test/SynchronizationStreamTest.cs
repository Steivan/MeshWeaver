﻿using System.Reactive.Linq;
using FluentAssertions;
using FluentAssertions.Extensions;
using MeshWeaver.Fixture;
using MeshWeaver.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace MeshWeaver.Data.Test;

public class SynchronizationStreamTest(ITestOutputHelper output) : HubTestBase(output)
{
    private const string Instance = nameof(Instance);

    protected override MessageHubConfiguration ConfigureHost(MessageHubConfiguration configuration)
    {
        return base.ConfigureHost(configuration)
            .AddData(data =>
                data.FromConfigurableDataSource(
                    "ad hoc",
                    dataSource =>
                        dataSource.WithType<MyData>(type =>
                            type.WithKey(instance => instance.Id)
                        ).WithType<object>(type => type.WithKey(i => i))
                )
            );
    }

    [Fact]
    public async Task ParallelUpdate()
    {
        List<MyData> tracker = new();
        var workspace = GetHost().GetWorkspace();
        var collectionName = workspace.DataContext.GetTypeSource(typeof(MyData)).CollectionName;
        var stream = workspace.GetStream(new CollectionsReference(collectionName));
        stream.Should().NotBeNull();
        stream.Reduce(new EntityReference(collectionName, Instance))
            .Select(i => i.Value)
            .Cast<MyData>()
            .Where(i => i != null)
            .Subscribe(tracker.Add);

        var count = 0;
        var array = Enumerable.Range(0, 10).AsParallel().Select(_ =>
        {
            stream.UpdateAsync(state =>
            {
                var instance = new MyData(Instance, (++count).ToString());
                var existingInstance = state?.Collections.GetValueOrDefault(collectionName)?.Instances
                    .GetValueOrDefault(Instance);

                return stream.ApplyChanges(
                    new EntityStoreAndUpdates(
                        (state ?? new()).Update(collectionName, i => i.Update(Instance, instance)),
                        [new EntityUpdate(collectionName, Instance, instance) { OldValue = existingInstance }],
                        stream.StreamId)
                );
            });
            return true;
        }).ToArray();

        stream.Dispose();
        await DisposeAsync();

        tracker.Should().HaveCount(10)
            .And.Subject.Select(t => t.Text).Should().Equal(Enumerable.Range(0, 10).Select(exp => (exp+1).ToString()));
    }
}
