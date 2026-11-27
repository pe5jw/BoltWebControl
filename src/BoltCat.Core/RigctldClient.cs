using System.Net.Sockets;
using System.Text;

namespace BoltCat.Core;

/// <summary>
/// TCP client naar rigctld (Hamlib daemon).
/// Gedeeld door BoltCatControl (Avalonia) en BoltWebControl (ASP.NET).
/// Elke aanroep opent een korte TCP verbinding (stateless, rigctld-stijl).
/// </summary>
public class RigctldClient : IDisposable
{
    private readonly string _host;
    private readonly int    _port;
    private readonly TimeSpan _timeout;
    private bool _disposed;

    public RigctldClient(string host = "localhost", int port = 4532, int timeoutMs = 2000)
    {
        _host    = host;
        _port    = port;
        _timeout = TimeSpan.FromMilliseconds(timeoutMs);
    }

    // ── Polling ─────────────────────────────────────────────────────────────

    public async Task<RigState> GetStateAsync(CancellationToken ct = default)
    {
        long   freq   = await GetFrequencyAsync(ct);
        string mode   = await GetModeAsync(ct);
        bool   ptt    = await GetPttAsync(ct);
        int    smeter = await GetSMeterAsync(ct);
        return new RigState(freq, mode, ptt, smeter);
    }

    // ── Getters ──────────────────────────────────────────────────────────────

    /// <summary>Vraagt frequentie op in Hz (rigctld: f)</summary>
    public async Task<long> GetFrequencyAsync(CancellationToken ct = default)
    {
        string resp = await SendCommandAsync("f\n", ct);
        return long.TryParse(resp.Trim(), out long hz) ? hz : 0;
    }

    /// <summary>Vraagt mode op, bv. USB / LSB / AM / FM / CW (rigctld: m)</summary>
    public async Task<string> GetModeAsync(CancellationToken ct = default)
    {
        string resp = await SendCommandAsync("m\n", ct);
        // rigctld antwoordt: "USB\n3000\n" — we nemen de eerste regel
        return resp.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "?";
    }

    /// <summary>Vraagt PTT-status op (rigctld: t)</summary>
    public async Task<bool> GetPttAsync(CancellationToken ct = default)
    {
        string resp = await SendCommandAsync("t\n", ct);
        return resp.Trim() == "1";
    }

    /// <summary>Vraagt S-meter op als 0-9 (rigctld: l STRENGTH → dB → schaal)</summary>
    public async Task<int> GetSMeterAsync(CancellationToken ct = default)
    {
        string resp = await SendCommandAsync("l STRENGTH\n", ct);
        // rigctld geeft dBm terug (bijv. -73.000000); S9 = -73 dBm, 6 dB per S-punt
        if (!double.TryParse(resp.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double dbm))
            return 0;
        // S9 = -73 dBm → 9 punten; iedere 6 dB = 1 punt; clamp 0–9
        int s = (int)Math.Round((dbm + 127) / 6.0);
        return Math.Clamp(s, 0, 9);
    }

    // ── Setters ──────────────────────────────────────────────────────────────

    /// <summary>Stelt frequentie in Hz in (rigctld: F <hz>)</summary>
    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
        => SendCommandAsync($"F {hz}\n", ct);

    /// <summary>Stelt mode in, bv. "USB" (rigctld: M <mode> 0)</summary>
    public Task SetModeAsync(string mode, CancellationToken ct = default)
        => SendCommandAsync($"M {mode} 0\n", ct);

    /// <summary>Zet PTT aan/uit (rigctld: T 1 / T 0)</summary>
    public Task SetPttAsync(bool on, CancellationToken ct = default)
        => SendCommandAsync($"T {(on ? 1 : 0)}\n", ct);

    // ── Intern ───────────────────────────────────────────────────────────────

    private async Task<string> SendCommandAsync(string command, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_host, _port, cts.Token);

        await using var stream = tcp.GetStream();
        byte[] cmd = Encoding.ASCII.GetBytes(command);
        await stream.WriteAsync(cmd, cts.Token);

        var sb   = new StringBuilder();
        var buf  = new byte[256];
        stream.ReadTimeout = (int)_timeout.TotalMilliseconds;

        try
        {
            while (true)
            {
                int n = await stream.ReadAsync(buf, cts.Token);
                if (n == 0) break;
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                // Einde van rigctld antwoord: lege regel OF "RPRT 0\n"
                string s = sb.ToString();
                if (s.Contains("RPRT") || (s.TrimEnd().Length > 0 && s.EndsWith('\n')))
                    break;
            }
        }
        catch (OperationCanceledException) { /* timeout = einde lezen */ }

        return sb.ToString();
    }

    public void Dispose() { _disposed = true; }
}
