using BoltCat.Core;
using BoltWebControl.Hubs;
using BoltWebControl.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// RigSettings — singleton, bevat live host/port/rig
builder.Services.AddSingleton<RigSettings>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new RigSettings
    {
        Host = cfg["Rigctld:Host"] ?? "localhost",
        Port = int.TryParse(cfg["Rigctld:Port"], out int p) ? p : 4532,
        Rig  = cfg["Rigctld:Rig"]  ?? "Auto",
    };
});

// RigctldClient — singleton, Host/Port worden bijgewerkt via RigSettings
builder.Services.AddSingleton<RigctldClient>(sp =>
{
    var s = sp.GetRequiredService<RigSettings>();
    return new RigctldClient(s.Host, s.Port);
});

builder.Services.AddHostedService<RigPollerService>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// ── REST: rig status ──────────────────────────────────────────────────────
app.MapGet("/api/status", async (RigctldClient rig, CancellationToken ct) =>
{
    try   { return Results.Ok(await rig.GetStateAsync(ct)); }
    catch { return Results.Problem("rigctld niet bereikbaar"); }
});

// ── REST: CAT commando's ──────────────────────────────────────────────────
app.MapPost("/api/freq", async (FreqRequest req, RigctldClient rig, CancellationToken ct) =>
{
    await rig.SetFrequencyAsync(req.Hz, ct); return Results.Ok();
});
app.MapPost("/api/mode", async (ModeRequest req, RigctldClient rig, CancellationToken ct) =>
{
    await rig.SetModeAsync(req.Mode, ct); return Results.Ok();
});
app.MapPost("/api/ptt", async (PttRequest req, RigctldClient rig, CancellationToken ct) =>
{
    await rig.SetPttAsync(req.On, ct); return Results.Ok();
});

// ── REST: instellingen lezen & schrijven ──────────────────────────────────
app.MapGet("/api/settings", (RigSettings s) => Results.Ok(new
{
    s.Host, s.Port, s.Rig,
    knownRigs = RigSettings.KnownRigs.Select(r => new { r.Model, r.Name })
}));

app.MapPost("/api/settings", (SettingsRequest req, RigSettings s, RigctldClient rig) =>
{
    if (!string.IsNullOrWhiteSpace(req.Host)) s.Host = req.Host;
    if (req.Port is > 0 and < 65536)          s.Port = req.Port;
    if (!string.IsNullOrWhiteSpace(req.Rig))  s.Rig  = req.Rig;

    // Live bijwerken van de RigctldClient
    rig.Host = s.Host;
    rig.Port = s.Port;

    return Results.Ok(new { s.Host, s.Port, s.Rig });
});

// ── SignalR hub ───────────────────────────────────────────────────────────
app.MapHub<RigHub>("/hubs/rig");

app.Run();

// ── Request records ───────────────────────────────────────────────────────
record FreqRequest(long Hz);
record ModeRequest(string Mode);
record PttRequest(bool On);
record SettingsRequest(string? Host, int Port, string? Rig);
