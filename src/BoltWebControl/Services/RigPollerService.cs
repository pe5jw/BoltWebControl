using BoltCat.Core;
using BoltWebControl.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BoltWebControl.Services;

/// <summary>
/// Achtergrondservice: pollt rigctld elke 500 ms en pusht RigState naar alle SignalR clients.
/// </summary>
public class RigPollerService : BackgroundService
{
    private readonly RigctldClient         _rig;
    private readonly IHubContext<RigHub>   _hub;
    private readonly ILogger<RigPollerService> _log;
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(500);

    public RigPollerService(RigctldClient rig, IHubContext<RigHub> hub, ILogger<RigPollerService> log)
    {
        _rig = rig;
        _hub = hub;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("RigPoller gestart — pollt elke {ms} ms", _interval.TotalMilliseconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = await _rig.GetStateAsync(ct);
                await _hub.Clients.All.SendAsync("RigUpdate", state, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning("rigctld poll mislukt: {msg}", ex.Message);
            }

            await Task.Delay(_interval, ct);
        }
    }
}
