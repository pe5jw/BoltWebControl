using Microsoft.AspNetCore.SignalR;

namespace BoltAudioBridge.Hubs;

/// <summary>
/// SignalR hub voor WebRTC signaling (offer/answer/ICE candidates).
/// Elke browser verbinding krijgt een eigen WebRTC sessie + MiniAudioDevice.
/// </summary>
public class AudioHub(WebRtcSessionManager sessions, ILogger<AudioHub> log) : Hub
{
    /// <summary>Browser stuurt SDP offer + gekozen device IDs → answer SDP terug.</summary>
    public Task<string> Offer(string sdp, string inputDeviceId, string outputDeviceId)
        => sessions.HandleOfferAsync(Context.ConnectionId, sdp, inputDeviceId, outputDeviceId);

    /// <summary>Browser stuurt ICE candidate → doorgeven aan SIPSorcery peer.</summary>
    public Task IceCandidate(string candidate, string sdpMid, int sdpMLineIndex)
    {
        sessions.HandleIceCandidate(Context.ConnectionId, candidate, sdpMid, sdpMLineIndex);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        log.LogInformation("Verbinding verbroken: {Id}", Context.ConnectionId);
        sessions.CloseSession(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
