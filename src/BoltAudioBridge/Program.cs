using BoltAudioBridge;
using BoltAudioBridge.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<AudioDeviceService>();
builder.Services.AddSingleton<WebRtcSessionManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// SignalR hub voor WebRTC signaling
app.MapHub<AudioHub>("/hub/audio");

// REST: server-kant audio devices ophalen
app.MapGet("/api/audio/devices", (AudioDeviceService svc) => svc.GetDevices());

app.Run();
