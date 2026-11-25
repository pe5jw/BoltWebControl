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
    public const int SampleRate    = 48000;
    public const int Channels      = 1;      // mono voor radio
    public const int FramesPerBuf  = 480;    // 10 ms @ 48 kHz

    // ── interne staat ─────────────────────────────────────────────────
    private MaDevice   _device;
    private GCHandle   _selfHandle;
    private readonly PcmRingBuffer _txRing = new(SampleRate * 2); // 2s buffer
    private bool _started;
    private bool _disposed;

    // ── init / teardown ───────────────────────────────────────────────
    public void Start(string? captureDeviceName, string? playbackDeviceName)
    {
        _selfHandle = GCHandle.Alloc(this);

        // DeviceConfigInit geeft een MaDeviceConfig terug (by value)
        var cfg = MiniAudio.DeviceConfigInit(MaDeviceType.Duplex);
        cfg.Capture.Format     = MaFormat.S16;
        cfg.Capture.Channels   = Channels;
        cfg.Playback.Format    = MaFormat.S16;
        cfg.Playback.Channels  = Channels;
        cfg.SampleRate         = SampleRate;
        cfg.PeriodSizeInFrames = FramesPerBuf;

        // DataCallback: het veld cfg.DataCallback is void* — cast de function pointer expliciet.
        cfg.DataCallback = (void*)(delegate*<MaDevice*, void*, void*, uint, void>)&DataCallback;

        cfg.PUserData = (void*)GCHandle.ToIntPtr(_selfHandle);

        // TODO: device-id selectie op naam via ma_context_get_devices
        // Voor nu: default capture/playback devices

        var result = MiniAudio.DeviceInit((MaContextPtr)null, in cfg, ref _device);
        if (result != MaResult.Success)
            throw new InvalidOperationException($"ma_device_init mislukt: {result}");

        result = MiniAudio.DeviceStart(ref _device);
        if (result != MaResult.Success)
        {
            MiniAudio.DeviceUninit(ref _device);
            throw new InvalidOperationException($"ma_device_start mislukt: {result}");
        }

        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        MiniAudio.DeviceUninit(ref _device);
        if (_selfHandle.IsAllocated) _selfHandle.Free();
    }

    // ── miniaudio callback (realtime thread) ──────────────────────────
    [UnmanagedCallersOnly]
    private static void DataCallback(
        MaDevice* pDevice,
        void*     pOutput,
        void*     pInput,
        uint      frameCount)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)pDevice->PUserData);
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
