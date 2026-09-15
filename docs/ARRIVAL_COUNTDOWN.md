# Arrival countdown display — TG-239

`Assets/Prefabs/UI/ArrivalCountdownDisplay.prefab` is a reusable world-space
display for an **in-game arrival event**. It uses scaled game time, so the existing
pause menu freezes the countdown.

## Setup

1. Drag the prefab into the desired scene or under a monitor/display mount.
2. Position and rotate its front toward the player. The panel is 1.14 m wide and
   0.48 m tall at its saved scale; adjust the root scale for the mounting surface.
3. On the root's **Arrival Countdown Display**, set **Duration Seconds**.
   The initial example is 10 seconds; supported values are 0 through 5999 (99:59).
4. Connect the approach event to `ArrivalCountdownDisplay.StartCountdown()`.
   Alternatively, enable **Play On Start** to begin when the instance first activates.
5. Connect **On Arrived** to the action that follows arrival.

The TextMeshPro references are already assigned. The prefab needs no scene camera
reference or input setup, and its graphics do not intercept pointer rays.
`Assets/Art/Materials/ArrivalCountdownText.mat` supplies the display's text styling.

## Behavior

- The display rounds remaining time up and shows `MM:SS`.
- At completion it holds `00:00`, changes the heading to **ARRIVED**, and invokes
  **On Arrived** once per started countdown.
- `StartCountdown(float)` starts a fresh countdown with a supplied duration.
- `PauseCountdown()` / `ResumeCountdown()` pause and resume this instance.
- `ResetCountdown()` stops and restores the Inspector duration without firing arrival.
- Disabling the component or its GameObject freezes progress until re-enabled.
- The saved prefab waits for a start trigger by default. It is not yet placed or
  connected to a scene-specific arrival sequence.

## Check in Unity

1. Drop the prefab into a temporary scene, enable **Play On Start**, and enter Play
   mode. Check that `00:10` counts down to `00:00` and the heading becomes **ARRIVED**.
2. Open the existing pause menu during a countdown. Verify the time freezes and
   resumes when the menu closes.
3. Connect **On Arrived** to a visible action; check it runs once, including after
   waiting several extra seconds. Restart the countdown and check it fires again.
4. Try durations of 0, 1, 61, and 5999 seconds; expect immediate arrival, `00:01`,
   `01:01`, and `99:59`, respectively.
5. Check readability and placement in the headset at the intended viewing distance.

## Validation performed

Verified in Unity 6000.3.8f1: script compilation, saved prefab references,
world-space rendering, formatting and duration bounds, immediate completion,
pause/resume with both local controls and `Time.timeScale = 0`, inactive-object
pause, reset, restart, and one arrival event per completed countdown. Rendered
and visually reviewed the prefab preview. Headset readability and scene-specific
arrival wiring still need to be checked when the prefab is placed.
