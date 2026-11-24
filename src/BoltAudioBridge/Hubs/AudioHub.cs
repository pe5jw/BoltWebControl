using Microsoft.AspNetCore.SignalR;

namespace BoltAudioBridge.Hubs;

/// <summary>
/// SignalR hub voor WebRTC signaling (offer/answer/ICE).
/// Browser verbindt hier voor audio setup.
/// </summary>
public class AudioHub(WebRtcSessionManager sessions) : Hub
{
    /// <summary>Browser stuurt SDP offer + gekozen device IDs.</summary>
    public async Task<string> Offer(string sdp, string inputDeviceId, string outputDeviceId)
    {
        var answer = await sessions.CreateOfferAnswerAsync(sdp, inputDeviceId, outputDeviceId);
        return answer;
    }

    /// <summary>Browser stuurt ICE candidate.</summary>
    public Task IceCandidate(string candidate, string sdpMid, int sdpMLineIndex)
    {
        // TODO: forward naar SIPSorcery peer
        return Task.CompletedTask;
    }
}
