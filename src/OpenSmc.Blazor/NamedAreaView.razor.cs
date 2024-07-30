﻿using System.Reactive.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using OpenSmc.Layout;

namespace OpenSmc.Blazor;

public partial class NamedAreaView
{
    private IDisposable subscription;
    [Inject]
    private ILogger<LayoutArea> Logger { get; set; }
    private UiControl RootControl { get; set; }


    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        subscription = Stream.GetControlStream(Area)
            .DistinctUntilChanged()
            .Subscribe(item => InvokeAsync(() => Render(item as UiControl)));
    }

    private void Render(UiControl control)
    {
        Logger.LogDebug(
            "Changing area {Area} to {Instance}",
            Area,
            control
        );
        RootControl = control;
        StateHasChanged();
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;

    }
}
