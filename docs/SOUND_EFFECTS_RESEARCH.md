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

These are the moments where the player is directly touching or acting on a Psyche puzzle piece. In VR, these are the highest-value sounds — without audio feedback the player often can't tell whether a grab or snap actually registered, because their real hand doesn't feel the virtual object.

### 1.1 Grab & Hover

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Hover enter** | Controller points at or approaches a grabbable piece | UI cue, quiet | P1 | Very short tick or soft pulse. Should not fatigue — hover happens constantly. Must respect XR Interaction Toolkit's `hoverEntered` event on `XRGrabInteractable`. |
| **Grab start** | Player closes grip on a piece | Physical, tactile | **P0** | The core "I'm holding something" confirmation. Should feel like cloth-on-metal or a soft grip. Triggered by `selectEntered`. |
| **Grab fail / slip** | Grip released mid-motion by mistake | Physical, subtle | P2 | Optional — only useful if we see playtesters fumbling pieces. |

### 1.2 Release, Drop & Reject

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Release in mid-air** | Player lets go of a piece away from any snap zone | Physical | P1 | Pairs with a landing impact (see Section 2). The release itself can be silent if the landing covers it. |
| **Release over invalid zone** | Player drops a piece into a snap zone it doesn't belong to | Rejection cue | **P0** | Currently the game gives no audible "wrong part" signal. A soft low buzz or "nope" tone avoids frustration. Hook into `SnapZone.OnSelectExited` + tag-mismatch path. |
| **Piece lost off-table** | Piece falls to the floor / out of reach | Ambient, alarm-adjacent | P1 | Ties into TG-124 reset button. A subtle cue ("that piece is lost") could prompt the player to use reset. |

### 1.3 Snap & Lock-In

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Valid snap** | Piece enters correct snap zone and locks | Satisfaction, mechanical | **P0** | The single most important puzzle sound. Should feel like a magnetic click / latch. Triggered when `SnappableObject.isSnapped` flips to true. |
| **Snap zone armed (hover)** | Correct piece hovering over correct zone before release | UI cue, anticipatory | P1 | Gentle hum or rising pitch to signal "let go here". Avoid if it clashes with grab sound. |
| **Full puzzle completion** | All pieces snapped | Stinger, celebratory | **P0** | One-shot reward sound. Distinct from the chalkboard completion audio already shipped in TG-60 — or explicitly reuse that cue for consistency (see Open Questions). |

### 1.4 Reset Button (forward-looking, TG-124)

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Button press** | Player presses the in-world reset button | UI cue, mechanical | P1 | Borrow the `VideoPlayerButton` pattern — short click on `OnMouseDown`. |
| **Pieces returning home** | Unsnapped pieces teleport back to their starting positions | Magical / soft whoosh | P1 | One collective sound is better than one-per-piece (avoids a noisy cluster). |

### 1.5 Cross-References

- The **chalkboard display system** (TG-60/TG-62) already ships with completion audio and category progression cues. Any new puzzle sounds should be checked against those to avoid stepping on them in the mix.
- The **pause menu** (TG-108) uses UI-style button clicks. Reset button press should be *distinct* so the player can tell in-world vs. menu interactions apart.

## 2. Object & Material-Contact Sounds

*TBD*

## 3. Ambient & Environmental Sounds

*TBD*

## 4. Prioritization & Implementation Notes

*TBD*

## 5. Asset Source References

*TBD*
