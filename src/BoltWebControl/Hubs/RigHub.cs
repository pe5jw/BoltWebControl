using BoltCat.Core;
using Microsoft.AspNetCore.SignalR;

namespace BoltWebControl.Hubs;

/// <summary>
/// SignalR hub — clients verbinden en ontvangen rig-updates via RigPollerService.
/// Clients kunnen ook direct commando's sturen (SetFreq, SetMode, SetPtt).
/// </summary>
public class RigHub : Hub
{
    private readonly RigctldClient _rig;

    public RigHub(RigctldClient rig) => _rig = rig;

    public async Task SetFreq(long hz)
        => await _rig.SetFrequencyAsync(hz);

    public async Task SetMode(string mode)
        => await _rig.SetModeAsync(mode);

    public async Task SetPtt(bool on)
        => await _rig.SetPttAsync(on);
}
