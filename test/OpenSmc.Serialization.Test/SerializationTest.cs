﻿using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Fixture;
using OpenSmc.Messaging;
using OpenSmc.Messaging.Hub;
using OpenSmc.ServiceProvider;
using Xunit;
using Xunit.Abstractions;

namespace OpenSmc.Serialization.Test;

public class SerializationTest : TestBase
{
    record HostAddress;

    [Inject] private IMessageHub<HostAddress> Host { get; set; }

    public SerializationTest(ITestOutputHelper output) : base(output)
    {
        Services.AddSingleton(sp =>
            sp.CreateMessageHub(new HostAddress(),
                hubConf => hubConf
                    .AddSerialization(conf =>
                        conf.ForType<MyEvent>(s =>
                            s.WithMutation((value, context) => context.SetProperty("NewProp", "New"))))));
    }

    [Fact]
    public async Task SimpleTest()
    {
        Host.Post(new MyEvent("Hello"));
        var events = await Host.Out.Timeout(TimeSpan.FromMicroseconds(500)).ToArray();
        events.Should().HaveCount(1);
    }
}


public record HostAddress();
public record MyEvent(string Text);