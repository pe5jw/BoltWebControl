namespace BoltAudioBridge;

/// <summary>
/// Single-producer single-consumer lock-free ring buffer voor PCM shorts.
/// Write: SIPSorcery OnAudioSourceEncodedSample thread.
/// Read:  miniaudio realtime callback thread.
/// </summary>
public sealed class PcmRingBuffer
{
    private readonly short[] _buf;
    private volatile int _writePos;
    private volatile int _readPos;

    public PcmRingBuffer(int capacity) => _buf = new short[capacity];

    public void Write(short[] samples)
    {
        int wp = _writePos;
        for (int i = 0; i < samples.Length; i++)
        {
            _buf[wp] = samples[i];
            wp = (wp + 1) % _buf.Length;
        }
        _writePos = wp;
    }

    public void Read(Span<short> output)
    {
        int rp = _readPos;
        int available = (_writePos - rp + _buf.Length) % _buf.Length;

        for (int i = 0; i < output.Length; i++)
        {
            // Underrun → stilte
            output[i] = i < available ? _buf[(rp + i) % _buf.Length] : (short)0;
        }

        _readPos = (rp + Math.Min(output.Length, available)) % _buf.Length;
    }
}
