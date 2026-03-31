# Chalkboard Display System

## Overview
A central chalkboard displays educational info about Psyche spacecraft components. Grab a piece to see its info, snap it into place for a permanent confirmation with progress tracking. When all pieces are placed, the board shows a completion message.

## Architecture

```
ComponentData (ScriptableObject)     Chalkboard (scene singleton)
  - componentName                      - nameText (TMP)
  - category                           - infoText (TMP)
  - description                        - progressText (TMP)
  - icon                               - snapSound / completionSound
       |                                     ^
       v                                     |
ComponentInfo (per piece)  ---------->  updates board
  - grab/release → ShowComponentInfo()
  - snap detected → ShowSnappedInfo()
  - release (no snap) → ShowDefault()

SnappableObject.onSnapped ──> ComponentInfo.OnSnapped()
SnapZone.onPieceSnapped   ──> (available for other systems)
```

## Scripts

### ComponentData.cs (ScriptableObject)
Create via **Assets > Create > Psyche VR > Component Data**.

| Field | Purpose |
|-------|---------|
| `componentName` | Display name (e.g. "Solar Panel") |
| `category` | Subtitle (e.g. "Power System", "Science Instrument") |
| `description` | Educational text for the chalkboard |
| `icon` | Optional sprite for future UI |
| `DisplayTitle` | Auto-formatted: "Name — Category" |

Pre-made assets in `Assets/ScriptableObjects/MissionContent/`.

### Chalkboard.cs
One per scene. Tag as **"Chalkboard"**.

| Feature | Detail |
|---------|--------|
| Fade transitions | Smooth text changes via CanvasGroup |
| Progress counter | "X / 6 components placed" |
| Snap checkmark | "Solar Panel — Power System ✔" |
| Completion state | Locks board with mission summary when all placed |
| Audio | Snap sound per piece, completion sound when done |
| Events | `onAllComponentsPlaced` UnityEvent |

### ComponentInfo.cs
Attach to each spacecraft piece.

| Feature | Detail |
|---------|--------|
| Data source | Drag a ComponentData ScriptableObject |
| VR input | XR grab/release events (works with PsycheGrabbable) |
| Desktop input | Mouse click/release |
| Snap detection | Event-driven via SnappableObject.onSnapped |
| Highlight | Cyan while held, green after snapped |
| Hover | 1.05x scale up in VR |

### SnapZone.cs (enhanced)
| Feature | Detail |
|---------|--------|
| Tag filtering | Only matching tags can snap |
| Visual feedback | Green (valid) / red (invalid) material swap |
| `onPieceSnapped` event | Fires with snapped GameObject for other systems |
| Documented fields | Tooltips on all Inspector fields |

### SnappableObject.cs (enhanced)
| Feature | Detail |
|---------|--------|
| `onSnapped` event | C# event, no polling needed |
| Property-backed | Setting `isSnapped = true` auto-fires event |

## Spacecraft Components

| Component | Category | Info |
|-----------|----------|------|
| **Solar Panel** | Power System | Two 5-panel cross-shaped arrays. 21 kW near Earth, 2.3 kW at Psyche. |
| **Magnetometer** | Science Instrument | Detects ancient magnetic field to determine if Psyche was a planetary core. |
| **Multispectral Imager** | Science Instrument | Twin cameras mapping surface in visible and near-infrared light. |
| **Gamma-Ray Spectrometer** | Science Instrument | Identifies surface elements via gamma-ray and neutron emissions. |
| **Hall-Effect Thruster** | Propulsion | First interplanetary use of Hall-effect thrusters. Xenon gas propulsion. |
| **Spacecraft Bus** | Structure | Maxar SSL-1300 platform. Manages power, comms, holds all components. |

## Setup

### Chalkboard
1. Create **Quad** (board surface), scale ~2x1.5, place on wall
2. Add child **Canvas** (World Space) + **Panel** + 3 **TextMeshPro** texts
3. Add **CanvasGroup** to Canvas
4. Add **Chalkboard** script, assign text fields
5. Optionally assign snap/completion AudioClips
6. Tag as `Chalkboard`

### Each Piece
1. Needs: **Collider**, **Rigidbody**, **PsycheGrabbable**, **SnappableObject**
2. Add **ComponentInfo**, drag matching **ComponentData** asset
3. Chalkboard auto-found by tag

### Desktop Testing
Click and hold a piece → board updates. Release → resets. No VR needed.

## Sources
- [Psyche Spacecraft](https://psyche.ssl.berkeley.edu/mission/the-spacecraft/)
- [JPL Quick Facts](https://www.jpl.nasa.gov/press-kits/psyche/quick-facts/)
- [NASA Psyche Instruments](https://science.nasa.gov/blogs/psyche/2023/10/13/the-psyche-spacecraft-and-science-instruments/)
