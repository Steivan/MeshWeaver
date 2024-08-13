﻿using System.Reactive.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MeshWeaver.Layout;

namespace MeshWeaver.Blazor;

public partial class NamedAreaView
{
    [Inject]
    private ILogger<NamedAreaView> Logger { get; set; }
    private UiControl RootControl { get; set; }


    private string DisplayArea { get; set; }
    private bool ShowProgress { get; set; }

    private IDisposable subscription = null;

    private string AreaToBeRendered { get; set; }
    protected override void BindData()
    {
        subscription?.Dispose();
        subscription = null;
        RootControl = null;
        AreaToBeRendered = ViewModel.Area.ToString();
        base.BindData();
        DataBind(ViewModel.DisplayArea, x => x.DisplayArea);
        DataBind(ViewModel.ShowProgress, x => x.ShowProgress);
        if (AreaToBeRendered != null)
            AddBinding(Stream.GetControlStream(AreaToBeRendered)
                .Subscribe(x =>
                {
                    InvokeAsync(() =>
                    {
                        RootControl = (UiControl)x;
                        Logger.LogDebug("Setting area {Area} to rendering area {AreaToBeRendered} to type {Type}", Area,
                            AreaToBeRendered, x?.GetType().Name ?? "null");
                        RequestStateChange();
                    });
                })
            );
    }


}
