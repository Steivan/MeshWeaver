﻿using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using OpenSmc.Data;
using OpenSmc.Messaging;
using OpenSmc.ServiceProvider;

namespace OpenSmc.Layout.Composition;

public class LayoutPlugin(LayoutDefinition layoutDefinition) :
    MessageHubPlugin(layoutDefinition.Hub),
    IMessageHandler<RefreshRequest>
{
    [Inject] private IUiControlService uiControlService;
    private readonly LayoutDefinition layoutDefinition;
    private readonly IWorkspace workspace = layoutDefinition.Hub.ServiceProvider.GetRequiredService<IWorkspace>();



    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await workspace.Initialized;
        await base.StartAsync(cancellationToken);

        if (layoutDefinition.InitialState == null)
            return;
        var control = layoutDefinition.InitialState;
        RenderControl(string.Empty, control, new());
        workspace.Commit();

        foreach (var initialization in layoutDefinition.Initializations)
            await initialization.Invoke(cancellationToken);
    }

    private EntityReference RenderControl(string area, UiControl control, RefreshRequest request)
    {
        if (control == null)
            return null;

        if (control is LayoutStackControl stack)
            control = stack with
            {
                Areas = stack.ViewElements
                    .Select(ve => RenderControl($"{area}/{ve.Area}", ParseControl(request, ve), request)).ToArray()
            };

        var layoutArea = new LayoutArea(area, control);
        workspace.Update(layoutArea);
        return workspace.GetReference(layoutArea);
    }

    private UiControl ParseControl(RefreshRequest request, ViewElement a)
        => a switch
        {
            ViewElementWithView ve => uiControlService.GetUiControl(ve.View),
            ViewElementWithViewDefinition ve => uiControlService.GetUiControl(ve.ViewDefinition.Invoke(request)),
            _ => throw new NotSupportedException()
        };


    private UiControl GetControl(RefreshRequest request)
    {
        var generator = layoutDefinition.ViewGenerators.FirstOrDefault(g => g.Filter(request));
        if (generator == null)
            return null;
        var control = uiControlService.GetUiControl(generator.Generator.Invoke(request));
        return control;
    }

    IMessageDelivery IMessageHandler<RefreshRequest>.HandleMessage(IMessageDelivery<RefreshRequest> request)
        => RefreshView(request);




    protected IMessageDelivery RefreshView(IMessageDelivery<RefreshRequest> request)
    {
        if (string.IsNullOrWhiteSpace(request.Message.Area))
            return request.Ignored();

        var area = request.Message.Area;

        DisposeArea(area);


        var layoutArea = GetControl(request.Message);

        var reference = RenderControl(request.Message.Area, layoutArea, request.Message);
        workspace.Commit();
        Hub.Post(new RefreshResponse(reference), o => o.ResponseFor(request));

        return request.Processed();
    }

    private void DisposeArea(string area)
    {
        var areas = workspace.State.GetData<LayoutArea>().Where(a => a.Area.StartsWith(area)).ToArray();
        workspace.Delete(areas);
    }
}

internal record ViewGenerator(Func<RefreshRequest, bool> Filter, ViewDefinition Generator);

public record LayoutDefinition(IMessageHub Hub)
{
    private readonly IUiControlService uiControlService = Hub.ServiceProvider.GetRequiredService<IUiControlService>();
    internal LayoutStackControl InitialState { get; init; }
    internal ImmutableList<ViewGenerator> ViewGenerators { get; init; } = ImmutableList<ViewGenerator>.Empty;
    public LayoutDefinition WithInitialState(LayoutStackControl initialState) => this with { InitialState = initialState };
    public LayoutDefinition WithGenerator(Func<RefreshRequest, bool> filter, ViewDefinition viewGenerator) => this with { ViewGenerators = ViewGenerators.Add(new(filter, viewGenerator)) };

    public LayoutDefinition WithView(string area, Func<RefreshRequest, object> generator) =>
        WithGenerator(r => r.Area == area, r => new LayoutArea(area,uiControlService.GetUiControl(generator.Invoke(r))));


    internal ImmutableList<Func<CancellationToken, Task>> Initializations { get; init; } = ImmutableList<Func<CancellationToken, Task>>.Empty;

    public LayoutDefinition WithInitialization(Func<CancellationToken, Task> func)
        => this with { Initializations = Initializations.Add(func) };
}
