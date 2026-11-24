namespace BoltAudioBridge;

/// <summary>
/// Enumerates audio devices via miniaudio (Hexa.NET.MiniAudio).
/// Werkt op Windows (WASAPI/WinMM) en Linux (ALSA/PulseAudio) zonder externe dependencies.
/// </summary>
public class AudioDeviceService
{
    // TODO: vervang door echte miniaudio device enumeration
    // ma_context_get_devices() → lijst van ma_device_info structs
    public AudioDevices GetDevices() => new(
        Inputs:  [new("default", "Default Input",  true)],
        Outputs: [new("default", "Default Output", true)]
    );
}

public record AudioDevices(
    IReadOnlyList<AudioDeviceInfo> Inputs,
    IReadOnlyList<AudioDeviceInfo> Outputs
);
