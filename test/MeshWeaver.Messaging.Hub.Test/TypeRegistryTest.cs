﻿using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MeshWeaver.Hub.Fixture;
using MeshWeaver.Messaging.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace MeshWeaver.Messaging.Hub.Test;

public class TypeRegistryTest(ITestOutputHelper output) : HubTestBase(output)
{
    record SayHelloRequest : IRequest<HelloEvent>;

    record HelloEvent;

    private record GenericRequest<T>(T Value);

    protected override MessageHubConfiguration ConfigureHost(
        MessageHubConfiguration configuration
    ) => configuration.WithTypes(typeof(GenericRequest<>));

    [Fact]
    public async Task GenericTypes()
    {
        var host = GetHost();
        await host.HasStarted;

        var typeRegistry = host.ServiceProvider.GetRequiredService<ITypeRegistry>();
        var canMap = typeRegistry.TryGetTypeName(typeof(GenericRequest<int>), out var typeName);
        canMap.Should().BeTrue();
        typeName.Should().Be("MeshWeaver.Messaging.Hub.Test.TypeRegistryTest.GenericRequest`1[Int32]");

        canMap = typeRegistry.TryGetType(typeName, out var mappedType);
        canMap.Should().BeTrue();
        mappedType.Should().Be(typeof(GenericRequest<int>));
    }
}