# Initial session data logger — TG-263

The logger starts automatically when Play mode or the application starts. No scene
object or prefab setup is needed.

## Initial scope

These are implementation defaults for the team to review as analytics requirements
are defined:

- One session begins after the first scene loads.
- An admin restart or mode switch ends the current session and creates a new one
  after the master scene reloads.
- Other scene loads stay in the same session.
- Application quit ends the session. Losing focus or suspending the application
  records an event and keeps the session open.
- Elapsed time uses a monotonic clock and includes in-game pauses.
  Changing `Time.timeScale` does not change it.
- Logs stay on the device; there is no server upload or automatic deletion.

This initial implementation records lifecycle events and offers a helper for
gameplay events. Puzzle results, scores, completion criteria, and other team-defined
metrics can be added at their source using that helper.

## Output

Files are saved under:

```text
Application.persistentDataPath/SessionLogs/session_<UTC timestamp>_<session ID>.jsonl
```

Unity prints the exact file path in the Console when each session begins.
`SessionDataLogger.CurrentLogPath` and `CurrentSessionId` expose the active file
and identifier at runtime. They are null when no session is open.

Each UTF-8 line is one JSON object with these fields:

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Format version, initially `1` |
| `sessionId` | Random ID generated for this session |
| `sequence` | Event order within the session, starting at `0` |
| `timestampUtc` | UTC event timestamp in ISO 8601 format |
| `elapsedSeconds` | Real seconds since the session began |
| `eventName` | Lifecycle event or caller-supplied event name |
| `mode` | `Event` or `Story`, fixed for the session |
| `scene` | Scene associated with the event |
| `details` | Optional text; for `session_end`, the end reason |
| `applicationVersion` | Unity application version |
| `platform` | Unity runtime platform |

Automatic events: `session_start`, `session_end`, `scene_loaded`,
`application_paused`, `application_resumed`, `application_focus_lost`, and
`application_focus_gained`. Application pause/focus events represent OS lifecycle
notifications; they are separate from opening the in-game pause menu.

End reasons: `application_quit`, `restart`, `mode_change`, or `logger_destroyed`.

## Record a gameplay event

Call from Unity's main thread:

```csharp
using PsycheVR.Data;

SessionDataLogger.LogEvent("puzzle_piece_placed", "left_solar_panel");
SessionDataLogger.LogEvent("arrival_completed");
```

The helper returns `false` if logging is unavailable or the name is blank. Details
are a string, and JSON escaping preserves quotes, newlines, and Unicode text.
These examples are integration points, not events wired into gameplay yet.

## Reliability

Each complete event is flushed immediately, and files have unique names so later
sessions preserve earlier logs. A normal exit writes `session_end`; an OS force-stop,
crash, or power loss can leave a session without that event or an incomplete final
line. Readers should preserve preceding complete records and treat a missing end
event as an interrupted session.

File access or storage failures produce a warning and disable logging for the
remainder of the application run, while gameplay continues.

## Manual verification

1. Enter Play mode in any scene. Find the `Saving session to ...` Console message.
2. Open the file while playing. Check the initial `session_start`, mode, scene, and
   session ID. Call `LogEvent` from a gameplay action and check the new line.
3. Pause the game, wait, and record another event. Elapsed time should still advance.
4. Use the admin menu to restart the current mode, then switch modes. Each operation
   should close the old file with the matching reason and create a distinct file.
5. Stop Play mode. The latest file should end with exactly one `session_end`.
6. On Quest, verify file creation and suspend/resume behavior on the target device.

## Validation performed

Verified with Unity 6000.3.8f1 compilation and Play Mode checks: automatic startup,
initial mode/scene metadata, custom events, JSON escaping, sequence numbers, unique
files, immediate record visibility, elapsed time during game pause, duplicate
logger protection, lifecycle pause/resume, ordinary scene reloads, admin restarts,
mode changes, and a single end event on quit. A simulated storage failure also
verified that logging stops with one warning and later calls safely return false.
Quest file access and device suspend/resume still require a headset check.
