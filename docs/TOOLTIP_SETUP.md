# Chalkboard Display System

## Overview
A central chalkboard/display board in the scene updates with educational info when the player grabs a spacecraft component. When released, it resets to default text.

## How It Works
1. Player grabs a spacecraft piece
2. The chalkboard updates to show the component name + educational fact
3. Player releases the piece → chalkboard resets to default

## Spacecraft Components

| Component | Description |
|-----------|------------|
| **Solar Panel** | Two 5-panel cross-shaped arrays power the spacecraft. They produce 21 kW near Earth and 2.3 kW at asteroid Psyche. |
| **Magnetometer** | Detects evidence of an ancient magnetic field on asteroid Psyche, helping determine if it was once a planetary core. |
| **Multispectral Imager** | Twin cameras photograph the asteroid surface in visible and near-infrared light to map its geology. |
| **Gamma-Ray Spectrometer** | Determines the chemical elements on the surface by detecting gamma-ray and neutron emissions. |
| **Hall-Effect Thruster** | Solar electric propulsion using xenon gas. Psyche is the first interplanetary spacecraft to use this technology. |
| **Spacecraft Bus** | The main chassis that holds all instruments, thrusters, and solar panels together. Based on Maxar's SSL-1300 platform. |

## Scripts

### Chalkboard.cs
The display board. One per scene.
- `ShowComponentInfo(name, info)` — updates the board text
- `ShowDefault()` — resets to default text
- Tag the GameObject as **"Chalkboard"** so pieces can find it automatically

### ComponentInfo.cs
Attach to each spacecraft piece.
- When grabbed (VR) or clicked (mouse), updates the Chalkboard
- When released, resets the Chalkboard
- Works with XRGrabInteractable for VR and OnMouseDown for desktop testing

## Setup

### 1. Create the Chalkboard
1. Create a **Quad** in the scene (this is the board surface)
2. Scale it to ~2x1.5, position on a wall
3. Add a **World Space Canvas** as a child
4. Add a **Panel** to the Canvas (dark background)
5. Add two **TextMeshPro - Text** elements (header + body)
6. Add the **Chalkboard** script to the Quad
7. Assign the TMP text references
8. **Tag** the Quad as `Chalkboard`

### 2. Set Up Each Piece
1. Select a spacecraft piece (e.g. SolarPanel cube)
2. Add the **ComponentInfo** script
3. Set **Component Name** (e.g. "Solar Panel")
4. Set **Component Info** (educational text from table above)
5. Leave Chalkboard field empty — it auto-finds by tag

### 3. Desktop Testing
Just click any piece with the mouse. The chalkboard updates instantly.

## Sources
- [Psyche Spacecraft Info](https://psyche.ssl.berkeley.edu/mission/the-spacecraft/)
- [JPL Quick Facts](https://www.jpl.nasa.gov/press-kits/psyche/quick-facts/)
