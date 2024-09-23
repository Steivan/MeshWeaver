﻿using MeshWeaver.Application.Styles;

namespace MeshWeaver.Layout;
/// <summary>
/// Represents a number control.
/// </summary>
/// <remarks>
/// For more information, visit the
/// <a href="https://www.fluentui-blazor.net/numberfield">Fluent UI Blazor NumberField documentation</a>.
/// </remarks>
/// <param name="Data">The data associated with the number control.</param>

public record NumberControl(object Data)
    : UiControl<NumberControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion); // TODO V10: Add formatter somehow (2023.09.07, Armen Sirotenko)
/// <summary>
/// Represents a date control.
/// </summary>
/// <remarks>
/// For more information, visit the 
/// <a href="https://www.fluentui-blazor.net/datepicker">Fluent UI Blazor DatePicker documentation</a>.
/// </remarks>
/// <param name="Data">The data associated with the date control.</param>
public record DateControl(object Data)
    : UiControl<DateControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion); // TODO V10: Add date formatter (2023.09.07, Armen Sirotenko)
/// <summary>
/// Represents an exception control.
/// </summary>
/// <remarks>
/// For more information, visit the 
/// <a href="https://www.fluentui-blazor.net/messagebar">Fluent UI Blazor MessageBar documentation</a>.
/// </remarks>
/// <param name="Message">The exception message.</param>
/// <param name="Type">The type of the exception.</param>
public record ExceptionControl(string Message, string Type)
    : UiControl<ExceptionControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion)
{
/// <summary>
    /// Gets or initializes the stack trace of the exception.
    /// </summary>
    public string StackTrace { get; init; }
}

//not in scope of MVP
public record CodeSampleControl(object Data)
    : UiControl<CodeSampleControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion);

public record HtmlControl(object Data)
    : UiControl<HtmlControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion);

public record CheckBoxControl(object Data)
    : UiControl<CheckBoxControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion);

/// <summary>
/// Control representing a progress bar.
/// </summary>
/// <remarks>
/// For more information, visit the 
/// <a href="https://www.fluentui-blazor.net/progressbar">Fluent UI Blazor ProgressBar documentation</a>.
/// </remarks>
/// <param name="Message">String message</param>
/// <param name="Progress">Between 0 and 100</param>
public record ProgressControl(object Message, object Progress)
    : UiControl<ProgressControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion);

public record SliderControl(int Min, int Max, int Step)
    : UiControl<SliderControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion);

public record RedirectControl(object Message, object RedirectAddress, object RedirectArea)
    : UiControl<RedirectControl>(ModuleSetup.ModuleName, ModuleSetup.ApiVersion);
