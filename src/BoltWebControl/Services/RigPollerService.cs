using BoltCat.Core;
using BoltWebControl.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BoltWebControl.Services;

/// <summary>
/// Achtergrondservice: pollt rigctld elke 500 ms en pusht naar SignalR clients.
/// Stuurt RigUpdate bij succes, RigOffline bij verbindingsfout.
/// </summary>
public class RigPollerService : BackgroundService
{
    private readonly RigctldClient            _rig;
    private readonly IHubContext<RigHub>      _hub;
    private readonly ILogger<RigPollerService> _log;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(500);
    private bool _lastOk = true;   // voor rate-limiting van RigOffline berichten

    public RigPollerService(RigctldClient rig, IHubContext<RigHub> hub, ILogger<RigPollerService> log)
    {
        _rig = rig; _hub = hub; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("RigPoller gestart — pollt elke {ms} ms", _interval.TotalMilliseconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = await _rig.GetStateAsync(ct);
                if (!_lastOk)
                {
                    _log.LogInformation("rigctld weer bereikbaar");
                    _lastOk = true;
                }
                await _hub.Clients.All.SendAsync("RigUpdate", state, cancellationToken: ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (_lastOk)
                {
                    _log.LogWarning("rigctld niet bereikbaar: {msg}", ex.Message);
                    _lastOk = false;
                }
                // Stuur RigOffline naar clients (alleen als status verandert → eerste keer)
                try
                {
                    await _hub.Clients.All.SendAsync("RigOffline", "niet bereikbaar", cancellationToken: ct);
                }
                catch { /* hub zelf ook weg */ }
            }

            await Task.Delay(_interval, ct);
        }
    }
}
