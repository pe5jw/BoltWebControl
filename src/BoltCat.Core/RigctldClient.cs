using System.Net.Sockets;
using System.Text;

namespace BoltCat.Core;

/// <summary>
/// TCP client naar rigctld (Hamlib daemon).
/// Per aanroep een korte verbinding — stateless, thread-safe.
/// Host en poort kunnen op elk moment worden bijgewerkt (live reconnect).
/// </summary>
public class RigctldClient : IDisposable
{
    private volatile string _host;
    private volatile int    _port;
    private readonly TimeSpan _timeout;

    public string Host { get => _host; set => _host = value?.Trim() ?? "localhost"; }
    public int    Port { get => _port; set => _port = (value is > 0 and < 65536) ? value : 4532; }

    public RigctldClient(string host = "localhost", int port = 4532, int timeoutMs = 2000)
    {
        _host    = host;
        _port    = port;
        _timeout = TimeSpan.FromMilliseconds(timeoutMs);
    }

    // ── Samengestelde poll ────────────────────────────────────────────────

    public async Task<RigState> GetStateAsync(CancellationToken ct = default)
    {
        long   freq   = await GetFrequencyAsync(ct);
        string mode   = await GetModeAsync(ct);
        bool   ptt    = await GetPttAsync(ct);
        int    smeter = await GetSMeterAsync(ct);
        return new RigState(freq, mode, ptt, smeter);
    }

    // ── Getters ───────────────────────────────────────────────────────────

    public async Task<long> GetFrequencyAsync(CancellationToken ct = default)
    {
        string r = await SendCommandAsync("f\n", ct);
        return long.TryParse(r.Trim(), out long hz) ? hz : 0;
    }

    public async Task<string> GetModeAsync(CancellationToken ct = default)
    {
        string r = await SendCommandAsync("m\n", ct);
        return r.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "?";
    }

    public async Task<bool> GetPttAsync(CancellationToken ct = default)
    {
        string r = await SendCommandAsync("t\n", ct);
        return r.Trim() == "1";
    }

    /// <summary>S-meter 0–9 (rigctld: l STRENGTH geeft dBm)</summary>
    public async Task<int> GetSMeterAsync(CancellationToken ct = default)
    {
        string r = await SendCommandAsync("l STRENGTH\n", ct);
        if (!double.TryParse(r.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double dbm))
            return 0;
        // S9 = -73 dBm; 6 dB per S-punt; clamp 0–9
        return Math.Clamp((int)Math.Round((dbm + 127) / 6.0), 0, 9);
    }

    // ── Setters ───────────────────────────────────────────────────────────

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
        => SendCommandAsync($"F {hz}\n", ct);

    public Task SetModeAsync(string mode, CancellationToken ct = default)
        => SendCommandAsync($"M {mode} 0\n", ct);

    public Task SetPttAsync(bool on, CancellationToken ct = default)
        => SendCommandAsync($"T {(on ? 1 : 0)}\n", ct);

    // ── TCP ───────────────────────────────────────────────────────────────

    private async Task<string> SendCommandAsync(string command, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_host, _port, cts.Token);

        await using var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(command), cts.Token);

        var sb  = new StringBuilder();
        var buf = new byte[256];
        stream.ReadTimeout = (int)_timeout.TotalMilliseconds;

        try
        {
            while (true)
            {
                int n = await stream.ReadAsync(buf, cts.Token);
                if (n == 0) break;
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                string s = sb.ToString();
                if (s.Contains("RPRT") || (s.TrimEnd().Length > 0 && s.EndsWith('\n')))
                    break;
            }
        }
        catch (OperationCanceledException) { }

        return sb.ToString();
    }

    public void Dispose() { }
}
