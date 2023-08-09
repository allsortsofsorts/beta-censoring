using BetaCensor.Core;
using BetaCensor.Core.Messaging;
using BetaCensor.Server;
using BetaCensor.Server.Discovery;
using BetaCensor.Web;
using BetaCensor.Web.Status;
using BetaCensor.Workers;
using CensorCore;
using CensorCore.ModelLoader;
using ConfigurEngine;
using MediatR;
using Microsoft.AspNetCore.Http.Json;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:2382");
// builder.WebHost.AdvertiseServer();

builder.Configuration
    .AddConfigFile("config")
    .AddConfigFile("stickers")
    .AddConfigFile("beta-config");

builder.Host.UseSystemd();
builder.Host.UseWindowsService();

//TODO: while effective (and clean), this is kind of a bad hack
// we should add a runtimeSettings.json, use the csproj to include it as an embedded resource
// then use AddJsonStream to pull it back out. Then it's still a JSON config like appSettings
// but we don't have to include a loose JSON file.
builder.Logging.AddFilter("Microsoft.AspNetCore", level => level > LogLevel.Warning);

// Add services to the container.

var loader = new ModelLoaderBuilder()
        .AddDefaultPaths()
        .SearchAssembly(System.Reflection.Assembly.GetEntryAssembly())
        .Build();
var model = await loader.GetModel();
if (model == null) throw new InvalidDataException("Failed to retrieve AI model! Aborting...");
builder.Services.AddCensoring(model);
builder.Services.AddSingleton<CensorCore.Censoring.ICensoringMiddleware, BetaCensor.Core.ObfuscationMiddleware>();

var serverOpts = builder.Configuration.GetServerOptions();
serverOpts ??= new ServerOptions();

builder.Services.AddSingleton<IImageHandler>(ServerConfigurationExtensions.BuildImageHandler(serverOpts));

// builder.Services.AddSpaStaticFiles(options => {options.RootPath = "wwwroot";});
var mvc = builder.Services.AddControllers(mvc => mvc.AddYamlFormatter()).AddJsonOptions(json => json.JsonSerializerOptions.ConfigureJsonOptions());
if (serverOpts.EnableRest) {
    Console.WriteLine("Enabling REST interface!");
    mvc.AddApplicationPart(typeof(CensorCore.Web.CensoringController).Assembly);
}
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddStatusPages<ServerInfoService>(builder.Environment);

builder.Services.AddMediatR(
    typeof(Program), 
    typeof(AIService), 
    typeof(BetaCensor.Core.Messaging.CensorImageRequest), 
    typeof(BetaCensor.Web.Controllers.InfoController)
);

builder.Services.AddPerformanceData();

if (serverOpts.EnableSignalR) {
    Console.WriteLine("Enabling SignalR interface!");
    builder.Services.AddSignalR(o =>
    {
        o.EnableDetailedErrors = true;
        o.KeepAliveInterval = TimeSpan.FromSeconds(10);
        o.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
    })
    .AddJsonProtocol(options => options.PayloadSerializerOptions.ConfigureJsonOptions())
    .AddHubOptions<BetaCensor.Server.Controllers.CensoringHub>(o =>
    {
        o.MaximumParallelInvocationsPerClient = 8;
        o.MaximumReceiveMessageSize = serverOpts.EnableLargeMessages ? null : 33554432;
    });
}

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !string.IsNullOrWhiteSpace(serverOpts.SocketPath)) {
    builder.WebHost.ConfigureKestrel(k =>
    {
        k.ListenUnixSocket(serverOpts.SocketPath);
    });
}

builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.ConfigureJsonOptions());

builder.Services.AddQueues<CensorImageRequest, CensorImageResponse>();
builder.Services.AddDefaultManagedRequestQueue();
// we don't want to validate this at the queue level because it would get *silently* dropped, not rejected.
// if it happens anywhere, it should be in the individual APIs before being queued.
// builder.Services.AddDefaultManagedRequestQueue((req => req.ImageUrl, url => (url ?? string.Empty).EndsWith(".gif")));
builder.Services.AddWorkers<DispatchWorkerService<CensorImageRequest, CensorImageResponse>>(builder.Configuration.GetSection("Server"));
builder.Services.AddWorkers<DispatchNotificationService<CensorImageResponse>>(1);

builder.Services.AddHostedService<DiscoveryService>();

// var stickerOpts = builder.Configuration.GetSection("Stickers");
// var captionsOpts = builder.Configuration.GetSection("Captions");
builder.Services.AddStickerService(builder.Environment);

builder.Services.AddScoped<MatchOptions>(ServerConfigurationExtensions.BuildMatchOptions);
builder.Services.AddSingleton<CensorCore.Censoring.GlobalCensorOptions>(ServerConfigurationExtensions.BuildCensorOptions);
builder.Services.AddSingleton<DiscordOverrides>();

var app = builder.Build();

// if (app.Environment.IsDevelopment()) {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseRouting();
// app.UseAuthorization();
app.UseEndpoints(e =>
{
    if (serverOpts.EnableSignalR) {
        e.MapHub<BetaCensor.Server.Controllers.CensoringHub>("/live", conf => {
            conf.ApplicationMaxBufferSize = 33554432;
            conf.TransportMaxBufferSize = 33554432;
        });
    }
});
// app.MapHub<BetaCensor.Server.Controllers.CensoringHub>("/live");
app.StartDiscordLoop();
app.MapControllers();
app.UseWebSockets();
app.UseStatusPages(app.Environment);
app.UseStickerProvider();
app.Run();
