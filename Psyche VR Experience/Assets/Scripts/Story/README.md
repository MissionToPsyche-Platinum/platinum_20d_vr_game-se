# US 218: Intro integration

Bedroom contains StoryDirector, StoryDateCard, and ObjectiveHighlight on the
StoryDirector object. The director only starts in Story mode.

- Edit Intro Date Text in `Assets/Resources/StoryText.asset`. The current text,
  `[Intro date pending]`, is intentionally a placeholder.
- Assign the model spacecraft Transform to StoryDirector's Intro Spacecraft
  field. It is intentionally unassigned until the target is confirmed.
- StoryDateCard is a temporary world-space TextMeshPro label. With no custom
  label assigned, it creates one in front of Camera.main on first Show.
- ObjectiveHighlight is a temporary cyan ring centered on the assigned target.
  No target means no ring. This is not Zach's final glow prefab.
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

Open Bedroom in Story mode, verify the date label, and assign a spacecraft to
verify its ring. Focus the Game view and press F8 five times to reach FreePlay;
another press should do nothing. Each phase should log once. Event mode should
show neither presentation. Restore any temporary mode/target settings afterward.

Story EditMode tests cover the phase sequence, mode gating, editable resource,
Intro Show/Hide/re-entry, and an unassigned target. Automated tests do not verify
the final spacecraft assignment or the presentation's readability in the headset.
