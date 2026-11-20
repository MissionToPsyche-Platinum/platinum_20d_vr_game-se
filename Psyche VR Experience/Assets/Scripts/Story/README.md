# US 218: Intro integration

Bedroom contains StoryDirector, StoryDateCard, and ObjectiveHighlight on the
StoryDirector object. The director only starts in Story mode.

- Edit Intro Date Text in `Assets/Resources/StoryText.asset`. The date is
  `October 13, 2023`: the launch-day setting in the repository's `Script.txt`,
  using the actual launch date confirmed by [NASA](https://science.nasa.gov/mission/psyche/mission-overview/).
- Intro Spacecraft is assigned to `StorySpacecraftDisplay`, a static display
  assembled from the four existing spacecraft meshes beside the launch button.
  Its ModeGate hides it in Event mode; the puzzle assemblies are unchanged.
- StoryDateCard is a temporary world-space TextMeshPro label. With no custom
  label assigned, it creates one at `StoryIntroDateAnchor`. The anchor is above
  the desk in the starting view. Each Show reapplies the position; scenes without
  an anchor use the current camera position.
- ObjectiveHighlight is a temporary cyan ring centered on the assigned target.
  It fits the visible mesh bounds and follows the model; no target means no ring.
  This is not Zach's final glow prefab.
- Entering Intro calls both Show methods. Leaving Intro hides both. Re-entering
  Intro shows them again. Replace the temporary presentations with the final
  visuals while preserving Show(string)/Show(Transform) and Hide().

## Automated verification

In Unity's Test Runner, run `PsycheVR.Story.Tests` under EditMode and
`PsycheVR.Story.PlayModeTests` under PlayMode. The PlayMode assembly is editor-only
because the F8 shortcut is intentionally excluded from player builds.

The three PlayMode tests use Unity's real Start/Update lifecycle and the Input
System's isolated keyboard fixture. They verify Intro text and highlighting,
F8 progression through all six phases, one transition per press (including a
held-key check), stopping at FreePlay, Event-mode inactivity, and startup without
a keyboard. They create a small runtime fixture rather than loading Bedroom;
the saved mode and input state are restored afterward.

Verified in Unity 6000.3.8f1 on November 6, 2026: all three PlayMode tests passed.

## Scene and headset check still required

Open Bedroom in Story mode and verify the date label and display spacecraft ring. Focus the Game view and press F8 five times to reach FreePlay;
another press should do nothing. Each phase should log once. Event mode should
show neither presentation. Restore any temporary mode/target settings afterward.

Story EditMode tests cover the phase sequence, mode gating, editable resource,
Intro Show/Hide/re-entry, and an unassigned target. Automated tests do not verify
the presentation's readability in the headset. The scene references and a render
from the starting view were checked locally; missing unrelated room assets limit
the completeness of that desktop preview. Fetch the spacecraft FBX through Git
LFS before opening the scene if it is still a pointer in a fresh checkout.

## TG-232: Real-launch phase integration

`StoryDirector` now unlocks its assigned **Real Launch Button** when entering
`RealLaunch`. It listens to **Launch Video**'s completion event and advances to
`FreePlay` only while in that phase. Earlier or repeated completions do not skip
phases. Disabling the director removes its listener; enabling it restores the
subscription. Event mode does not subscribe or unlock the story launch button.

`play_video.OnVideoCompleted` exposes the existing serialized UnityEvent, so
Inspector listeners continue to work. `ButtonController` initializes in `Awake`
so an unlock issued by another component's `Start` is not later overwritten.

### Scene hookup still required

Before editing Bedroom, coordinate scene ownership as described in
`CONTRIBUTING.md`. On the scene's StoryDirector, assign the mission-control
ButtonController to **Real Launch Button** and the big-screen `play_video` to
**Launch Video**. Assign that same player to the button's **Video To Start**;
avoid relying on the first-player fallback when the scene has multiple videos.
No scene or prefab references are changed by this code commit.

For an isolated Phase 5 check, enter Story mode and use F8 to reach `RealLaunch`.
The mission-control button should unlock. Press it and let the video finish;
the director should log `FreePlay` exactly once. Repeat in Event mode to confirm
the puzzle still unlocks the button and video playback behaves as before.
The automatic Phase 4 bedroom return remains separate work, so F8 is still
needed to reach this phase until the earlier phases are wired.

### Regression coverage

`StoryLaunchPlayModeTests` covers unlocking on phase entry, completion to
FreePlay, ignored completions in earlier phases, disable/re-enable lifecycle,
Event-mode isolation, unlocking before Start, and suppression of a second press
during animation. The tests use runtime fixture objects and invoke the player's
completion UnityEvent; they do not decode a video or verify headset visuals.

Validation for this change: the runtime assembly and PlayMode test sources
compile with Unity 6000.3.8f1's C# compiler. The attempted batch PlayMode run
could not execute tests: file-read timeouts in `Library` prevented Unity from
loading cached assemblies, including `UnityEditor.TestRunner`. Rerun the story
PlayMode suite in a working editor before marking TG-232 verified.
