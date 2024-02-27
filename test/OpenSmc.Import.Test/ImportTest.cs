﻿using FluentAssertions;
using OpenSmc.Activities;
using OpenSmc.Data;
using OpenSmc.Data.TestDomain;
using OpenSmc.DataStructures;
using OpenSmc.Hub.Fixture;
using OpenSmc.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace OpenSmc.Import.Test;

public class ImportTest(ITestOutputHelper output) : HubTestBase(output)
{

    private const string Cashflows = nameof(Cashflows);
    protected override MessageHubConfiguration ConfigureHost(MessageHubConfiguration configuration)
    {

        return base.ConfigureHost(configuration)
                .AddData(
                    data => data.FromConfigurableDataSource
                    (
                        nameof(DataSource),
                        source => source
                            .ConfigureTypesForImport()
                    )
                )
                .AddImport(
                    data => data
                        .FromHub(new HostAddress(), source => source
                            .ConfigureTypesForImport()
                        ),
                    import => import
                        .WithFormat(Cashflows, format => format
                            .WithAutoMappings()
                            .WithImportFunction(MapCashflows)
                        )
                )
            ;
    }

    private static IEnumerable<object> MapCashflows(ImportRequest request, IDataSet dataSet, IMessageHub hub, IWorkspace workspace)
    {
        var importedInstance = workspace.GetData<TestDomain.TransactionalData>().ToArray();
        return importedInstance.Select(i => new TestDomain.ComputedData(i.Id, i.LoB, i.BusinessUnit, 2 * i.Value));
    }

    [Fact]
    public async Task TestVanilla()
    {
        var client = GetClient();
        var importRequest = new ImportRequest(VanillaCsv);
        var importResponse = await client.AwaitResponse(importRequest, o => o.WithTarget(new HostAddress()));
        importResponse.Message.Log.Status.Should().Be(ActivityLogStatus.Succeeded);

        var items = await client.AwaitResponse(new GetManyRequest<TestDomain.TransactionalData>(),
            o => o.WithTarget(new HostAddress()));
        items.Message.Items.Should().HaveCount(4)
            .And.ContainSingle(i => i.Id == "1")
            .Which.Value.Should().BeApproximately(1.5d, 1e-5); // we started with 7....
        items.Message.Items.Should().ContainSingle(i => i.Id == "2").Which.LoB.Should().Be("1");
    }

    private const string VanillaCsv =
@"@@TransactionalData
Id,LoB,BusinessUnit,Value
1,1,1,1.5
2,1,2,2
3,2,1,3
4,2,2,4
";

    [Fact]
    public async Task TestCashflows()
    {
        var client = GetClient();
        var importRequest = new ImportRequest(VanillaCsv) { Format = Cashflows };
        var importResponse = await client.AwaitResponse(importRequest, o => o.WithTarget(new HostAddress()));
        importResponse.Message.Log.Status.Should().Be(ActivityLogStatus.Succeeded);
        
        var items = await client.AwaitResponse(new GetManyRequest<TestDomain.ComputedData>(),
            o => o.WithTarget(new HostAddress()));
        items.Message.Items.Should().HaveCount(4)
            .And.ContainSingle(i => i.Id == "1")
            .Which.Value.Should().BeApproximately(3.0d, 1e-5); // computed as 2*1.5
    }
}