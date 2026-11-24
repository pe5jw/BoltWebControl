using System.Runtime.InteropServices;
using Hexa.NET.MiniAudio;

namespace BoltAudioBridge;

/// <summary>
/// Wrapper rond een miniaudio duplex device.
/// Capture (radio RX → browser):  OnCaptureFrame event → SIPSorcery audio track
/// Playback (browser mic → radio TX): SIPSorcery roept SendPlaybackFrame aan
///
/// miniaudio duplex callback loopt op een realtime audio thread — geen allocaties,
/// geen locks. We gebruiken een lock-free ring buffer voor de TX richting.
/// </summary>
public sealed unsafe class MiniAudioDevice : IDisposable
{
    // ── publiek contract ──────────────────────────────────────────────
    /// <summary>Aangroepen door SIPSorcery met PCM van de browser mic (TX richting).</summary>
    public void SendPlaybackFrame(short[] pcm) => _txRing.Write(pcm);

    /// <summary>Vuur af met PCM van de soundcard (RX richting) → SIPSorcery.</summary>
    public event Action<short[]>? OnCaptureFrame;

    // ── config ───────────────────────────────────────────────────────
    public const int SampleRate   = 48000;
    public const int Channels     = 1;      // mono voor radio
    public const int FramesPerBuf = 480;    // 10 ms @ 48 kHz

    // ── interne staat ─────────────────────────────────────────────────
    private ma_device  _device;
    private GCHandle   _selfHandle;
    private readonly PcmRingBuffer _txRing = new(SampleRate * 2); // 2s buffer
    private bool _disposed;

    // ── init / teardown ───────────────────────────────────────────────
    public void Start(string? captureDeviceName, string? playbackDeviceName)
    {
        _selfHandle = GCHandle.Alloc(this);

        var cfg = MiniAudio.ma_device_config_init(ma_device_type.ma_device_type_duplex);
        cfg.capture.format    = ma_format.ma_format_s16;
        cfg.capture.channels  = Channels;
        cfg.playback.format   = ma_format.ma_format_s16;
        cfg.playback.channels = Channels;
        cfg.sampleRate        = SampleRate;
        cfg.periodSizeInFrames = FramesPerBuf;
        cfg.dataCallback      = &DataCallback;
        cfg.pUserData         = (void*)GCHandle.ToIntPtr(_selfHandle);

        // Device ID opzoeken op naam (null = default)
        // TODO: ma_context_get_devices + naam match voor niet-default devices
        // Voor nu: default devices, namen worden genegeerd tenzij null

        fixed (ma_device* pDev = &_device)
        {
            var result = MiniAudio.ma_device_init(null, &cfg, pDev);
            if (result != ma_result.MA_SUCCESS)
                throw new InvalidOperationException($"ma_device_init mislukt: {result}");

            MiniAudio.ma_device_start(pDev);
        }
    }

    public void Stop()
    {
        fixed (ma_device* pDev = &_device)
            MiniAudio.ma_device_uninit(pDev);

        if (_selfHandle.IsAllocated) _selfHandle.Free();
    }

    // ── miniaudio callback (realtime thread) ──────────────────────────
    [UnmanagedCallersOnly]
    private static void DataCallback(
        ma_device* pDevice,
        void* pOutput,
        void* pInput,
        uint frameCount)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)pDevice->pUserData);
        if (handle.Target is not MiniAudioDevice self) return;

        int samples = (int)frameCount * Channels;

        // RX: capture → event (naar SIPSorcery WebRTC track)
        if (pInput != null)
        {
            var captured = new short[samples];
            MemoryMarshal.Cast<byte, short>(
                new ReadOnlySpan<byte>(pInput, samples * 2))
                .CopyTo(captured);
            self.OnCaptureFrame?.Invoke(captured);
        }

        // TX: ring buffer → playback (van browser mic via SIPSorcery)
        if (pOutput != null)
        {
            var outSpan = MemoryMarshal.Cast<byte, short>(
                new Span<byte>(pOutput, samples * 2));
            self._txRing.Read(outSpan);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
