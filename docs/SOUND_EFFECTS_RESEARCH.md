# Sound Effects Research — Psyche Puzzle Experience

**Ticket:** TG-122 (parent: TG-121 — Investigate potential sound effects for the puzzle experience)
**Status:** Draft / In Progress
**Owner:** Branden Chong

---

## Purpose

This document catalogs the actions, objects, and environmental moments in the Psyche VR puzzle experience that would benefit from dedicated sound effects. It is the research deliverable for TG-122 and is intended to feed into a later implementation ticket once the team agrees on scope and priority.

The goal is **not** to pick final audio files — that's an asset task. The goal is to answer:

1. *Where in the experience does silence currently hurt immersion or feedback?*
2. *Which of those moments are worth the effort to sound-design?*
3. *What's the rough category of sound each moment needs (UI cue, physical impact, ambient loop, etc.)?*

---

## Scope

**In scope**
- The Psyche spacecraft puzzle (assembly, snap zones, lost pieces, reset)
- The bedroom scene the puzzle lives in
- The chalkboard / tooltip display system
- Player interactions (grab, release, hover, button press)

**Out of scope**
- Mission Control scene audio (separate ticket)
- Voiceover / narration
- Music / soundtrack
- Final asset selection and licensing

---

## Evaluation Criteria

Each candidate sound is evaluated on:

| Criterion        | Question                                                              |
|------------------|-----------------------------------------------------------------------|
| **Feedback**     | Does it confirm a player action they might otherwise miss in VR?      |
| **Immersion**    | Does its absence break the "I'm really here" feeling?                 |
| **Discoverability** | Does it teach the player what's interactable or what just happened? |
| **Effort**       | How much work to source, tune, and wire up?                           |
| **Priority**     | P0 (must-have) / P1 (should-have) / P2 (nice-to-have)                 |

---

## Categories

The research is organized into the following sections. Each section will be filled in across subsequent commits.

1. **Puzzle Interaction Sounds** — grab, release, snap, reject, reset
2. **Object & Material-Contact Sounds** — piece-on-table, piece-on-piece, drops
3. **Ambient & Environmental Sounds** — bedroom room tone, chalkboard, background
4. **Prioritization & Implementation Notes** — what ships first, how it wires in
5. **Asset Source References** — where to look for free/licensed SFX

---

## Open Questions

- Do we have a target audio budget (file count, total MB) for the build?
- Is there an existing audio mixer / bus setup in the project, or do we stand one up as part of implementation?
- Should chalkboard UI sounds match the already-shipped tooltip completion audio, or be distinct?

---

*Sections below are placeholders and will be populated in follow-up commits.*

## 1. Puzzle Interaction Sounds

*TBD*

## 2. Object & Material-Contact Sounds

*TBD*

## 3. Ambient & Environmental Sounds

*TBD*

## 4. Prioritization & Implementation Notes

*TBD*

## 5. Asset Source References

*TBD*
