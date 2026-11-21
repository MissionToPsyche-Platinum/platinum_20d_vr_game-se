# Story integration: Intro (US 218), Build (US 220), and Launch (US 230)

Bedroom contains StoryDirector at the scene root and a Story-only group "Story"
(ModeGate Story) holding StoryDeskStart, the StoryDateCard prefab and the
ObjectiveHighlight prefab (Assets/Prefabs/Story). The director's Date Card and
Objective Highlight fields point at those two prefab instances. The director only
starts in Story mode.

- Edit Intro Date Text in `Assets/Resources/StoryText.asset`. The date is
  `October 13, 2023`: the launch-day setting in the repository's `Script.txt`,
  using the actual launch date confirmed by [NASA](https://science.nasa.gov/mission/psyche/mission-overview/).
- Intro Spacecraft is assigned to `StorySpacecraftDisplay`, a grabbable display
  assembled from the four existing spacecraft meshes beside the launch button.
  Its ModeGate hides it in Event mode; the puzzle assemblies are unchanged.
- StoryDateCard (TG-342) fades in at eye height in front of the player, follows
  the head like the Event cards, holds for 4 seconds and fades out by itself.
  Hide ends it early. Each Show places it in front of the player again.
- ObjectiveHighlight (TG-342) is a chevron that bobs above the top centre of the
  target's meshes (not its pivot, which can sit off the model) and turns toward
  the player. No target means no chevron.
- Both switch their own GameObject off when hidden, so keep each on its own
  object, never on the director's.
- Entering Intro calls both Show methods. Leaving Intro hides both. Re-entering
  Intro shows them again. Later phases call the same Show(string)/Show(Transform)
  and Hide().

## Automated verification

### Build / task 228

Entering Build finds an active PuzzleCompletion in the director's scene with
the same puzzle scope. The existing Kiosk watcher is disabled in Story mode, so the director adds a watcher
to its own object if needed. That watcher discovers the active puzzle zones in
Start, including pieces already snapped before Build. Watchers ignore zones in
other loaded scenes. When Bedroom Puzzle is assigned, the watcher only counts
active tagged zones under that root; other puzzles cannot advance the story.
Unscoped kiosk watchers retain their existing scene-wide behavior.

The director checks IsComplete before subscribing to Completed, then calls
Advance on completion to enter ChildLaunch. Leaving Build or disabling the
director removes the subscription; re-enabling during Build checks completion
again. InstructionTextManager continues handling individual piece explanations.

Regression tests cover completion during Build, completion before Build,
disable/re-enable catch-up, leaving and re-entering Build, and creating a Story
watcher without activating the kiosk. PlayMode tests exercise watcher startup
with an already-solved zone, a snap notification after entering Build, and
disable/re-enable catch-up through Unity's real lifecycle.

Verified November 21, 2026 in Unity 6000.3.8f1 batch mode: all 23 Story EditMode
tests and all 16 Story PlayMode tests passed on the combined Phase 3/4/5 branch. Diff checks passed. The EditMode
catch-up test explicitly invokes OnDisable/OnEnable, just as the fixture invokes
Start, because StoryDirector does not execute in EditMode. The PlayMode catch-up
test verifies those callbacks through Unity itself. The Bedroom gameplay and
headset checks below remain outstanding.

Manual acceptance check: open Bedroom in Story mode, press F8 twice to enter
Build, and finish the spacecraft puzzle. Expect `[StoryDirector] Entering
ChildLaunch.` once. Repeat with the puzzle finished before entering Build;
ChildLaunch should follow immediately (or when the new watcher starts). Check
that snapped pieces still show their explanations. Event mode should retain its
existing kiosk behavior.

### Intro

In Unity's Test Runner, run `PsycheVR.Story.Tests` under EditMode and
`PsycheVR.Story.PlayModeTests` under PlayMode. The PlayMode assembly is editor-only
because the F8 shortcut is intentionally excluded from player builds.

The three original Phase 1 PlayMode tests use Unity's real Start/Update lifecycle and the Input
System's isolated keyboard fixture. They verify Intro text and highlighting,
F8 progression through all six phases, one transition per press (including a
held-key check), stopping at FreePlay, Event-mode inactivity, and startup without
a keyboard. They create a small runtime fixture rather than loading Bedroom, with the date
card and highlight on child objects as in the scene;
the saved mode and input state are restored afterward.

Verified in Unity 6000.3.8f1 on November 7, 2026, with the final card and highlight: all
EditMode and PlayMode Story tests passed.

## Scene and headset check still required

Open Bedroom in Story mode and verify the Intro date label and spacecraft ring.
Grab the display to run Memory and automatically enter Build. Once Build appears
in the Console, focus the Game view and press F8 three times to reach FreePlay;
another press should do nothing. F8 is a debug phase skip, so do not press it during
the memory trip when checking the normal flow. Event mode should show neither
story presentation. Restore any temporary mode/target settings afterward.

Story EditMode tests cover the phase sequence, mode gating, editable resource,
Intro Show/Hide/re-entry, and an unassigned target. Automated tests do not verify
the presentation's readability in the headset. The scene references and a render
from the starting view were checked locally; missing unrelated room assets limit
the completeness of that desktop preview. Fetch the spacecraft FBX through Git
LFS before opening the scene if it is still a pointer in a fresh checkout.

## US 361: Phase 2 — memory transition

The Intro spacecraft uses PsycheGrabbable with the shared DeskToy profile and a
box collider fitted to its meshes. Its rigidbody uses the normal DeskToy physics
for grabbing and releasing. StoryDirector subscribes to selectEntered
only during Intro, advances to Memory on grab, and hides the objective through
its existing phase presentation. Disabling the director or leaving Intro removes
the listener. Returning to Intro restores it. Event mode does not advance.

BlinkTeleporter.TeleportTo(anchor, fadeOutDuration, holdDuration, fadeInDuration)
accepts durations in unscaled seconds for one trip. The original one-argument
method still uses its serialized settings (defaults: 0.35 / 0.05 / 0.35 seconds).
The memory trip uses 1.5 / 0.8 / 1.5 without changing ordinary blinks.

StoryDirector defers the trip until the next frame so the XRI grab event finishes
before the teleporter releases both hands. The scene references the rig's existing
BlinkTeleporter and a dedicated StoryBedroomArrival at the normal bedroom landing
position and yaw. The per-trip completion callback runs only after fade-in finishes.
On arrival, the director shows StoryText.BuildDateText (October 2003), advances to Build,
and keeps the date visible for memoryDateDuration (2.5 unscaled seconds by default).
Memory therefore lasts only for the trip, not the date-card display time.

The memory card uses the current camera view by default, rather than the Intro
anchor back at mission control. Assign Memory Date Anchor to use a fixed bedroom
placement. The Intro anchor is preserved for replay. Fade timings and date display
duration are serialized on StoryDirector for headset tuning. Leaving Memory or
disabling the director cancels its pending/active trip; leaving Build or disabling
the director clears the card timer. Cancelled trips do not advance the story.

Regression coverage includes grab event wiring, repeated grabs, disabled and
Event-mode guards, Intro re-entry, and a PlayMode custom teleport followed by an
ordinary blink while timeScale is zero. PlayMode coverage also checks the full
grab → Memory → bedroom → date → Build sequence, date placement and expiry,
repeat-grab protection, and cancellation before arrival.

Headset check: enter Story mode, grab the mission-control display, and confirm a
slow fade into the bedroom with readable October 2003 text. Expect one Memory log and one
Build log. Verify the ordinary room-switch blink still feels unchanged. Headset
verification of reachability, landing comfort, and date readability remains pending.

## TG-397: Bedroom scene integration verification

`BedroomStoryIntegrationTests` opens the saved `Assets/Scenes/Bedroom.unity` in an
Editor preview scene and closes it after the fixture. It does not save the scene,
change game mode, or replace the user's open scenes. These tests inspect real scene
objects and resolved prefab overrides; they complement the small PlayMode fixtures.

The five scene checks cover:

- One active story director and a grabbable Intro target with a rigidbody, shared
  grab settings, enabled grab colliders, and Story-only visibility.
- The live rig's teleporter, camera-mounted fade overlay, both hand interactors,
  and the ordinary room-switch route sharing the same teleporter.
- A dedicated StoryBedroomArrival near the normal bedroom landing pose, separate
  from the ordinary anchor and away from mission control. Position/yaw tolerances
  allow modest headset tuning without fixing every transform value in a test.
- Resolved ordinary blink timings of 0.35 / 0.05 / 0.35 seconds, plus finite, slower
  memory timings. This checks Bedroom's prefab overrides, not just C# defaults.
- The arrival date text resource, date-card and highlight references, a positive finite
  date display duration, and either a bedroom placement anchor or one active
  MainCamera for camera-relative placement.

Run `PsycheVR.Story.Tests` in Unity Test Runner's EditMode tab and
`PsycheVR.Story.PlayModeTests` in its PlayMode tab. The latter covers the runtime
sequence, repeat-grab protection, arrival card expiry, and cancellation.

These automated checks do not simulate a physical controller grab in the full
Bedroom scene or establish visual readability, obstacle clearance, or VR comfort.
The scene/simulator walkthrough and headset tuning remain separate checks.

Verified November 14, 2026, with Unity 6000.3.8f1 in batch mode:
26/26 Story EditMode tests passed (including all five Bedroom scene checks), and
6/6 Story PlayMode tests passed. No scene or gameplay configuration changes were
needed. This records automated verification only; no manual simulator or headset
walkthrough was performed as part of TG-397.

## TG-231 / TG-232: Child launch and real launch

Bedroom now wires both launch phases through StoryDirector:

- **Bedroom Puzzle** is the bedroom's `Snappable (1)` root. While in Build,
  the Phase 3 PuzzleCompletion watcher advances to ChildLaunch when all active
  tagged SnapZones are complete. Untagged zones and
  snaps from other puzzle roots do not count. Re-enabling the director checks
  for a puzzle completed while it was disabled.
- **Child Launch Button** is `StoryChildLaunchButton` beside that puzzle on the
  bedroom desk. It is visible only in Story mode, and `Start Video On Press` is
  disabled. ChildLaunch unlocks it; pressing it advances to RealLaunch and
  starts the return teleport.
- **Story Desk Return** is the existing mission-control teleport anchor, renamed
  `StoryDeskReturn`. The return uses the same per-trip `TeleportTo` as the
  memory trip: 1.5 s fade out, 0.8 s hold, 1.5 s fade in (`returnFadeSeconds`,
  `returnHoldSeconds`). Normal debug teleport durations remain unchanged. If the
  return trip is replaced or cancelled, its `onCancelled` callback unlocks the
  real button anyway, so the story cannot stay stuck in RealLaunch.
- The real button stays locked during the return. After the fade finishes, the
  date card shows StoryText.LaunchDateText (**October 2023**) in front of the player,
  and the real button unlocks.
- **Real Launch Button** references the button in `Player_Desk_setup (1)`.
  Both its video target and **Launch Video** reference the big-screen player.
  The video reference points to the player in develop's mission-control shell
  prefab. Both launch buttons remain slap-only; their select events have no
  persistent actions. The real button retains develop's SlapKey, including its
  sound, movement, and haptics. The child button uses the existing hand-path
  detection and suppresses video playback.
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
slap-only configuration. Run `PsycheVR.Story.PlayModeTests` in PlayMode. The launch tests cover
puzzle completion, slow-fade rig movement and yaw, date text, video suppression
on the child button, phase gating, stale completion events, lifecycle handling,
and Event-mode isolation. Fixture tests emit snap/completion notifications;
they do not replace a real puzzle interaction or video-decoding/headset check.

For a manual check, enter Story mode, reach Build, and complete the bedroom
puzzle. Verify ChildLaunch logs and the bedroom button works. Press it: the rig
should fade back to mission control, show October 2023, and unlock the real
button. Press the real button and let the video end: FreePlay should log once.
Repeat Event mode's normal puzzle/video flow to check for regressions.

**Earlier phases:** Phase 2 (Intro grab → Memory trip → Build, PR 80) is merged
into this branch, so the story runs from Intro to FreePlay without F8.

### Combined Phase 3/4/5 status — November 21, 2026

Phase 3, including its EditMode lifecycle fix, is merged into this branch.
StoryDirector uses PuzzleCompletion's IsComplete/Completed contract for Build
instead of maintaining a second snap-completion implementation. The Bedroom
Puzzle reference scopes the watcher to the intended puzzle. Launch integration
tests wait for watcher startup and verify that a tagged unrelated puzzle cannot
advance Build.

Unity 6000.3.8f1 batch results: 23/23 Story EditMode tests and 16/16 Story PlayMode
tests passed. This includes the Bedroom scene-reference test and the combined
Build, child-launch return, and video-completion fixtures.

Merged origin/develop at 9826ada after fetching on November 21. Bedroom keeps
its updated date-card/highlight prefabs and room assets, with the story launch
references added to those objects. ButtonController preserves slap-only input,
uses SlapKey.Blocked for story gating, and exposes IsUnlocked independently of
XR selection. A PlayMode regression verifies physical-key unlock, story locks,
and one accepted press without enabling ray selection. Scene validation checks
the real VideoPlayer reference and the SlapKey wiring.

Full Intro/Memory progression and manual Bedroom/video/headset checks remain
outstanding. These fixture results do not establish end-to-end acceptance
without F8.
