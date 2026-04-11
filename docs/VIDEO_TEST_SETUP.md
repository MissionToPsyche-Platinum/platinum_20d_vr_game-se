# VideoTest Scene Setup Guide

## Overview
The VideoTest scene demonstrates a button-triggered video player. Click a 3D button to play/pause a video on a screen.

## Quick Start

1. Open `Assets/Scenes/VideoTest` in Unity
2. Make sure **only** VideoTest is loaded (remove any other scenes from the Hierarchy)
3. Click **PlayButton** in the Hierarchy
4. In the Inspector, find the **Video Player Button** component
5. Drag a video file (e.g. `tg-16` from `Assets/Video/`) into the **Video Clip** field
6. Drag **VideoScreen** from the Hierarchy into the **Target Renderer** field
7. Hit **Play** (triangle at top of Unity)
8. **Click the red button** with your mouse — video plays on the screen, button turns green
9. Click again to pause — button turns red

## Scene Objects

| Object | What it does |
|--------|-------------|
| **VideoScreen** | Quad that displays the video (like a wall-mounted TV) |
| **PlayButton** | Red cube — click to play/pause. Turns green when playing. |
| **ScreenFrame** | Dark frame behind the video screen |
| **Test Camera** | Camera for non-VR desktop testing |
| **XR Origin (XR Rig)** | VR camera rig — used when testing with a headset |

## Adding Your Own Video

1. Drop any `.mp4` file into `Assets/Video/`
2. Wait for Unity to import it
3. Select **PlayButton** in the Hierarchy
4. Drag your video into the **Video Clip** field in the Inspector

## How the Script Works

`VideoPlayerButton.cs` (in `Assets/Scripts/Gameplay/VideoSystem/`):

- Attaches a `VideoPlayer` component at runtime
- `OnMouseDown()` — triggers when you click the 3D button with your mouse
- `ToggleVideo()` — public method, can also be called from UI buttons or XR events
- Changes button color: **red** = stopped, **green** = playing
- Button animates (shrinks briefly) on press

## Using in VR

In VR, the button works with XR interaction. Point your controller at the button and press the trigger/grip to activate it. The XR Origin (XR Rig) prefab from `Assets/Prefabs/VR/` handles VR input.

## Troubleshooting

- **No video playing**: Make sure Video Clip and Target Renderer are assigned on PlayButton
- **Black screen**: Video might still be loading — wait a moment after clicking
- **Can't click button**: Make sure you're in Play Mode (hit the Play button at top)
- **Seeing the Bedroom**: Remove the Bedroom scene from the Hierarchy (right-click > Remove Scene)
- **Pink materials**: The project uses URP — check that materials use `Universal Render Pipeline/Lit` shader
