# Quality Plan results (template)

The team records Quality Plan test results in one shared Google Sheet, "Psyche Quality Results", linked from the team drive. This file is its template: the same metrics, checklists and columns, so a fresh sheet can be rebuilt from it. Results for the final build go into the Software Quality Report.

Every row records the build (version and version code), the date and the tester. Run every test on the release APK on the Meta Quest 3 unless a test says otherwise. Pass / Fail is one of Pass, Fail, Not run.

## Metrics

One row per metric. Columns after Test: Measured value, Pass / Fail, Build (version, code), Date, Tester, Notes.

| Metric | Description | Acceptance level | Req. | Test |
|---|---|---|---|---|
| Frame-Time Overruns | % of frames longer than 13.9 ms (one frame at 72 Hz) | Less than 1% | 1 | Performance Capture |
| Scene Load Time | Worst-case seconds from a scene load (app launch, mode switch, kiosk reset) to the first frame the player can act | 10 s or less | 2 | Performance Capture |
| Soak Crashes | Crashes or freezes during a continuous 2-hour Event mode run | 0 | 3 | Kiosk Soak Test |
| Sustained Frame Rate | Drop in average fps between the first and last 10 minutes of the 2-hour run | 5% or less | 1, 3 | Kiosk Soak Test |
| Misuse Recovery Rate | % of scripted misuse scenarios that end with the player able to keep playing | 100% | 4 | Misuse Scenario Test |
| Comfort Checklist Pass Rate | % of comfort checklist items passed | 100% | 5 | Comfort Review |
| Standard Binding Coverage | % of player actions on Meta Quest standard controls (grip and trigger both grab; a held object's action on the other button) | 100% | 6 | Control Mapping Audit |
| Severe Heuristic Findings | Unresolved findings rated severity 3 or 4 on Nielsen's scale | 0 | 7 | Heuristic Evaluation |
| Factual Errors | Unresolved factual errors in text, narration and models vs NASA, JPL and ASU Psyche sources | 0 | 8 | Content Fact Check |
| Latent Defect Count | Open Taiga defects at the final build, by severity (Fatal / Critical / Substantial / Minor) | 0 Fatal, 0 Critical | 9 | Defect Tracking Review |
| Third-Party IP Content | Assets whose license does not allow distribution in this project | 0 | 10 | Asset License Audit |
| Field Abnormal Exit Rate | % of logged field sessions with no session_end and a last record that is not a headset suspend | 0% | 3, 9 | Field Session Log Analysis |
| Event Completion Rate | % of Event sessions (with kiosk_first_input) logging kiosk_puzzle_completed before kiosk_clock_expired | 70% or higher | 12 | Field Session Log Analysis |
| Attribution Coverage | % of third-party assets with source, license and credit recorded in CREDITS.md | 100% | 11 | Asset License Audit |

## T3 Misuse scenarios

Each scenario in both modes. Pass = play continues without an app restart or developer help.

| # | Scenario |
|---|---|
| 1 | Remove the headset mid-session, then put it back on |
| 2 | Leave the play boundary, then return |
| 3 | Drop or throw every grabbable out of reach, through furniture and off the floor |
| 4 | Press every button during a scene load |
| 5 | Put down one controller and continue with the other |
| 6 | Block a controller's tracking, then restore it |

## T4 Comfort checklist

Two team members, each mode, from Meta's published VR comfort guidelines.

| # | Checklist item |
|---|---|
| 1 | No camera motion without head movement |
| 2 | No camera acceleration |
| 3 | Horizon stays level |
| 4 | No strobing or high-contrast flashing |
| 5 | Text and UI at a comfortable reading distance |
| 6 | Nothing spawns inside the player's head |
| 7 | Fades on every scene transition |

## T5 Control mapping

Columns: binding in `XRI Default Input Actions.inputactions`, what the headset does, Pass / Fail. Grab is on both the grip and the trigger (Meta Quest standard controls; the game has no separate use action, so the trigger would otherwise do nothing), and a held object's own action is on the other button. An action on a non-standard control, or one no grip, trigger or hand can reach, is a defect.

| # | Player action | Expected control |
|---|---|---|
| 1 | Pick up and hold an object | Grip or trigger |
| 2 | Drop or throw a held object | Release the button holding it |
| 3 | Click a held pen | The other button of the holding hand |
| 4 | Crumple a held paper sheet | The other button of the holding hand |
| 5 | Turn a book page | Grip or trigger on the page edge |
| 6 | Open a drawer or the glass case | Grip or trigger |
| 7 | Press a launch or puzzle-board button | Slap with an open hand |
| 8 | Press a floating menu button | Trigger (UI press) |
| 9 | Start an Event session | Both triggers |
| 10 | Staff reset (Event) | Hold grip + trigger + Y and grip + trigger + right stick press |
| 11 | Pause | Menu button |

## T6 Heuristic findings

At least three evaluators per mode, each finding rated on Nielsen's 0-4 severity scale, then merged and rated again. Every merged severity 3 or 4 gets a Taiga bug. Columns: ID, Mode, Heuristic, Where, Finding, Severity, Evaluator, Merged severity, Taiga bug, Status (Open, Fixed, Retested, Won't fix).

Heuristics: 1 Visibility of system status; 2 Match between system and the real world; 3 User control and freedom; 4 Consistency and standards; 5 Error prevention; 6 Recognition rather than recall; 7 Flexibility and efficiency of use; 8 Aesthetic and minimalist design; 9 Help users recognize, diagnose, and recover from errors; 10 Help and documentation.
