namespace BoltAudioBridge;

/// <summary>
/// Beheert WebRTC peer connections via SIPSorcery.
/// Browser ↔ SIPSorcery peer ↔ miniaudio (soundcard).
///
/// Flow:
///   1. Browser stuurt offer SDP via SignalR
///   2. Server maakt RTCPeerConnection (SIPSorcery)
///   3. Server koppelt miniaudio capture → WebRTC audio track (TX)
///   4. Server speelt WebRTC audio track af via miniaudio playback (RX)
///   5. Server stuurt answer SDP + ICE candidates terug via SignalR
/// </summary>
public class WebRtcSessionManager(ILogger<WebRtcSessionManager> logger)
{
    // TODO: implement SIPSorcery RTCPeerConnection + miniaudio bridge
    public Task<string> CreateOfferAnswerAsync(string offerSdp, string inputDeviceId, string outputDeviceId)
    {
        logger.LogInformation("WebRTC session aangevraagd: in={In} out={Out}", inputDeviceId, outputDeviceId);
        return Task.FromResult("answer-sdp-placeholder");
    }
}
