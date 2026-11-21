# Story integration: Intro (US 218), Build (US 220), and Launch (US 230)

Bedroom contains StoryDirector at the scene root and a Story-only group "Story"
(ModeGate Story) holding StoryDeskStart, the StoryDateCard prefab and the
ObjectiveHighlight prefab (Assets/Prefabs/Story). The director's Date Card and
Objective Highlight fields point at those two prefab instances. The director only
starts in Story mode.

- Edit Intro Date Text in `Assets/Resources/StoryText.asset`. The date is
  `October 13, 2023`: the launch-day setting in the repository's `Script.txt`,
  using the actual launch date confirmed by [NASA](https://science.nasa.gov/mission/psyche/mission-overview/).
- Intro Spacecraft is assigned to `StorySpacecraftDisplay`, a static display
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

The three PlayMode tests use Unity's real Start/Update lifecycle and the Input
System's isolated keyboard fixture. They verify Intro text and highlighting,
F8 progression through all six phases, one transition per press (including a
held-key check), stopping at FreePlay, Event-mode inactivity, and startup without
a keyboard. They create a small runtime fixture rather than loading Bedroom, with the date
card and highlight on child objects as in the scene;
the saved mode and input state are restored afterward.

Verified in Unity 6000.3.8f1 on November 7, 2026, with the final card and highlight: all
EditMode and PlayMode Story tests passed.

## Scene and headset check still required

Open Bedroom in Story mode and verify the date card and the chevron over the display spacecraft. Focus the Game view and press F8 five times to reach FreePlay;
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
  the Phase 3 PuzzleCompletion watcher advances to ChildLaunch when all active
  tagged SnapZones are complete. Untagged zones and
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
  date card shows **October 2023** in front of the player,
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

**Earlier-phase dependency:** Intro/Memory automatic progression is not wired
on this branch. Phase 4/5 tests enter Build directly; the full story's acceptance
criterion of running from Intro without F8 still depends on the earlier-phase
integration. Do not mark that end-to-end criterion complete from these tests.

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
