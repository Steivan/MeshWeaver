using OpenSmc.Messaging;

namespace OpenSmc.Northwind.Application;

public static class HubConfiguration
{
    public static MessageHubConfiguration ConfigureNorthwindHubs(
        this MessageHubConfiguration configuration
    )
    {
        return configuration
            .AddNorthwindViews()
            .AddNorthwindEmployees()
            .AddNorthwindOrders()
            .AddNorthwindSuppliers()
            .AddNorthwindProducts()
            .AddNorthwindCustomers();
        // .WithRoutes(forward =>
        //     forward
        //         .RouteAddressToHostedHub<ReferenceDataAddress>(c =>
        //             c.AddNorthwindReferenceData()
        //         )
        //         .RouteAddressToHostedHub<EmployeeAddress>(c => c.AddNorthwindEmployees())
        //         .RouteAddressToHostedHub<OrderAddress>(c =>    c.AddNorthwindOrders())
        //         .RouteAddressToHostedHub<SupplierAddress>(c => c.AddNorthwindSuppliers())
        //         .RouteAddressToHostedHub<ProductAddress>(c =>  c.AddNorthwindProducts())
        //         .RouteAddressToHostedHub<CustomerAddress>(c => c.AddNorthwindCustomers())
        // );
        ;
    }
}
