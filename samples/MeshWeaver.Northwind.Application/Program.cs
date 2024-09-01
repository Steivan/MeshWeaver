﻿using System.Diagnostics;
using MeshWeaver.Application;
using MeshWeaver.Blazor.AgGrid;
using MeshWeaver.Blazor.ChartJs;
using MeshWeaver.Hosting;
using MeshWeaver.Hosting.Blazor;
using MeshWeaver.Portal.ServiceDefaults;
using Microsoft.Extensions.Logging.Console;
using MeshWeaver.Hosting.Monolith;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddSingleton<ConsoleFormatter, CsvConsoleFormatter>();
builder.Services.Configure<CsvConsoleFormatterOptions>(options =>
{
    options.TimestampFormat = "hh:mm:ss:fff";
    options.IncludeTimestamp = true;
});
builder.Services.AddLogging(config => config.AddConsole(
    options =>
    {
        options.FormatterName = nameof(CsvConsoleFormatter);
    }).AddDebug());


builder.AddMeshWeaver(
    new UiAddress(),
    config => config    
        .AddBlazor(x =>
        x.AddChartJs()
            .AddAgGrid()
    )
        .AddMonolithMesh()
);


if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseStaticWebAssets();
}

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting blazor server on PID: {PID}", Process.GetCurrentProcess().Id);

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

