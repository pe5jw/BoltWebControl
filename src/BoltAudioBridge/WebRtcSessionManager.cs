using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using BoltAudioBridge.Hubs;

namespace BoltAudioBridge;

/// <summary>
/// Beheert WebRTC peer connections.
///
/// Browser ↔ SIPSorcery RTCPeerConnection ↔ MiniAudioDevice ↔ soundcard
///
/// Per verbinding:
///   - één RTCPeerConnection (SIPSorcery)
///   - één MiniAudioDevice (duplex: capture + playback)
///   - AudioEncoder voor Opus (ingebouwd in SIPSorcery)
/// </summary>
public class WebRtcSessionManager(
    ILogger<WebRtcSessionManager> log,
    IHubContext<AudioHub> hub) : IDisposable
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    // ── publieke API (aangeroepen vanuit AudioHub) ─────────────────────

    /// <summary>
    /// Browser stuurt SDP offer + gekozen device IDs.
    /// Geeft SDP answer terug; ICE candidates volgen asynchroon via SignalR.
    /// </summary>
    public async Task<string> HandleOfferAsync(
        string connectionId,
        string offerSdp,
        string inputDeviceId,
        string outputDeviceId)
    {
        // Ruim eventuele oude sessie op
        CloseSession(connectionId);

        // ── miniaudio device starten ──────────────────────────────────
        var audio = new MiniAudioDevice();
        audio.Start(
            captureDeviceName:  inputDeviceId  == "default" ? null : inputDeviceId,
            playbackDeviceName: outputDeviceId == "default" ? null : outputDeviceId);

        // ── SIPSorcery audio source: levert PCM van soundcard aan WebRTC ──
        var audioEncoder = new AudioEncoder();
        var audioSource  = new AudioExtrasSource(audioEncoder,
            new AudioSourceOptions { AudioSource = AudioSourcesEnum.ExternalInput });

        // Koppel miniaudio capture → SIPSorcery external input
        audio.OnCaptureFrame += pcm =>
        {
            // SIPSorcery verwacht PCM als short[] @ 48 kHz mono
            audioSource.ExternalAudioSourceRawSample(
                AudioSamplingRatesEnum.Rate48KHz, 480, pcm);
        };

        // ── WebRTC peer connection ────────────────────────────────────
        var peerConn = new RTCPeerConnection(new RTCConfiguration
        {
            // Geen STUN/TURN nodig: BoltTunnel zorgt voor de tunnel
            iceServers = []
        });

        var audioTrack = new MediaStreamTrack(
            audioSource.GetAudioSourceFormats(),
            MediaStreamStatusEnum.SendRecv);
        peerConn.addTrack(audioTrack);

        // Browser audio (mic) aankomt → soundcard TX
        peerConn.OnRtpPacketReceived += (_, media, pkt) =>
        {
            if (media != SDPMediaTypesEnum.audio) return;
            // AudioFormat.OpusWebRTC = payload type 111, 48kHz, 2ch, useinbandfec=1
            var decoded = audioEncoder.DecodeAudio(pkt.Payload, AudioFormat.OpusWebRTC);
            if (decoded is { Length: > 0 })
                audio.SendPlaybackFrame(decoded);
        };

        // ICE candidates asynchroon naar browser sturen
        peerConn.onicecandidate += candidate =>
        {
            if (candidate is null) return; // end-of-candidates
            _ = hub.Clients.Client(connectionId).SendAsync(
                "IceCandidate",
                candidate.candidate,
                candidate.sdpMid,
                candidate.sdpMLineIndex);
        };

        peerConn.onconnectionstatechange += state =>
        {
            log.LogInformation("[{Id}] WebRTC staat: {State}", connectionId, state);
            if (state is RTCPeerConnectionState.failed or RTCPeerConnectionState.closed)
                CloseSession(connectionId);
        };

        // SDP offer verwerken
        peerConn.setRemoteDescription(new RTCSessionDescriptionInit
            { type = SdpType.offer, sdp = offerSdp });

        await audioSource.StartAudio();

        var answer = peerConn.createAnswer();
        await peerConn.setLocalDescription(answer);

        _sessions[connectionId] = new Session(peerConn, audio, audioSource);

        log.LogInformation("[{Id}] Sessie gestart — in={In} out={Out}",
            connectionId, inputDeviceId, outputDeviceId);

        return answer.sdp;
    }

    public void HandleIceCandidate(string connectionId, string candidate, string sdpMid, int sdpMLineIndex)
    {
        if (_sessions.TryGetValue(connectionId, out var s))
            s.Peer.addIceCandidate(new RTCIceCandidateInit
            {
                candidate      = candidate,
                sdpMid         = sdpMid,
                sdpMLineIndex  = (ushort)sdpMLineIndex
            });
    }

    public void CloseSession(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var s))
        {
            s.Peer.close();
            s.Audio.Dispose();
            log.LogInformation("[{Id}] Sessie gesloten", connectionId);
        }
    }

    public void Dispose()
    {
        foreach (var key in _sessions.Keys)
            CloseSession(key);
    }

    private record Session(
        RTCPeerConnection   Peer,
        MiniAudioDevice     Audio,
        AudioExtrasSource   AudioSource);
}
