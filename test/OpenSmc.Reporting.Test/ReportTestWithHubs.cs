﻿using FluentAssertions;
using OpenSmc.Collections;
using OpenSmc.Hub.Fixture;
using OpenSmc.TestDomain.SimpleData;
using Xunit.Abstractions;
using OpenSmc.Messaging;
using OpenSmc.Data;
using OpenSmc.Data.TestDomain;
using OpenSmc.DataCubes;
using OpenSmc.Pivot.Builder;
using OpenSmc.Scopes;
using OpenSmc.Scopes.DataCubes;
using OpenSmc.TestDomain;

namespace OpenSmc.Reporting.Test;

//public static class PivotRegistryExtensions
//{
//    public static MessageHubConfiguration AddPivot(this MessageHubConfiguration conf)
//    {
//        return conf.WithServices(services => services
//            .AddArithmetics()
//            .RegisterScopes());
//    }

//    // TODO V10: move to pivot project (08.02.2024, Ekaterina Mishina)
//    public static MessageHubConfiguration AddPivot2(this MessageHubConfiguration conf)
//    {
//        return conf
//            .AddArithmetics()
//            .AddDataCubes()
//            .AddScopesDataCubes()
//            .AddScopes();
//    }
//}

public static class DataExtensions
{
    public static MessageHubConfiguration ConfigureDataForReport(this MessageHubConfiguration parent)
        => parent.WithHostedHub(
            new ReportDataAddress(parent.Address),
            configuration => configuration
                .AddData(data => data
                    .FromConfigurableDataSource
                    (
                        "DataForReport",
                        dataSource => dataSource
                            .WithType<ValueWithHierarchicalDimension>(t => t
                                .WithKey(x => x.DimA)
                                .WithInitialData(ValueWithHierarchicalDimension.Data)
                            )
                    )
                )
        );
    
    // TODO V10: think of moving scopes and data cubes registration to reporting plugin (05.03.2024, Ekaterina Mishina)
    public static MessageHubConfiguration ConfigureReportingHub(this MessageHubConfiguration parent)
        => parent.WithHostedHub(new ReportingAddress(parent.Address), config => config
            .WithServices(services => services.RegisterScopes())
            .AddScopesDataCubes()
            .AddReporting(data => data
                    .FromHub(new ReportDataAddress(parent.Address),
                        dataSource => dataSource
                            .WithType<ValueWithHierarchicalDimension>()
                    ),
                reportConfig => reportConfig
                    .Set<ValueWithHierarchicalDimension>(t => t
                        .WithData(w => w.GetData<ValueWithHierarchicalDimension>().ToDataCube().RepeatOnce())
                        .WithReportBuilder(b => b.WithQuerySource(new StaticDataFieldQuerySource())
                            .SliceRowsBy(nameof(ValueWithHierarchicalDimension.DimA))
                            .ToTable()
                            .WithOptions(rm => rm.HideRowValuesForDimension("DimA", x => x.ForLevel(1)))
                            .WithOptions(o => o.AutoHeight())))
            ));
}

public class ReportTestWithHubs(ITestOutputHelper output) : HubTestBase(output)
{
    protected override MessageHubConfiguration ConfigureHost(MessageHubConfiguration configuration)
    {
        return base.ConfigureHost(configuration)
                .ConfigureDataForReport()
                .ConfigureReportingHub()
            ;
    }

    [Fact]
    public async Task SimpleReport()
    {
        // currency conversion on the fly according to exchange rate => AddData(raw values, exchange rates), scopes factory register scopes factory, resolve in plugin, x => x (raw data => data cube), slice options
        var client = GetClient();
        var reportRequest = new ReportRequest();

        // act
        var reportResponse = await client.AwaitResponse(reportRequest, o => o.WithTarget(new ReportingAddress(new HostAddress())));

        // assert
        reportResponse.Message.GridOptions.Should().NotBeNull();

        var data = ValueWithHierarchicalDimension.Data.ToDataCube().RepeatOnce();
        // TODO V10: move this to report config (configure report hub) (05.03.2024, Ekaterina Mishina)
        DataCubePivotBuilder<IDataCube<ValueWithHierarchicalDimension>, ValueWithHierarchicalDimension, ValueWithHierarchicalDimension, ValueWithHierarchicalDimension> dataCubePivotBuilder = PivotFactory.ForDataCubes(data);
        var dataCubeReportBuilder = dataCubePivotBuilder
            .WithQuerySource(new StaticDataFieldQuerySource())
            .SliceRowsBy(nameof(ValueWithHierarchicalDimension.DimA))
            .ToTable()
            .WithOptions(rm => rm.HideRowValuesForDimension("DimA", x => x.ForLevel(1)))
            .WithOptions(o => o.AutoHeight());
        var gridOptions = dataCubeReportBuilder
            .Execute();

        //await gridOptions.Verify("HierarchicalDimensionHideAggregation.json");
    }
    
}

