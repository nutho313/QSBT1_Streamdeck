# nutho313 QSBT1 Stream Deck Plugin

> 🌐 [www.nutho313.ch](https://www.nutho313.ch) · ☕ [Support on Ko-fi](https://ko-fi.com/nutho313)

Stream Deck plugin to control the **Qubic System QS-BT1** seat belt tensioner directly — no SimHub required.

---

## Features

| Action | Keypad | Encoder (Stream Deck +) |
|---|---|---|
| **Adjust Parameter** | Press = +step | Rotate CW/CCW = ±step · Push = reset |
| **Toggle Tune** | Press = enable/disable | — |
| **Activate Profile** | Press = activate | — |
| **Show Status** | Tap = refresh | — |

- 🟢 **Green** button = tune enabled
- 🔴 **Red** button = tune disabled
- Live value displayed on button face
- Status action polls device every **3 seconds**

---

## Supported Tunes

All tunes from **Seat Belt Tensioner | Base**, **Motion Primary | SFX** and **Seat Belt Tensioner | SFX** are available.

---

## Installation

1. Download `QSBT1_Streamdeck.streamDeckPlugin` from [Releases](https://github.com/nutho313/QSBT1_Streamdeck/releases)
2. Double-click the `.streamDeckPlugin` file — Stream Deck software installs it automatically
3. Add actions to your Stream Deck profile
4. In the Property Inspector for each action: enter your QS-BT1 **IP**, **Port**, **Profile ID**

---

## Building from source

### Prerequisites
- Visual Studio 2022 or Rider
- .NET 6 SDK
- Stream Deck Software 6.x

### Steps
```bash
git clone https://github.com/nutho313/QSBT1_Streamdeck.git
cd QSBT1_Streamdeck
dotnet build
```

---

## Architecture

```
Stream Deck App
      ↓ WebSocket (streamdeck-tools SDK)
QSBT1_Streamdeck.exe
      ↓ HTTP REST API
QS-BT1 Device (192.168.8.131:8081)
```

---

## Changelog

### v0.1 — Initial Release
- AdjustAction: keypad + encoder dial support
- ToggleAction: green/red state display
- ActivateProfileAction
- StatusDisplayAction: 3s auto-poll

---

## License
MIT — nutho313
