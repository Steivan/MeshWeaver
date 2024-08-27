﻿using System.Diagnostics;
using Microsoft.FluentUI.AspNetCore.Components;
using MeshWeaver.Hosting;
using MeshWeaver.Portal;
using MeshWeaver.Portal.Web;
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddFluentUIComponents();
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

builder.Services.AddFluentUIComponents();

builder.Host.UseMeshWeaver(
    new BlazorServerAddress(),
    config => config.ConfigurePortalHubs()
);

if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseStaticWebAssets();
}

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting blazor server on PID: {PID}", Process.GetCurrentProcess().Id);

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. Yoseru may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

public record BlazorServerAddress
{
    public Guid Id { get; init; } = Guid.NewGuid();
}