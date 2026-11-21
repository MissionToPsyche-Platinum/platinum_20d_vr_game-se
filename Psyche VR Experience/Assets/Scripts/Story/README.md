# US 218: Intro integration

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
On arrival, the director shows StoryText.MemoryDateText (2003), advances to Build,
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
grab → Memory → bedroom → 2003 → Build sequence, date placement and expiry,
repeat-grab protection, and cancellation before arrival.

Headset check: enter Story mode, grab the mission-control display, and confirm a
slow fade into the bedroom with readable 2003 text. Expect one Memory log and one
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
- The 2003 text resource, date-card and highlight references, a positive finite
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
