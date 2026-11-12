# US 218: Intro integration

Bedroom contains StoryDirector, StoryDateCard, and ObjectiveHighlight on the
StoryDirector object. The director only starts in Story mode.

- Edit Intro Date Text in `Assets/Resources/StoryText.asset`. The date is
  `October 13, 2023`: the launch-day setting in the repository's `Script.txt`,
  using the actual launch date confirmed by [NASA](https://science.nasa.gov/mission/psyche/mission-overview/).
- Intro Spacecraft is assigned to `StorySpacecraftDisplay`, a grabbable display
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

## US 361: Phase 2 — grab trigger and teleport timing support

The Intro spacecraft uses PsycheGrabbable with the shared DeskToy profile and a
box collider fitted to its meshes. Its rigidbody uses the normal DeskToy physics
for grabbing and releasing. StoryDirector subscribes to selectEntered
only during Intro, advances to Memory on grab, and hides the objective through
its existing phase presentation. Disabling the director or leaving Intro removes
the listener. Returning to Intro restores it. Event mode does not advance.

BlinkTeleporter.TeleportTo(anchor, fadeOutDuration, holdDuration, fadeInDuration)
accepts durations in unscaled seconds for one trip. The original one-argument
method still uses its serialized settings (defaults: 0.35 / 0.05 / 0.35 seconds).
A future memory trip can pass 1.5 / 0.8 / 1.5 without changing ordinary blinks.

This implements tasks 1 and 2 only. The grab currently ends in Memory; the bedroom
teleport, arrival date card, and automatic advance to Build are subsequent tasks.

Regression coverage includes grab event wiring, repeated grabs, disabled and
Event-mode guards, Intro re-entry, and a PlayMode custom teleport followed by an
ordinary blink while timeScale is zero. Headset verification remains necessary
for the display's reachability and grab feel.
