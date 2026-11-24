# BoltWebControl

Self-hosted web CAT control — ASP.NET Core server op Linux, bediening via browser.

## Projecten

| Project | Omschrijving |
|---------|--------------|
| `BoltCat.Core` | Gedeelde rigctld TCP client + rig state model |
| `BoltAudioBridge` | WebRTC ↔ soundcard audio bridge (standalone PoC) |
| `BoltWebControl` | ASP.NET Core webserver + SignalR + frontend |

## Quick start

```bash
dotnet run --project src/BoltAudioBridge
```

## Stack

- ASP.NET Core .NET 10
- SIPSorcery (WebRTC)
- Hexa.NET.MiniAudio (audio I/O)
- SignalR (signaling + CAT realtime)
- Vanilla JS frontend

## Licentie

MIT — PE5JW Joeri
