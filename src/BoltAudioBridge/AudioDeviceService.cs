using System.Runtime.InteropServices;
using Hexa.NET.MiniAudio;

namespace BoltAudioBridge;

/// <summary>
/// Enumerates audio devices via miniaudio ma_context_get_devices.
/// Werkt op Windows (WASAPI/WinMM) en Linux (ALSA/PulseAudio) — geen externe dependencies.
/// </summary>
public sealed class AudioDeviceService : IDisposable
{
    private readonly ILogger<AudioDeviceService> _log;
    private bool _disposed;
    private MaContext _ctx;
    private bool _ctxOk;

    public AudioDeviceService(ILogger<AudioDeviceService> log)
    {
        _log = log;
        InitContext();
    }

    private unsafe void InitContext()
    {
        // ContextInit(MaBackend* backends, uint backendCount, MaContextConfigPtr pConfig, ref MaContext pContext)
        // Geef null/0 door voor automatische backend-selectie
        var result = MiniAudio.ContextInit((MaBackend*)null, 0, MaContextConfigPtr.Null, ref _ctx);
        if (result != MaResult.Success)
        {
            _log.LogWarning("ma_context_init mislukt ({R}) — device enumeration niet beschikbaar", result);
            _ctxOk = false;
        }
        else
        {
            _ctxOk = true;
        }
    }

    public unsafe AudioDevices GetDevices()
    {
        if (!_ctxOk)
            return Fallback();

        // Declareer pointers als lokale (stack) variabelen.
        // In een unsafe methode kan het adres van een stack-pointer direct worden genomen —
        // geen fixed statement nodig (fixed is alleen voor managed heap objecten).
        MaDeviceInfo* pPlayback = null;
        uint playCount = 0;
        MaDeviceInfo* pCapture = null;
        uint captureCount = 0;

        MaDeviceInfo** ppPlayback = &pPlayback;
        MaDeviceInfo** ppCapture  = &pCapture;

        var result = MiniAudio.ContextGetDevices(
            ref _ctx,
            new MaDeviceInfoPtrPtr(ppPlayback), &playCount,
            new MaDeviceInfoPtrPtr(ppCapture),  &captureCount);

        if (result != MaResult.Success)
        {
            _log.LogWarning("ma_context_get_devices mislukt ({R})", result);
            return Fallback();
        }

        var inputs  = new List<AudioDeviceInfo>();
        var outputs = new List<AudioDeviceInfo>();

        for (uint i = 0; i < captureCount; i++)
        {
            MaDeviceInfo* infoPtr = pCapture + i;
            inputs.Add(new AudioDeviceInfo(
                Id:        DeviceIdToString(infoPtr),
                Name:      NameFromInfo(infoPtr),
                IsDefault: infoPtr->IsDefault != 0));
        }

        for (uint i = 0; i < playCount; i++)
        {
            MaDeviceInfo* infoPtr = pPlayback + i;
            outputs.Add(new AudioDeviceInfo(
                Id:        DeviceIdToString(infoPtr),
                Name:      NameFromInfo(infoPtr),
                IsDefault: infoPtr->IsDefault != 0));
        }

        return new AudioDevices(inputs, outputs);
    }

    // ── helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Leest de naam uit MaDeviceInfo.
    /// MaDeviceInfo heeft Name_0..Name_255 als losse bytes in het struct-layout.
    /// </summary>
    private static unsafe string NameFromInfo(MaDeviceInfo* info)
    {
        byte* namePtr = &info->Name_0;
        return Marshal.PtrToStringUTF8((IntPtr)namePtr) ?? "Unknown";
    }

    private static unsafe string DeviceIdToString(MaDeviceInfo* info)
    {
        // MaDeviceId is een union van 256 bytes; neem de eerste 4 als hex fingerprint
        var bytes = new ReadOnlySpan<byte>(&info->Id, 4);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static AudioDevices Fallback() => new(
        [new("default", "Default Input",  true)],
        [new("default", "Default Output", true)]);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ctxOk)
        {
            MiniAudio.ContextUninit(ref _ctx);
            _ctxOk = false;
        }
    }
}

public record AudioDevices(
    IReadOnlyList<AudioDeviceInfo> Inputs,
    IReadOnlyList<AudioDeviceInfo> Outputs);
