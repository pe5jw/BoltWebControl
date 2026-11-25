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
///
/// Miniaudio werkt op 48 kHz; SIPSorcery's ExternalAudioSourceRawSample
/// accepteert Rate8KHz of Rate16KHz. We downsamplen 48→16 kHz (factor 3)
/// vóór het doorgeven — simpele decimation, voldoende voor spraak.
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

        // ── SIPSorcery audio source ───────────────────────────────────
        // AudioSourcesEnum heeft geen ExternalInput; gebruik Silence als
        // placeholder. ExternalAudioSourceRawSample werkt onafhankelijk.
        var audioEncoder = new AudioEncoder();
        var audioSource  = new AudioExtrasSource(audioEncoder,
            new AudioSourceOptions { AudioSource = AudioSourcesEnum.Silence });

        // Koppel miniaudio capture → SIPSorcery external input
        // SIPSorcery accepteert Rate8KHz of Rate16KHz.
        // Miniaudio levert 48 kHz → downsample 3:1 naar 16 kHz.
        audio.OnCaptureFrame += pcm =>
        {
            var pcm16 = Downsample3x(pcm);                    // 480 → 160 samples
            audioSource.ExternalAudioSourceRawSample(
                AudioSamplingRatesEnum.Rate16KHz, 10, pcm16); // 10 ms @ 16 kHz
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
        // Decodeer Opus naar PCM en stuur naar de soundcard.
        // Gebruik het eerste formaat dat de encoder ondersteunt (Opus voor WebRTC).
        var opusFormat = audioEncoder.SupportedFormats.First();
        peerConn.OnRtpPacketReceived += (_, media, pkt) =>
        {
            if (media != SDPMediaTypesEnum.audio) return;
            var decoded = audioEncoder.DecodeAudio(pkt.Payload, opusFormat);
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

        // SDP offer verwerken — RTCSdpType is in SIPSorcery.Net
        peerConn.setRemoteDescription(new RTCSessionDescriptionInit
            { type = RTCSdpType.offer, sdp = offerSdp });

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

    // ── helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Simpele 3:1 decimation: neem elk 3e sample (48 kHz → 16 kHz).
    /// Voldoende voor radio spraak; geen anti-aliasing filter nodig
    /// omdat Opus toch low-pass filtert op de encoder.
    /// </summary>
    private static short[] Downsample3x(short[] input)
    {
        int outLen = input.Length / 3;
        var output = new short[outLen];
        for (int i = 0; i < outLen; i++)
            output[i] = input[i * 3];
        return output;
    }

    private record Session(
        RTCPeerConnection   Peer,
        MiniAudioDevice     Audio,
        AudioExtrasSource   AudioSource);
}
