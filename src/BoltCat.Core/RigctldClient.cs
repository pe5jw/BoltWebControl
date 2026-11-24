namespace BoltCat.Core;

/// <summary>
/// TCP client naar rigctld (Hamlib daemon).
/// Gedeeld door BoltCatControl (Avalonia) en BoltWebControl (ASP.NET).
/// </summary>
public class RigctldClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;

    public RigctldClient(string host = "localhost", int port = 4532)
    {
        _host = host;
        _port = port;
    }

    public Task<RigState> GetStateAsync(CancellationToken ct = default)
        => Task.FromResult(new RigState(14225000, "USB", false, 5)); // stub

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
        => Task.CompletedTask; // stub

    public Task SetModeAsync(string mode, CancellationToken ct = default)
        => Task.CompletedTask; // stub

    public Task SetPttAsync(bool on, CancellationToken ct = default)
        => Task.CompletedTask; // stub

    public void Dispose() { }
}
