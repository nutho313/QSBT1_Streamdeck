<table>
<tr>
<td><a href="https://www.nutho313.ch"><img src="https://avatars.githubusercontent.com/u/268033363?s=100&v=4" width="100"/></a></td>
<td>

# nutho313 QSBT1 ControlMapper

**Control the Qubic System QS-BT1 seat belt tensioner directly from your Stream Deck.**

[![Version](https://img.shields.io/badge/version-1.0-orange)](https://github.com/nutho313/QSBT1_Streamdeck/releases)
[![Platform](https://img.shields.io/badge/platform-Windows-blue)](https://github.com/nutho313/QSBT1_Streamdeck)
[![Stream Deck](https://img.shields.io/badge/Stream%20Deck-SDK%20v2-black)](https://developer.elgato.com)

[🌐 nutho313.ch](https://www.nutho313.ch) · [☕ Ko-fi](https://ko-fi.com/nutho313)

</td>
</tr>
</table>

---

## What is this?

The **QS-BT1** is a seat belt tensioner system by Qubic System that provides haptic feedback during sim racing — braking, acceleration, lateral forces, vibrations, and more. This Stream Deck plugin lets you **adjust every tune parameter in real time**, directly from your Stream Deck, without ever leaving your sim.

---

## Installation

1. Go to [**Releases**](https://github.com/nutho313/QSBT1_Streamdeck/releases)
2. Download `ch.nutho313.qsbt1.streamDeckPlugin`
3. **Double-click** the file — Stream Deck installs it automatically
4. Restart Stream Deck if prompted

---

## First Setup

The plugin communicates with the QS-BT1 software over your **local network**.

1. Drag a **Settings** button onto your Stream Deck
2. Open its Property Inspector (click the button in the Stream Deck app)
3. Click **🔍 Auto-detect** — the plugin will try to find your PC's IP automatically
4. If not found, enter your IP manually (e.g. `192.168.1.100`)
5. Click **★ Apply** to save and push settings to all buttons
6. Click **⚡ Test** to verify the connection — you should see your profile count

> **Tip:** The Settings button does not need to stay on your active profile. You can put it on a hidden page and only access it when needed.

---

## Actions

The plugin provides **10 actions** divided into 4 categories.

### 🎛 Dial actions *(round icon — for Stream Deck+ encoders)*

#### Tune Dial
Displays **all parameters** of a selected tune on the LCD screen.

| Input | Action |
|-------|--------|
| Rotate CW | +step on selected parameter |
| Rotate CCW | −step on selected parameter |
| Push | Toggle tune ON / OFF |
| Touch | Cycle to next parameter |

#### Single Tune Dial
Displays a **single parameter** of a selected tune on the LCD screen.

| Input | Action |
|-------|--------|
| Rotate CW | +step |
| Rotate CCW | −step |
| Push | Toggle tune ON / OFF |

#### Overall Gain Dial
Adjusts **all physical force gains** simultaneously.

| Input | Action |
|-------|--------|
| Rotate CW | +0.1 on all force gains |
| Rotate CCW | −0.1 on all force gains |
| Push | Reset all force gains to 1.0 |

Affected tunes: Braking, Acceleration, Sideways Acceleration, Centrifugal Force, Vertical G-Force, Road Harshness

#### Overall FX Dial
Adjusts **all vibration effect levels** simultaneously.

| Input | Action |
|-------|--------|
| Rotate CW | +0.1 on all effects |
| Rotate CCW | −0.1 on all effects |
| Push | Reset all effects to 1.0 |

Affected tunes: Rev Limiter, Wheel Forward Slip/Lock, Wheel Slip Angle, ABS Active, Rumble Strips Frequency, Rumble Strips Intensity, LFE Enhancement

---

### 🔲 Button actions *(square icon — for standard Stream Deck keys)*

All keypad buttons use a **multi-press counter** with a 250ms detection window.

#### Press logic

| Presses | Result |
|---------|--------|
| 1 press, wait 250ms | **+1 step** |
| 2 rapid presses | **−1 step** |
| 3 rapid presses | **−2 steps** |
| 4 rapid presses | **−3 steps** |
| N rapid presses | **−(N−1) steps** |

One tap = go up. Multiple rapid taps = go down by the number of extra presses. This allows fast coarse control in both directions without needing to flip between modes.

#### Tune Button
Displays **all parameters** of a selected tune on the button image.

| Input | Action |
|-------|--------|
| 1 press | +step on selected parameter |
| 2+ presses | −(N−1) steps |
| Hold 1s | Cycle to next parameter |
| Hold 1s on last param | Toggle tune ON / OFF |

#### Single Tune Button
Displays a **single parameter** of a selected tune on the button image.

| Input | Action |
|-------|--------|
| 1 press | +step |
| 2+ presses | −(N−1) steps |
| Hold 1s | Toggle tune ON / OFF |

#### Overall Gain
Adjusts **all physical force gains** simultaneously.

| Input | Action |
|-------|--------|
| 1 press | +0.1 on all force gains |
| 2+ presses | −(N−1) × 0.1 on all |
| Hold 1s | Reset all to 1.0 |

#### Overall FX
Adjusts **all vibration effect levels** simultaneously.

| Input | Action |
|-------|--------|
| 1 press | +0.1 on all effects |
| 2+ presses | −(N−1) × 0.1 on all |
| Hold 1s | Reset all to 1.0 |

---

### 👤 Profile Button

Displays the **active profile** in a slot-machine style with the previous and next profile visible.

| Input | Action |
|-------|--------|
| Press | Activate next profile in rotation |

The display shows:
- **Top band (blue)** — previous profile
- **Middle band (orange)** — active profile
- **Bottom band (blue)** — next profile

---

### ⚙️ Settings

Plugin configuration panel. Does not need to be on your active profile.

- Set IP address and port of the QS-BT1 software
- **Auto-detect** — finds your local IP automatically
- **★ Apply** — saves and pushes settings to all buttons
- **⚡ Test** — verifies connection and shows profile count
- **🌐 Open Web Interface** — opens the QS-BT1 web UI in your browser

---

## Available Tunes

### Physical Forces (group 12)
| Tune | Parameters |
|------|-----------|
| Braking | Gain, Sharpness, Deadzone |
| Acceleration | Gain, Sharpness, Deadzone |
| Sideways Acceleration | Gain, Sharpness, Deadzone |
| Centrifugal Force | Gain, Sharpness, Deadzone |
| Bounds | Neutral, Maximum |
| Vertical G-Force | Gain, Sharpness |
| Side Slip | Threshold, Frequency, Intensity |
| Road Harshness | Gain, Sharpness |
| Pre-Impact Protection | Long., Lateral, Duration |

### Movement Detection (group 2)
| Tune | Parameters |
|------|-----------|
| Violent Movement Threshold | Threshold |
| Violent Movement Suppression Time | Duration |

### Vibration Effects (group 17)
| Tune | Parameters |
|------|-----------|
| Rev Limiter | Frequency, Intensity |
| Gear Change Effect | Duration, Downshift, Upshift |
| Wheel Forward Slip/Lock | Frequency, Intensity |
| Wheel Slip Angle | Frequency, Intensity |
| ABS Active | Frequency, Intensity |
| Rumble Strips Frequency | At 20kmh, At 300kmh |
| Rumble Strips Intensity | At 20kmh, At 300kmh |
| Engine Vibration Extra | Phase Shift, Alone, In-Group |
| LFE Enhancement | Gain, Sharpness |

---

## Display

Each button renders a **live image** showing:
- Tune name and ON/OFF status (colored dot)
- Parameter name and current value
- Progress bar (proportional to min/max range)

All images render at **2× resolution** (144×144px) for sharp display on high-density screens.

---

## Requirements

- Windows 10 or later
- Stream Deck software 6.0+
- Qubic System QS-BT1 with software running on the same network
- Stream Deck+ for dial actions (encoder required)

---

## Building from source

```powershell
# Clone the repo
git clone https://github.com/nutho313/QSBT1_Streamdeck.git
cd QSBT1_Streamdeck

# Build and install
powershell -ExecutionPolicy Bypass -File .\build-plugin.ps1
```

Requires .NET 8 SDK and Stream Deck software installed.

---

## Changelog

### v1.0
- Overall Gain button + dial — adjust all force gains simultaneously
- Overall FX button + dial — adjust all vibration effects simultaneously
- Multi-press button logic: 1 press = +step, N presses = −(N−1) steps, 250ms window
- Color-coded tune dropdown in Property Inspector

### v0.991
- Single Tune Dial action (encoder support)
- Slot-machine style Profile Button
- Round icons for dials, square icons for buttons
- 2× render resolution for sharper text
- Tune Dial: dot indicator replaces ON/OFF text

### v0.99
- Single Tune Button action
- Dedicated PI per action type
- Custom icons for all actions
- nutho313 logo integration

---

## License

MIT — free to use, modify, and distribute.

---

<p align="center">
Made with ❤️ by <a href="https://www.nutho313.ch">nutho313</a> &nbsp;·&nbsp; <a href="https://ko-fi.com/nutho313">☕ Support on Ko-fi</a>
</p>

---

## Build History

| Build | Date | Notes |
|-------|------|-------|
| v1.09 (base) | 2026-04-22 | Last known good — Overall Gain/FX button+dial, multi-press 300ms, color-coded PI |
| v1.09.001 | 2026-04-22 | Fix GetTuneValue/GetTuneEnabled overloads (build error) |
| v1.09.002 | 2026-04-22 | Fix slowness: remove freshJson per-tune, timeout 4s→1.5s |

> **Note:** From v1.09 onwards, each zip uses incremental build numbers (1.09.001, 1.09.002...) to track changes precisely. The build number is in this README and in `manifest.json`.
