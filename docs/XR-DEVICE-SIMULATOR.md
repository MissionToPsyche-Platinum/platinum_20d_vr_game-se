# XR Device Simulator Setup Guide

## What Is the XR Device Simulator?

The XR Device Simulator is a Unity package sample that lets you test VR interactions in the Unity Editor **without a physical headset**. It simulates head movement, hand controllers, and input using your keyboard and mouse.

When enabled, pressing Play in the Editor will show a simulator overlay with virtual controllers and a headset indicator.

## How It Works

The simulator is part of the **XR Interaction Toolkit** samples. The project includes the sample files under:

```
Psyche VR Experience/Assets/Samples/XR Interaction Toolkit/3.3.1/
```

A settings asset controls whether the simulator auto-starts:

```
Psyche VR Experience/Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset
```

The setting `m_AutomaticallyInstantiateSimulatorPrefab` is set to `1` (enabled), so the simulator prefab spawns automatically when you press Play in the Editor.

## Controls

### Head (HMD) Movement
| Action | Input |
|--------|-------|
| Look around | Right-click + move mouse |
| Move forward/back/left/right | WASD (while right-clicking) |
| Move up/down | Q / E (while right-clicking) |

### Left Controller
| Action | Input |
|--------|-------|
| Activate left controller | Press T |
| Move left controller | T held + move mouse |
| Rotate left controller | T held + right-click + move mouse |
| Trigger (select) | Left-click (while left controller active) |
| Grip | G (while left controller active) |

### Right Controller
| Action | Input |
|--------|-------|
| Activate right controller | Press Y |
| Move right controller | Y held + move mouse |
| Rotate right controller | Y held + right-click + move mouse |
| Trigger (select) | Left-click (while right controller active) |
| Grip | G (while right controller active) |

### General
| Action | Input |
|--------|-------|
| Toggle between controllers | Tab |
| Reset controller position | Press 0 (zero) |

> **Note:** Exact keybindings may vary depending on your XR Interaction Toolkit version. Check the simulator overlay in Play mode for the current bindings.

## Required Manual Setup in Unity Editor

The simulator handles input emulation, but **locomotion and scene setup must be configured in the Unity Editor GUI** — they cannot be done via text file edits.

### 1. Add Locomotion Providers to XR Origin (BasicScene)

The XR Origin in BasicScene needs locomotion components to enable movement:

1. Open `BasicScene` in the Unity Editor
2. Select the **XR Origin (XR Rig)** GameObject in the Hierarchy
3. Click **Add Component** in the Inspector
4. Add **Continuous Move Provider (Action-based)**
   - Under **System**, assign the XR Origin's **Locomotion Mediator**
   - Under **Left Hand Move Action** and/or **Right Hand Move Action**, bind the appropriate XRI input actions (e.g., `XRI Left Locomotion/Move` or `XRI Right Locomotion/Move`)
5. Add **Snap Turn Provider (Action-based)**
   - Under **System**, assign the XR Origin's **Locomotion Mediator**
   - Under **Left Hand Snap Turn Action** and/or **Right Hand Snap Turn Action**, bind the appropriate XRI input actions

### 2. Add XR Origin to Bedroom Scene

The Bedroom scene currently only has a basic Main Camera and no XR Origin:

1. Open the **Bedroom** scene
2. Delete or disable the existing **Main Camera** (the XR Origin has its own camera)
3. Drag the **XR Origin (XR Rig)** prefab from `Psyche VR Experience/Assets/XRI/Prefabs/` into the scene Hierarchy
   - If no prefab exists yet, copy the XR Origin setup from BasicScene
4. Position the XR Origin at the desired spawn point
5. Ensure the XR Origin has the same locomotion providers as described above
6. Save the scene

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No simulator overlay in Play mode | Check that `XRDeviceSimulatorSettings.asset` has `m_AutomaticallyInstantiateSimulatorPrefab: 1` |
| Simulator appears but can't move | Add ContinuousMoveProvider and SnapTurnProvider to the XR Origin (see manual setup above) |
| Scene has no VR camera | Ensure the scene has an XR Origin with a Camera Offset > Main Camera child object |
| Controls don't respond | Click inside the Game view first to give it focus |

## For Teammates

When you pull the latest changes:
- The XR Device Simulator sample files are included in the repo — no need to re-import them
- The simulator is set to auto-instantiate, so just press Play and it should appear
- You still need to ensure your scene has an XR Origin with locomotion providers configured (see manual setup above)
