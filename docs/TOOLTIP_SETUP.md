# Snap Zone Tooltip System

## Overview
When a spacecraft piece is snapped into place (or clicked in desktop testing), a tooltip appears showing the component name and an educational fact about it.

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

### SnapZoneTooltip.cs
For use with the real snap zone system. Attach alongside `SnapZone` on a snap zone object.
- Monitors `SnapZone.hasSnapped`
- When a piece snaps in, shows a tooltip with fade-in animation
- Configurable delay, duration, and content

### TooltipTestButton.cs
For desktop POC testing without VR. Attach to any 3D object with a Collider.
- Click a cube to simulate a snap
- Cube turns green, tooltip appears
- No XR dependencies

## Testing the POC (TooltipTest Scene)

1. Open `Assets/Scenes/TooltipTest`
2. The scene has 6 cubes representing spacecraft components
3. For each cube, you need to wire up in the Inspector:
   - Add **TooltipTestButton** component
   - Set **Component Name** and **Component Info**
   - Create a **Canvas** (right-click Hierarchy > UI > Canvas)
   - Add a **Panel** as child of Canvas
   - Add two **TextMeshPro - Text** elements inside the Panel (one for name, one for info)
   - Drag the Panel into **Tooltip Panel** field
   - Drag the TMP texts into **Name Text** and **Info Text** fields
4. Hit Play and click any cube

## Integrating with Real Snap Zones

When spacecraft models are ready (TG-57), add `SnapZoneTooltip` to each snap zone:

1. Select a SnapZone object (e.g. GoldSnapZone)
2. Add Component > SnapZoneTooltip
3. Set Component Name and Info
4. Create/assign tooltip UI elements
5. The tooltip auto-triggers when `SnapZone.hasSnapped` becomes true

## Sources
- [Psyche Spacecraft Info](https://psyche.ssl.berkeley.edu/mission/the-spacecraft/)
- [JPL Quick Facts](https://www.jpl.nasa.gov/press-kits/psyche/quick-facts/)
