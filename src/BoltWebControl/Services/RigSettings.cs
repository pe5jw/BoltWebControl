namespace BoltWebControl.Services;

/// <summary>
/// Live-configuratie voor de rigctld verbinding.
/// Singleton — wordt gedeeld door RigPollerService en het settings-endpoint.
/// </summary>
public class RigSettings
{
    private string _host    = "localhost";
    private int    _port    = 4532;
    private string _rig     = "Auto";
    private string _serPort = "";

    public string Host    { get => _host;    set => _host    = value?.Trim() ?? "localhost"; }
    public int    Port    { get => _port;    set => _port    = (value is > 0 and < 65536) ? value : 4532; }
    public string Rig     { get => _rig;     set => _rig     = value?.Trim() ?? "Auto"; }
    /// <summary>Seriële poort voor rigctld (bijv. COM3 of /dev/ttyUSB0). Leeg = niet opgegeven.</summary>
    public string SerialPort { get => _serPort; set => _serPort = value?.Trim() ?? ""; }

    // Bekende Hamlib rig-modellen (model-nummer → naam) voor de dropdown
    public static readonly IReadOnlyList<(int Model, string Name)> KnownRigs =
    [
        (1,    "Dummy / Test (geen radio)"),
        // ── Yaesu ──────────────────────────────────────────────────────────
        (120,  "Yaesu FT-817"),
        (122,  "Yaesu FT-847"),
        (127,  "Yaesu FT-857"),
        (128,  "Yaesu FT-897"),
        (1042, "Yaesu FT-897D"),
        (129,  "Yaesu FT-100"),
        (1033, "Yaesu FT-950"),
        (1035, "Yaesu FT-2000"),
        (1038, "Yaesu FT-5000"),
        (1039, "Yaesu FT-9000"),
        (1041, "Yaesu FTDX-1200"),
        (1043, "Yaesu FTDX-3000"),
        (1044, "Yaesu FTDX-5000"),
        (1051, "Yaesu FT-991"),
        (1057, "Yaesu FT-DX10"),
        (1061, "Yaesu FT-DX101D"),
        (1062, "Yaesu FT-DX101MP"),
        // ── Icom ───────────────────────────────────────────────────────────
        (370,  "Icom IC-7100"),
        (371,  "Icom IC-7200"),
        (373,  "Icom IC-7300"),
        (374,  "Icom IC-7600"),
        (375,  "Icom IC-7700"),
        (376,  "Icom IC-7610"),
        (377,  "Icom IC-9700"),
        (378,  "Icom IC-705"),
        // ── Kenwood ────────────────────────────────────────────────────────
        (3084, "Kenwood TS-2000"),
        (3085, "Kenwood TS-480"),
        (3086, "Kenwood TS-590S"),
        (3087, "Kenwood TS-590SG"),
        (3089, "Kenwood TS-990S"),
        (3090, "Kenwood TS-890S"),
        // ── Elecraft ───────────────────────────────────────────────────────
        (3001, "Elecraft K3"),
        (3002, "Elecraft KX3"),
        (3004, "Elecraft K4"),
        // ── FlexRadio ──────────────────────────────────────────────────────
        (2014, "FlexRadio 6300"),
        (2015, "FlexRadio 6400"),
        (2016, "FlexRadio 6500"),
        (2017, "FlexRadio 6600"),
        (2018, "FlexRadio 6700"),
    ];
}
