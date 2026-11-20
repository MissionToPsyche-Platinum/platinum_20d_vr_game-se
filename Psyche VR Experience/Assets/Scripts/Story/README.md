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

## TG-231 / TG-232: Child launch and real launch

Bedroom now wires both launch phases through StoryDirector:

- **Bedroom Puzzle** is the bedroom's `Snappable (1)` root. While in Build,
  completing all tagged SnapZones advances to ChildLaunch. Untagged zones and
  snaps from other puzzle roots do not count. Re-enabling the director checks
  for a puzzle completed while it was disabled.
- **Child Launch Button** is `StoryChildLaunchButton` beside that puzzle on the
  bedroom desk. It is visible only in Story mode, and `Start Video On Press` is
  disabled. ChildLaunch unlocks it; pressing it advances to RealLaunch and
  starts the return teleport.
- **Story Desk Return** is the existing mission-control teleport anchor, renamed
  `StoryDeskReturn`. The rig's BlinkTeleporter fades out/in over 1.5 seconds each
  for this transition. Normal debug teleport durations remain unchanged.
- The real button stays locked during the return. After the fade finishes, the
  date card shows **October 2023** at the existing mission-control date anchor,
  and the real button unlocks.
- **Real Launch Button** references the button in `Player_Desk_setup (1)`.
  Both its video target and **Launch Video** reference the big-screen player.
  Its select event calls only PressButton; the stale direct StartVideo override
  was removed so playback has one owner for both XR selection and hand slaps.
- Pressing the real button hides the date. Completion of that video's existing
  UnityEvent advances RealLaunch to FreePlay. Completion during the return or
  in another phase is ignored. Listeners are removed while the director is
  disabled and on destruction.

ButtonController's story gate prevents a case opening or direct PressButton
call from bypassing phase locks. The gate defaults to allowing normal behavior;
Event mode never enables story control. The Event room's existing kiosk/video
configuration and shared prefabs are unchanged.

### Verification

Run `PsycheVR.Story.Tests` in EditMode, including `StoryLaunchSceneTests`, which
loads Bedroom and validates the actual prefab references and persistent button
bindings. Run `PsycheVR.Story.PlayModeTests` in PlayMode. The launch tests cover
puzzle completion, slow-fade rig movement and yaw, date text, video suppression
on the child button, phase gating, stale completion events, lifecycle handling,
and Event-mode isolation. Fixture tests emit snap/completion notifications;
they do not replace a real puzzle interaction or video-decoding/headset check.

For a manual check, enter Story mode, reach Build, and complete the bedroom
puzzle. Verify ChildLaunch logs and the bedroom button works. Press it: the rig
should fade back to mission control, show October 2023, and unlock the real
button. Press the real button and let the video end: FreePlay should log once.
Repeat Event mode's normal puzzle/video flow to check for regressions.

**Earlier-phase dependency:** Intro/Memory automatic progression is not wired
on this branch. Phase 4/5 tests enter Build directly; the full story's acceptance
criterion of running from Intro without F8 still depends on the earlier-phase
integration. Do not mark that end-to-end criterion complete from these tests.

### Checkpoint status — November 20, 2026

Development is paused for integration of the Phase 2/3 work into develop.
Scene structural checks found no duplicate object IDs or unresolved local
references. The PlayMode test sources compiled, and an intermediate runtime
compilation passed. The Unity test run was stopped during startup while
offloaded iCloud cache files were being restored; no runtime test pass is
claimed. The standalone EditMode compile encountered a stale cached response
file referencing a missing `BedroomStoryIntegrationTests.cs`.

After integrating the earlier phases, recompile the final code and run both
story test suites, inspect the bedroom button placement, and verify the full
story and Event-mode flows in the scene/headset before closing the tasks.
