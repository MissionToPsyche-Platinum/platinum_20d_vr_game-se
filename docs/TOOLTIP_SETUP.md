# Chalkboard Display System

## Overview
A central chalkboard displays educational info about Psyche spacecraft components. Grab a piece to see its info, snap it into place for a permanent confirmation with progress tracking.

## Architecture

```
ComponentData (ScriptableObject)     Chalkboard (scene singleton)
  - componentName                      - nameText (TMP)
  - description                        - infoText (TMP)
  - icon                               - progressText (TMP)
       |                                     ^
       v                                     |
ComponentInfo (on each piece)  ------>  updates board
  - listens to grab/release events       via ShowComponentInfo()
  - listens to SnappableObject.onSnapped via ShowSnappedInfo()
  - visual feedback (color, scale)       via ShowDefault()
```

## Scripts

### ComponentData.cs (ScriptableObject)
Create via **Assets > Create > Psyche VR > Component Data**.
- `componentName` — display name
- `description` — educational text
- `icon` — optional sprite

Pre-made assets in `Assets/ScriptableObjects/MissionContent/`:
- SolarPanel, Magnetometer, MultispectralImager
- GammaRaySpectrometer, HallEffectThruster, SpacecraftBus

### Chalkboard.cs
One per scene. Tag as **"Chalkboard"**.
- Fade transitions between text changes
- Progress counter ("X/6 components placed")
- Checkmark on snapped components
- `onAllComponentsPlaced` UnityEvent when puzzle complete

### ComponentInfo.cs
Attach to each spacecraft piece.
- Drag a ComponentData asset into the **Data** field
- Updates chalkboard on VR grab or mouse click
- Event-driven snap detection (no polling)
- Highlight color when held, green when snapped
- Hover scale-up effect in VR
- Auto-finds Chalkboard by tag

### SnappableObject.cs (modified)
- Added `onSnapped` event — fires when `isSnapped` is set to true
- Eliminates polling in ComponentInfo's Update loop

## Setup

### Chalkboard
1. Create a **Quad** (board surface), scale ~2x1.5, place on wall
2. Add child **Canvas** (World Space), size to match
3. Add **Panel** + three **TextMeshPro - Text** elements (name, info, progress)
4. Add **CanvasGroup** to Canvas
5. Add **Chalkboard** script, assign text fields
6. Tag as `Chalkboard`

### Each Piece
1. Must have: **Collider**, **Rigidbody**, **PsycheGrabbable**, **SnappableObject**
2. Add **ComponentInfo** script
3. Drag the matching **ComponentData** asset from `ScriptableObjects/MissionContent/`
4. Chalkboard auto-discovered by tag

### Desktop Testing
Click and hold a piece with mouse — chalkboard updates. Release — resets.

## Spacecraft Components

| Component | Info |
|-----------|------|
| **Solar Panel** | Two 5-panel cross-shaped arrays. 21 kW near Earth, 2.3 kW at Psyche (3+ AU from Sun). |
| **Magnetometer** | Detects ancient magnetic field evidence to determine if Psyche was a planetary core. |
| **Multispectral Imager** | Twin cameras mapping surface geology in visible and near-infrared light. |
| **Gamma-Ray Spectrometer** | Identifies surface chemical elements via gamma-ray and neutron emissions. |
| **Hall-Effect Thruster** | First interplanetary use of Hall-effect thrusters. Solar electric propulsion with xenon gas. |
| **Spacecraft Bus** | Maxar SSL-1300 platform chassis. Manages power, comms, and holds all components. |

## Sources
- [Psyche Spacecraft](https://psyche.ssl.berkeley.edu/mission/the-spacecraft/)
- [JPL Quick Facts](https://www.jpl.nasa.gov/press-kits/psyche/quick-facts/)
- [NASA Psyche Instruments](https://science.nasa.gov/blogs/psyche/2023/10/13/the-psyche-spacecraft-and-science-instruments/)
