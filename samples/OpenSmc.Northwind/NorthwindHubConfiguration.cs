﻿using System.Reflection;
using OpenSmc.Data;
using OpenSmc.Messaging;
using static OpenSmc.Data.DataPluginExtensions;
using static OpenSmc.Import.ImportExtensions;

namespace OpenSmc.Northwind;

public static class NorthwindHubConfiguration
{
    private static readonly Assembly NorthwindAssembly = typeof(NorthwindHubConfiguration).Assembly;

    public static MessageHubConfiguration AddNorthwindReferenceData(
        this MessageHubConfiguration configuration
    )
    {
        return configuration
            .AddImport()
            .AddData(data =>
                data.FromEmbeddedResource<Category>(
                        "Files.categories.csv",
                        config => config.WithType<Category>()
                    )
                    .FromEmbeddedResource<Region>(
                        "Files.regions.csv",
                        config => config.WithType<Region>()
                    )
                    .FromEmbeddedResource<Territory>(
                        "Files.territories.csv",
                        config => config.WithType<Territory>()
                    )
            );
    }

    public static MessageHubConfiguration AddNorthwindOrders(
        this MessageHubConfiguration configuration
    )
    {
        return configuration
            .AddImport()
            .AddData(data =>
                data.FromEmbeddedResource<Order>(
                    "Files.orders.csv",
                    config => config.WithType<Order>()
                )
            );
    }

    public static MessageHubConfiguration AddNorthwindSuppliers(
        this MessageHubConfiguration configuration
    )
    {
        return configuration
            .AddImport()
            .AddData(data =>
                data.FromEmbeddedResource<Supplier>(
                    "Files.suppliers.csv",
                    config => config.WithType<Supplier>()
                )
            );
    }

    public static MessageHubConfiguration AddNorthwindEmployees(
        this MessageHubConfiguration configuration
    )
    {
        return configuration
            .AddImport()
            .AddData(data =>
                data.FromEmbeddedResource<Employee>(
                    "Files.employees.csv",
                    config => config.WithType<Employee>()
                )
            );
    }

    public static MessageHubConfiguration AddNorthwindProducts(
        this MessageHubConfiguration configuration
    )
    {
        return configuration
            .AddImport()
            .AddData(data =>
                data.FromEmbeddedResource<Product>(
                    "Files.products.csv",
                    config => config.WithType<Product>()
                )
            );
    }

    public static TDataSource AddNorthwindDomain<TDataSource>(this TDataSource dataSource)
        where TDataSource : DataSource<TDataSource>
    {
        return dataSource
            .AddNorthwindReferenceData()
            .WithType<Order>()
            .WithType<Supplier>()
            .WithType<Employee>()
            .WithType<Product>();
    }

    public static TDataSource AddNorthwindReferenceData<TDataSource>(this TDataSource dataSource)
        where TDataSource : DataSource<TDataSource>
    {
        return dataSource.WithType<Category>().WithType<Region>().WithType<Territory>();
    }

    public static IMessageHub GetCustomerHub(this IServiceProvider serviceProvider, object address)
    {
        return serviceProvider.CreateMessageHub(address, config => config.AddData(data => data));
    }
}

public class ReferenceDataAddress;

public class CustomerAddress;

public class ProductAddress;

public class EmployeeAddress;

public class OrderAddress;

public class ShipperAddress;

public class SupplierAddress;
