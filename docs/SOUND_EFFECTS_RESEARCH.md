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

These are the passive physics sounds — what you hear when objects hit, slide, or tumble against surfaces. Currently the puzzle pieces are completely silent when dropped, collided, or dragged. In VR this is especially jarring because the player sees a metal spacecraft part hit a wooden desk and hears… nothing.

All of these would hook into Unity's `OnCollisionEnter` / `OnCollisionStay` callbacks on the piece Rigidbodies (via a new lightweight `CollisionAudio` component, or added to `PsycheGrabbable`). Velocity magnitude at impact determines volume and clip selection.

### 2.1 Piece-on-Surface Contacts

| Contact Pair | Description | Sound Character | Priority | Notes |
|---|---|---|---|---|
| **Metal piece → desk/table** | Piece dropped or set down on the wooden work surface | Metallic tap on wood, thunk | **P0** | The most frequent physics contact in the experience. Needs 3-4 clip variants to avoid the "machine-gun" effect of the same sample repeating. Scale volume by `relativeVelocity.magnitude`. |
| **Metal piece → floor** | Piece falls off the table to the bedroom floor | Heavier metallic clang on carpet/tile | P1 | Should sound "further away" and more alarming than table contact — reinforces that the piece is lost. Pairs with Section 1 "piece lost" cue. |
| **Piece set down gently** | Player carefully places a piece (low velocity) | Soft tap, barely audible | P1 | Velocity threshold below ~0.3 m/s — use a quieter, softer clip variant rather than just reducing volume. |

### 2.2 Piece-on-Piece Contacts

| Contact Pair | Description | Sound Character | Priority | Notes |
|---|---|---|---|---|
| **Metal piece → metal piece** | Two loose pieces collide while being moved around | Light metallic clink | P1 | Shorter and thinner than piece-on-table. Tag-check both colliders to confirm both are `SnappableObject`s. |
| **Piece dragged across surface** | Player slides a piece along the desk without lifting | Scraping / sliding metallic | P2 | Uses `OnCollisionStay` with velocity check. Can be fatiguing — only add if playtesting shows it feels empty without it. |

### 2.3 Other Object Contacts

| Contact Pair | Description | Sound Character | Priority | Notes |
|---|---|---|---|---|
| **Pedestal / model base** | Assembled Psyche model interacted with on its stand | Solid, heavy metallic | P2 | Only relevant if the finished model is interactable post-completion. Low priority unless the design calls for a "admire your work" moment. |
| **Chalkboard interaction** | Physical touch or tap on the chalkboard surface | Chalk-on-slate tap | P2 | The chalkboard is currently a display, not a physics object. Only needed if we add physical grab/touch interaction to it later. |

### 2.4 Implementation Considerations

- **Clip variation:** Any contact sound that can trigger more than once per second needs 3+ clip variants played via `AudioSource.PlayOneShot` with slight random pitch shift (0.95–1.05). This avoids the uncanny "machine-gun" repetition.
- **Velocity gating:** Contacts below ~0.1 m/s should be silent (micro-jitter from physics solver). Contacts above ~2 m/s can share a single "hard impact" clip.
- **Spatial audio:** All contact sounds should use 3D `AudioSource` settings (spatial blend = 1.0) so they localize to the impact point. The bedroom is small enough that attenuation rolloff can be tight (1–5m).
- **No existing collision audio system:** The project currently has no `OnCollisionEnter` audio hooks anywhere in custom scripts. This would be a net-new system, which affects the effort estimate in Section 4.

## 3. Ambient & Environmental Sounds

Ambient audio is the bed of the mix — the sounds the player isn't consciously listening for, but whose absence makes the scene feel flat, sterile, or "empty VR demo." The bedroom scene currently runs in dead silence, which is arguably the single biggest immersion gap in the experience right now.

Unlike Sections 1 and 2, these sounds are mostly looping or one-shot atmospherics triggered by scene state rather than direct player action.

### 3.1 Room Tone & Atmosphere

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Bedroom room tone** | Continuous low-level ambience for the bedroom scene | Looping ambient bed | **P0** | Subtle HVAC hum + distant-outside murmur. Single stereo loop, very low volume (-30 dB), non-spatial. Sets the whole scene; the cheapest win in the entire doc. |
| **Monitor / computer hum** | Electronics in the mission-control-adjacent setup | Looping ambient, spatial | P1 | 3D source near the monitor prefabs. Ties the TG-106/TG-111 lit-up screens into the soundscape. |
| **Ceiling light buzz** | Fluorescent or LED hum near light fixtures | Looping ambient, spatial, very quiet | P2 | Only noticeable near the ceiling lights. Realistic but easily feels like a bug if too loud. |

### 3.2 Scene & State Transitions

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Scene fade-in** | Start of the bedroom scene / after a TG-28 fade | Soft riser / welcome swell | P1 | Hooks into the TG-54 fade manager once it lands. Should be brief (< 1.5s) and not repeat on every transition. |
| **Scene fade-out** | Leaving the scene / transition to next area | Soft fall / breath-out | P1 | Mirror of fade-in. Also hooks TG-54. |
| **Puzzle reset triggered** | Global "state is resetting" moment (TG-124) | Magical whoosh bed | P1 | Distinct from the per-piece return sound in Section 1 — this is the *scene* response, a soft sweep that unifies the individual piece sounds. |
| **First piece picked up** | One-time cue when the player first engages the puzzle | Discovery stinger | P2 | Optional narrative beat — signals "the experience has started." Low priority; easy to cut. |

### 3.3 Chalkboard & Tooltip Ambience

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Chalkboard idle presence** | Subtle indicator that the chalkboard is "on" | Looping, very quiet | P2 | Possibly a faint chalk-dust or paper-rustle loop. Only if playtesters miss the chalkboard; TG-60 already ships strong visual presence. |
| **Tooltip appear** | Text updates on the chalkboard as a piece is grabbed | UI cue, short | P1 | Soft "writing" or chalk-tap sound. Should feel like something *happened*, not intrude. Must not fire on every hover — debounce to significant content changes. |
| **Category progression** | Moving between info categories on the chalkboard | UI cue, page-turn-like | P1 | Already partially covered by TG-60's existing audio — verify before adding new clips. |

### 3.4 Environmental "Life" Details

| Moment | Description | Category | Priority | Notes |
|---|---|---|---|---|
| **Outside world hint** | Very distant suggestion of a world beyond the bedroom | Looping, low-frequency, non-spatial | P2 | Anchors the scene in reality. Could be distant wind, faint traffic, or nothing at all depending on the narrative (space facility vs. home office). Decision ties to TG-96 exterior environment work. |
| **Bus / vehicle cue** | Referenced in `BasketballPhysics.cs` and the "bus through floor" bug fix — is there a bus in-scene? | TBD | P2 | Flag for scope clarification. If there is a bus/vehicle prop in the bedroom, it may warrant its own ambient source. |

### 3.5 Ambient Audio Guidelines

- **One bed, not five:** The room tone should be a single unified loop. Layering too many ambient loops creates a muddy, tiring mix and wastes memory.
- **Ducking on interaction:** When a P0 interaction sound plays (snap, valid grab), the ambient bed should duck by 3–6 dB for ~0.5s. This pushes the action forward in the mix without needing louder SFX.
- **Mute on pause:** When the TG-108 pause menu is open, ambient loops should pause (not just duck). Use a global `AudioMixerSnapshot`.
- **No music here:** This doc is explicitly not proposing soundtrack. If the design later adds music, the ambient guidelines above will need revisiting because music + room tone + loops competes quickly.

## 4. Prioritization & Implementation Notes

This section consolidates the P0/P1/P2 candidates from Sections 1–3 into a sequenced rollout plan, with rough effort sizing and technical notes for whoever picks up the follow-up implementation ticket.

### 4.1 Consolidated Priority List

**P0 — must ship (core feedback gaps)**

| # | Sound | Section | Why it's P0 |
|---|---|---|---|
| 1 | Valid snap / lock-in | 1.3 | Single most important puzzle sound. Without it the player can't tell a snap registered. |
| 2 | Grab start | 1.1 | Core "I'm holding something" confirmation. VR hands don't feel real without it. |
| 3 | Release over invalid zone | 1.2 | Currently silent; causes confusion and frustration when wrong parts don't snap. |
| 4 | Full puzzle completion | 1.3 | Reward payoff for the whole experience. |
| 5 | Metal piece → desk/table | 2.1 | Most frequent physics event in the experience, currently silent. |
| 6 | Bedroom room tone | 3.1 | Kills the "sterile VR demo" feel; a single cheap loop. |

**P1 — should ship (quality/immersion)**

Hover cue, release-in-air landing, snap zone armed hum, piece-lost cue, reset button press, pieces returning home, piece-on-floor impact, gentle placement variant, piece-on-piece clink, scene fade-in/out, reset scene whoosh, tooltip appear, category progression verification, monitor hum.

**P2 — nice-to-have**

Grab fail/slip, pedestal contact, chalkboard physical touch, sliding/drag audio, ceiling light buzz, first-piece discovery stinger, chalkboard idle loop, outside-world hint, bus/vehicle ambient (pending scope clarification).

### 4.2 Rollout Phases

**Phase 1 — Core Feedback (P0 only)**
Ships the 6 sounds above. Target outcome: the puzzle *feels responsive*. Estimated effort: ~1 sprint including asset sourcing. Deliverable: one PR per category (interaction + contact + ambient) to keep review tractable.

**Phase 2 — Quality Pass (high-value P1s)**
Hover, release landing, reset button, fade transitions, tooltip appear. Estimated effort: ~half a sprint. Requires TG-54 fade manager to be merged first.

**Phase 3 — Polish (remaining P1 + selective P2)**
Driven by playtest feedback. Only add P2 items that specifically address observed confusion or flatness.

### 4.3 Technical Architecture Sketch

The implementation ticket will need to stand up audio infrastructure that doesn't currently exist:

1. **`AudioMixer` asset** with buses: `Master`, `Interaction`, `Physics`, `Ambient`, `UI`. Enables Section 3.5 ducking and pause behavior.
2. **`CollisionAudio` component** — generic MonoBehaviour attached to any Rigidbody that should emit contact sounds. Holds a `List<CollisionAudioEntry>` mapping tag-pair → clip array → velocity curve.
3. **`SnapZoneAudio` component** — subscribes to `SnapZone` events (valid snap, invalid drop, hover enter/exit). Keeps audio logic out of `SnapZone.cs` itself so gameplay code stays decoupled.
4. **`AmbientLoopManager`** — singleton that owns looping beds, handles scene-load starts, and exposes `Duck(float dB, float seconds)` for interaction moments.
5. **`CompletionAudio` hook** — listens for all `SnappableObject.isSnapped` flipping true and detects the "all snapped" state to fire the completion stinger. Could live on the future reset button manager.

### 4.4 Asset Count Estimate

Rough guess for Phase 1 assets:

| Category | Unique clips | Variants each | Total files |
|---|---|---|---|
| Snap / lock-in | 1 | 2–3 | 3 |
| Grab start | 1 | 3 | 3 |
| Invalid drop | 1 | 1 | 1 |
| Completion stinger | 1 | 1 | 1 |
| Piece → desk | 1 | 4 | 4 |
| Room tone | 1 | 1 (loop) | 1 |
| **Phase 1 total** | — | — | **~13 files** |

Phase 2 roughly doubles this. All-in, the project is likely looking at ~30–40 audio files if all P0+P1 ship.

### 4.5 Decisions Needed Before Implementation Starts

1. **Audio budget** — confirm build size and memory headroom for ~30–40 small WAV/OGG files.
2. **Audio mixer ownership** — who maintains the mixer asset and sets bus levels. Recommend one owner to avoid drift.
3. **Chalkboard audio reuse vs. new clips** — decide whether the completion stinger reuses the TG-60 chalkboard sound or is distinct. Listed as an open question in the doc header.
4. **Scope of "the bus"** — clarify whether the reference in `BasketballPhysics.cs` / floor-phasing fix reflects an in-scene prop that needs its own audio treatment.

## 5. Asset Source References

*TBD*
