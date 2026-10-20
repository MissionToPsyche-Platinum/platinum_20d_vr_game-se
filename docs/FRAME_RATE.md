# Event mode frame rate on the Quest 3 (TG-211)

## Starting point

The Oct 13 sponsor sessions (build 0.9.0 event v11) logged 61-68 fps against the Quest 3's 72 Hz. 8-34% of frames went over budget each minute, and the first minute had freezes of 1.4 s and 0.5 s. Loading took 7.9-8.3 s, inside the 10 s target.

Target (Quality Plan): under 1% of frames over 17.4 ms.

## Findings

### Steady frame time (GPU)

| Finding | Status |
| --- | --- |
| The booth's coffee mug had 112k triangles (body 80k, two logos 16k each), more than the rest of the booth together, and cast shadows. | Fixed: 4.3k |
| Each basketball was a 12.5k-triangle sphere (4 in the booth, 3 in the bedroom). | Fixed: 1.5k, same UV mapping |
| Foveated rendering is off: the Foveated Rendering OpenXR feature is disabled for Android. | Next: enable, compare on the headset |
| The main light draws a 4096 soft-shadow map every frame (Performance URP Config, used on Android). | Next: smaller map, or none once the lighting is baked (TG-202) |
| No occlusion culling is baked, and Event mode keeps the Story world loaded: looking left or behind from the booth, the bedroom and the outdoor terrain (2,114 trees) are drawn behind the hall walls. | Next: bake occlusion with the lighting (TG-202) |
| The booth is lit by a realtime point light on top of the realtime sun. | TG-202 bake |

### Spikes and the first-minute freezes (CPU)

| Finding | Status |
| --- | --- |
| The Android player recorded a full native stack trace for every log message and warning. | Fixed: none for logs, script only for warnings; errors keep full traces |
| Every sound decompressed on load with preloading off, so each clip was read and decoded on the main thread the first time it played (every first grab, page turn, snap, and the 1.2 MB headset signal). | Fixed: preloaded and loaded in the background; clips over 1 MB stay compressed in memory |
| No shader warm-up: on Vulkan each new material that comes into view builds its pipeline on first draw, which is typical of a first-minute freeze. | Next: record the variants on the headset and warm them up during loading |
| The launch video and the cat book video are VP8 WebM, decoded in software. The Linux editor can only play VP8, so they stay WebM unless the profile shows the decode matters. | Watch in the profile |

## How it is measured

Each Event session logs a `perf_sample` every 60 s (`fps`, `overrunPct`, `worstMs`; see SESSION_DATA_LOGGER.md). For each build, run three 2.5-minute sessions on the Quest 3 and compare those samples. Use the Unity Profiler over adb for anything the samples cannot explain.
