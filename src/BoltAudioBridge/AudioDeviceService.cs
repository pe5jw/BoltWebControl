using System.Runtime.InteropServices;
using Hexa.NET.MiniAudio;

namespace BoltAudioBridge;

/// <summary>
/// Enumerates audio devices via miniaudio ma_context_get_devices.
/// Werkt op Windows (WASAPI/WinMM) en Linux (ALSA/PulseAudio) — geen externe dependencies.
/// </summary>
public class AudioDeviceService : IDisposable
{
    private readonly ILogger<AudioDeviceService> _log;
    private unsafe ma_context* _ctx;
    private bool _disposed;

    public AudioDeviceService(ILogger<AudioDeviceService> log)
    {
        _log = log;
        InitContext();
    }

    private unsafe void InitContext()
    {
        _ctx = (ma_context*)NativeMemory.AllocZeroed((nuint)sizeof(ma_context));
        var result = MiniAudio.ma_context_init(null, 0, null, _ctx);
        if (result != ma_result.MA_SUCCESS)
        {
            _log.LogWarning("ma_context_init mislukt ({R}) — device enumeration niet beschikbaar", result);
            NativeMemory.Free(_ctx);
            _ctx = null;
        }
    }

    public unsafe AudioDevices GetDevices()
    {
        if (_ctx == null)
            return Fallback();

        ma_device_info* pPlayback; uint playCount;
        ma_device_info* pCapture;  uint captureCount;

        var result = MiniAudio.ma_context_get_devices(
            _ctx,
            &pPlayback, &playCount,
            &pCapture,  &captureCount);

        if (result != ma_result.MA_SUCCESS)
        {
            _log.LogWarning("ma_context_get_devices mislukt ({R})", result);
            return Fallback();
        }

        var inputs  = new List<AudioDeviceInfo>();
        var outputs = new List<AudioDeviceInfo>();

        for (uint i = 0; i < captureCount; i++)
        {
            var info = pCapture[i];
            inputs.Add(new AudioDeviceInfo(
                Id:        DeviceIdToString(info.id),
                Name:      NameFromBytes(info.name),
                IsDefault: info.isDefault != 0));
        }

        for (uint i = 0; i < playCount; i++)
        {
            var info = pPlayback[i];
            outputs.Add(new AudioDeviceInfo(
                Id:        DeviceIdToString(info.id),
                Name:      NameFromBytes(info.name),
                IsDefault: info.isDefault != 0));
        }

        return new AudioDevices(inputs, outputs);
    }

    // ── helpers ───────────────────────────────────────────────────────

    private static unsafe string NameFromBytes(fixed byte name[256])
    {
        // Hexa.NET geeft naam als fixed byte array
        fixed (byte* p = name)
            return Marshal.PtrToStringUTF8((IntPtr)p) ?? "Unknown";
    }

    private static unsafe string DeviceIdToString(ma_device_id id)
    {
        // Gebruik het eerste stuk van de union als hex fingerprint
        var bytes = new ReadOnlySpan<byte>(&id, 4);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static AudioDevices Fallback() => new(
        [new("default", "Default Input",  true)],
        [new("default", "Default Output", true)]);

    public unsafe void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ctx != null)
        {
            MiniAudio.ma_context_uninit(_ctx);
            NativeMemory.Free(_ctx);
            _ctx = null;
        }
    }
}

public record AudioDevices(
    IReadOnlyList<AudioDeviceInfo> Inputs,
    IReadOnlyList<AudioDeviceInfo> Outputs);
