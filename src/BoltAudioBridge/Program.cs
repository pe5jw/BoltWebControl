using BoltAudioBridge;
using BoltAudioBridge.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<AudioDeviceService>();
builder.Services.AddSingleton<WebRtcSessionManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapSignalR<AudioHub>("/hub/audio");
app.MapGet("/api/audio/devices", (AudioDeviceService svc) => svc.GetDevices());

app.Run();
