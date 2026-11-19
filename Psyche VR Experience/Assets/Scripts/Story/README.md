# US 218: Intro integration

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
