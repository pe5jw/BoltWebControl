using BoltCat.Core;
using BoltWebControl.Hubs;
using BoltWebControl.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// RigctldClient — host/port uit appsettings of env
builder.Services.AddSingleton<RigctldClient>(sp =>
{
    var cfg  = sp.GetRequiredService<IConfiguration>();
    string h = cfg["Rigctld:Host"] ?? "localhost";
    int    p = int.TryParse(cfg["Rigctld:Port"], out int v) ? v : 4532;
    return new RigctldClient(h, p);
});

// Achtergrondservice die rigctld pollt en via SignalR pusht
builder.Services.AddHostedService<RigPollerService>();

// ── App ───────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseDefaultFiles();   // serveert wwwroot/index.html op /
app.UseStaticFiles();

// REST API
app.MapGet("/api/status", async (RigctldClient rig, CancellationToken ct) =>
{
    try   { return Results.Ok(await rig.GetStateAsync(ct)); }
    catch { return Results.Problem("rigctld niet bereikbaar"); }
});

app.MapPost("/api/freq", async (FreqRequest req, RigctldClient rig, CancellationToken ct) =>
{
    await rig.SetFrequencyAsync(req.Hz, ct);
    return Results.Ok();
});

app.MapPost("/api/mode", async (ModeRequest req, RigctldClient rig, CancellationToken ct) =>
{
    await rig.SetModeAsync(req.Mode, ct);
    return Results.Ok();
});

app.MapPost("/api/ptt", async (PttRequest req, RigctldClient rig, CancellationToken ct) =>
{
    await rig.SetPttAsync(req.On, ct);
    return Results.Ok();
});

// SignalR hub
app.MapHub<RigHub>("/hubs/rig");

app.Run();

// ── Record types voor JSON body ───────────────────────────────────────────
record FreqRequest(long Hz);
record ModeRequest(string Mode);
record PttRequest(bool On);
