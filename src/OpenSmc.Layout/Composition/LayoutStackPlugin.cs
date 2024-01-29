﻿using System.Collections.Immutable;
using System.Reflection;
using OpenSmc.Messaging;
using OpenSmc.Reflection;
using OpenSmc.ServiceProvider;

namespace OpenSmc.Layout.Composition;

public class LayoutStackPlugin(IMessageHub hub) :
    UiControlPlugin<LayoutStackControl>(hub),
    IMessageHandler<SetAreaRequest>,
    IMessageHandler<LayoutStackUpdateRequest>

{
    [Inject] private IUiControlService uiControlService;
    private readonly LayoutDefinition layoutDefinition;

    public LayoutStackPlugin(LayoutDefinition layoutDefinition) : this(layoutDefinition.Hub)
    {
        this.layoutDefinition = layoutDefinition;
    }

    public override LayoutStackControl StartupState()
    {
        if (layoutDefinition?.InitialState == null)
            return null;
        return layoutDefinition.InitialState with { Hub = hub1, Address = hub1.Address };
    }

    private AreaChangedEvent GetArea(string area)
    {
        return Control.Areas.FirstOrDefault(x => x.Area == area);
    }

    protected AreaChangedEvent SetAreaImpl(object view, ViewDefinition viewDefinition, string path, SetAreaOptions options)
    {
        var area = options.Area;
        var deleteView = view == null && viewDefinition == null && path == null;
        var existing = GetArea(area);
        if (existing != null)
        {
            (existing.View as UiControl)?.Hub?.Dispose();
            UpdateState(s => s with { AreasImpl = s.AreasImpl.RemoveAll(a => a.Area == area) });
        }

        if (deleteView)
            return null;

        if (path != null)
            view = GetViewByPath(path, options);

        var control = viewDefinition != null
                          ? Controls.RemoteView(viewDefinition, options)
                          : view as UiControl ?? uiControlService.GetUiControl(view);


        control = CreateUiControlHub(control, area);
        Hub.ConnectTo(control.Hub);
        var ret = new AreaChangedEvent(area, control, options.AreaViewOptions);
        UpdateState(state => state.SetAreaToState(ret));
        Post(ret, x => x.WithTarget(MessageTargets.Subscribers));

        return ret;
    }

    private object GetViewByPath(string path, SetAreaOptions options)
    {
        var request = new RefreshRequest { Path = path, Options = options };
        var generator = layoutDefinition.ViewGenerators.FirstOrDefault(g => g.Filter(request));
        return generator?.Generator(request);
    }


    public override void InitializeState(LayoutStackControl control)
    {
        if(control == null) return;
        base.InitializeState(control);
        var areas = Control.ViewElements
                                 .Select
                                     (
                                      a => a is ViewElementWithView { View: not null } vv
                                                     ? SetAreaImpl(vv.View, null, null, vv.Options)
                                                     : a is ViewElementWithViewDefinition { ViewDefinition: not null } vd
                                                         ? SetAreaImpl(null, vd.ViewDefinition, null, vd.Options)
                                                         :
                                                         a is ViewElementWithPath vp
                                                             ? SetAreaImpl(null, null, vp.Path, vp.Options)
                                                             : new AreaChangedEvent(a.Area, null)

                                     )
                                 .ToArray();


        UpdateState(s => s with { Areas = areas });
    }


    IMessageDelivery IMessageHandler<SetAreaRequest>.HandleMessage(IMessageDelivery<SetAreaRequest> request)
    {
        var areaChanged = SetAreaImpl(request.Message.View, request.Message.ViewDefinition, request.Message.Path, request.Message.Options);
        Post(areaChanged ?? new AreaChangedEvent(request.Message.Area, null), o => o.ResponseFor(request.Message.ForwardedRequest ?? request).WithTarget(MessageTargets.Subscribers));
        return request.Processed();
    }

    protected override IMessageDelivery RefreshView(IMessageDelivery<RefreshRequest> request)
    {
        var areaChanged = string.IsNullOrWhiteSpace(request.Message.Area)
                              ? new AreaChangedEvent(request.Message.Area, State)
                              : State.Areas.FirstOrDefault(x => x.Area == request.Message.Area);

        if (areaChanged == null)
        {
            var view = CreateView(request);
            if (view == null)
            {
                Post(new AreaChangedEvent(request.Message.Area, null), o => o.ResponseFor(request));
                return request.Processed();
            }

            UpdateState(s => s with { AreasImpl = s.AreasImpl.Add(new(request.Message.Area, new SpinnerControl())) });
            return SetArea(request, view);
        }

        Post(areaChanged, o => o.ResponseFor(request));
        return request.Processed();
    }

    protected IMessageDelivery SetArea(IMessageDelivery<RefreshRequest> request, object view)
        => SetArea(request, view, new(request.Message.Area));

    protected IMessageDelivery SetArea(IMessageDelivery request, object view, SetAreaOptions options)
    {
        Post(GetSetAreaRequest(request, options, view), o => o.WithRequestIdFrom(request));
        return request.Processed();
    }

    private static SetAreaRequest GetSetAreaRequest(IMessageDelivery request, SetAreaOptions options, object view)
    {
        if (view is ViewDefinition viewDef)
            return new SetAreaRequest(options, viewDef) { ForwardedRequest = request };
        if (view is Delegate del)
        {
            var returnType = del.Method.ReturnType;
            ViewDefinition viewDefinition;
            if (returnType.IsGenericType && typeof(Task<>).IsAssignableFrom(returnType.GetGenericTypeDefinition()))
            {
                var viewType = returnType.GetGenericArguments().Single();
                if (viewType == typeof(ViewElementWithView))
                {
                    viewDefinition = del.Method.GetParameters().Length switch
                    {
                        0 => async _ => await ((Func<Task<ViewElementWithView>>)del).Invoke(),
                        1 => async o => await ((Func<SetAreaOptions, Task<ViewElementWithView>>)del).Invoke(o),
                        _ => throw new NotSupportedException()
                    };
                }
                else
                {
                    viewDefinition = del.Method.GetParameters().Length switch
                    {
                        0 => o => (Task<ViewElementWithView>)AwaitTaskMethod.MakeGenericMethod(returnType).InvokeAsFunction(o, del.DynamicInvoke()),
                        1 => o => (Task<ViewElementWithView>)AwaitTaskMethod.MakeGenericMethod(returnType).InvokeAsFunction(o, del.DynamicInvoke(o)),
                        _ => throw new NotSupportedException()
                    };


                }

            }
            else
            {
                if (returnType == typeof(ViewElementWithView))
                    viewDefinition = del.Method.GetParameters().Length switch
                    {
                        0 => _ => Task.FromResult((ViewElementWithView)del.DynamicInvoke()),
                        1 => o => Task.FromResult((ViewElementWithView)del.DynamicInvoke(o)),
                        _ => throw new NotSupportedException()
                    };
                else
                    viewDefinition = del.Method.GetParameters().Length switch
                    {
                        0 => o => Task.FromResult<ViewElementWithView>(new(del.DynamicInvoke(), o)),
                        1 => o => Task.FromResult<ViewElementWithView>(new(del.DynamicInvoke(o), o)),
                        _ => throw new NotSupportedException()
                    };
            }

            return new SetAreaRequest(options, viewDefinition);
        }
        return new SetAreaRequest(options, view) { ForwardedRequest = request };
    }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    private static readonly MethodInfo AwaitTaskMethod = ReflectionHelper.GetStaticMethodGeneric(() => AwaitTask<object>(null, null));
    private readonly IMessageHub hub1 = hub;
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    // ReSharper disable once UnusedMethodReturnValue.Local
    private static async Task<ViewElementWithView> AwaitTask<T>(SetAreaOptions options, Task<T> task) => new(await task, options);

    protected virtual object CreateView(IMessageDelivery<RefreshRequest> request)
    {
        var generator = layoutDefinition.ViewGenerators.FirstOrDefault(g => g.Filter(request.Message));
        return generator?.Generator.Invoke(request.Message);
    }



    IMessageDelivery IMessageHandler<LayoutStackUpdateRequest>.HandleMessage(IMessageDelivery<LayoutStackUpdateRequest> request)
    {
        var (updatedView, action) = request.Message;
        action(Hub, State.Areas, updatedView.ViewElements.OfType<ViewElementWithView>());
        return request.Processed();
    }

    public override bool IsDeferred(IMessageDelivery delivery)
    {
        return base.IsDeferred(delivery);
    }
}

internal record ViewGenerator(Func<RefreshRequest, bool> Filter, Func<RefreshRequest, object> Generator);

public record LayoutDefinition(IMessageHub Hub) : MessageHubModuleConfiguration
{
    internal LayoutStackControl InitialState { get; init; }
    internal ImmutableList<ViewGenerator> ViewGenerators { get; init; } = ImmutableList<ViewGenerator>.Empty;
    public LayoutDefinition WithInitialState(LayoutStackControl initialState) => this with { InitialState = initialState };
    public LayoutDefinition WithGenerator(Func<RefreshRequest, bool> filter, Func<RefreshRequest, object> viewGenerator) => this with { ViewGenerators = ViewGenerators.Add(new(filter, viewGenerator)) };

    public LayoutDefinition WithView(string area, Func<RefreshRequest, object> generator) =>
        WithGenerator(r => r.Area == area, generator);

}
