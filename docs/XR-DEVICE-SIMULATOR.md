# XR Device Simulator

Test VR in the Unity Editor without a headset. Press Play and the simulator overlay appears automatically.

## Controls

### Movement & Looking
| Action | Key |
|--------|-----|
| Move | WASD |
| Move up/down | E / Q |
| Look around | Arrow keys or right-click + mouse |
| Thumbstick (for locomotion) | I/J/K/L |
| Reset position | R |

### Controllers
| Action | Key |
|--------|-----|
| Toggle left controller | [ |
| Toggle right controller | ] |
| Toggle HMD | H |
| Cycle devices | Tab |
| Trigger | T |
| Grip | G |
| Primary button (A/X) | 1 |
| Secondary button (B/Y) | 2 |

## Scene Setup (Manual — Unity Editor Only)

Each scene needs an **XR Origin** with locomotion providers. These cannot be set via code.

### Adding an XR Origin to a scene
1. Delete the existing Main Camera
2. Drag `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab` into the Hierarchy
3. Position it at the desired spawn point

### Adding locomotion providers to an XR Origin
1. Select the XR Origin in the Hierarchy
2. Add Component: **Continuous Move Provider (Action-based)**
   - Mediator: `Locomotion`
   - Left Hand Move Input: `XRI Left Locomotion/Move`
   - Right Hand Move Input: `XRI Right Locomotion/Move`
3. Add Component: **Snap Turn Provider (Action-based)**
   - Mediator: `Locomotion`
   - Left Hand Turn Input: `XRI Left Locomotion/Turn`
   - Right Hand Turn Input: `XRI Right Locomotion/Turn`
4. Save the scene (Ctrl+S)

### Simulator prefab missing?
If you get a yellow warning about the prefab being missing:
1. Edit > Project Settings > XR Interaction Toolkit
2. Drag `Assets/Samples/XR Interaction Toolkit/3.3.1/XR Device Simulator/XR Device Simulator.prefab` into the **Simulator Prefab** field

## Performance Tips
- Close the Scene view while in Play mode (rendering both tanks FPS)
- Lower the Game view Scale
- Lower quality in Edit > Project Settings > Quality

## For Teammates
- Simulator sample files are in the repo — no need to reimport
- Auto-instantiation is enabled — just press Play
- Your scene needs an XR Origin with locomotion providers (see above)
