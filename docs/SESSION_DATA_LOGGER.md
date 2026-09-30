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

## Upload receiver — TG-281

The standalone Apps Script source is in
[`tools/session-log-upload/Code.gs`](../tools/session-log-upload/Code.gs), with a
V8 manifest in `appsscript.json`. This implements the server side of TG-280;
the Unity logger still writes locally. TG-283 adds the client uploader.

### Configuration

Copy `Code.gs` and `appsscript.json` into a standalone Apps Script project.
Under Project Settings, enable the manifest file to edit `appsscript.json`.
Set these **Script Properties** in Project Settings (not in source control):

| Property | Value |
| --- | --- |
| `UPLOAD_TOKEN` | Shared secret used by the uploader |
| `DRIVE_FOLDER_ID` | Destination folder ID |
| `SPREADSHEET_ID` | Destination spreadsheet ID |
| `SHEET_NAME` | Existing tab name, for example `Sessions` |

Use a dedicated empty tab. The first upload creates the header row; later uploads
check those headers before writing. A missing tab or unexpected headers returns
`ok: false`. The executing account needs write access to both destinations.
Missing configuration disables uploads. No real token or destination IDs are
included in this repository.

**Deployment status:** not deployed or verified against Google services yet.
TG-282 covers deployment under the ASU account, team sharing, verification with
the ten September 26 sample files, and recording the deployed URL/redeployment
instructions here. The headset needs an endpoint it can access without an
interactive Google sign-in; verify the account's deployment options during TG-282.

### Request and response contract

POST a JSON object with these fields:

| Field | Required value |
| --- | --- |
| `token` | Exact match for `UPLOAD_TOKEN` |
| `deviceName` | Nonblank string, at most 256 characters |
| `buildStamp` | Nonblank string, at most 512 characters; TG-283 supplies the build identifier |
| `type` | Lowercase `story` or `event`, matching the log's `mode` |
| `fileName` | Plain filename ending in `.jsonl`, at most 255 characters; no path separators/control characters |
| `content` | Original JSONL text, at most 1,000,000 characters |

The existing logs have no build stamp; test requests for them can use an explicit
label such as `legacy-0.1.0`. The receiver uses the request's `deviceName` and
`buildStamp` as upload metadata and does not require the future TG-283 log fields.

Success is `{"ok":true}`. Failure is `{"ok":false,"error":"..."}`. Clients must
check the JSON `ok` value, not just the HTTP status. The response uses Apps Script
ContentService's JSON MIME type. Follow its response redirect when testing with
an HTTP client. Never log or commit a request body containing the real token.

### Stored data and parsing rules

The original content is saved unchanged under `fileName`. An existing file with
that name in the configured folder is overwritten. A script lock serializes
filename lookup, file creation/replacement, and Sheet writes. Pre-existing
duplicate filenames are reported as a storage failure instead of choosing one
arbitrarily. Wrong or missing tokens never access Drive or Sheets.

Every successful POST appends one data row, even when overwriting a previously
uploaded file. Columns are:

| Column | Source |
| --- | --- |
| `uploadTimeUtc` | Server UTC time when processing the upload |
| `deviceName` | Request metadata |
| `sessionId` | First complete record |
| `buildStamp` | Request metadata |
| `flavor` | Request `type`, checked against every record's mode |
| `startTimeUtc` | `session_start.timestampUtc` |
| `durationSeconds` | Last complete elapsed time minus start elapsed time, rounded to milliseconds |
| `endReason` | `application_quit` becomes `quit`; `restart`, `mode_change`, and `logger_destroyed` are preserved; missing `session_end` becomes `suspended` |
| `eventCount` | Number of complete records, including start/end and lifecycle events |

Parsing requires schema version 1, one session ID, sequence numbers starting at
zero without gaps, nondecreasing elapsed seconds, and `session_start` first.
An end event must be last. Blank lines, UTF-8 BOM, CRLF, and a complete final
record without a newline are supported. An unparseable **unterminated last line**
is treated as crash truncation and omitted from the summary; the saved file still
contains it. Malformed complete lines or invalid record fields reject the upload
before writing. Duration for an interrupted session only covers observed events.
Formula-like metadata is escaped when written to Sheets and remains unchanged
in the saved JSONL file.

Drive and Sheets writes are not a cross-service transaction. If saving the file
succeeds and appending/flushing the row fails, the response is `ok: false` and the
file may already exist. Retry overwrites it. A lost response or uncertain Sheet
write can produce another summary row on retry; this matches TG-280's rule of
one row per valid POST rather than deduplicating rows by session. Service failures
return a generic error without exposing configuration or log contents.

### Local validation

With Node.js 18 or newer, run from the repository root:

```sh
node --test tools/session-log-upload/Code.test.cjs
```

The suite executes the actual `Code.gs` with in-memory Google service doubles.
It covers the nine output columns, exact file preservation, repeated uploads,
token/configuration rejection, interrupted records, both flavors, invalid data,
formula escaping, lock contention, service failures, and unexpected destination
state. Fixtures follow the current `SessionLogWriter` schema; the September 26
sample ZIP has not yet been supplied. These checks do not replace TG-282's live
deployment and sample-file tests.

Google references: [web app request handling](https://developers.google.com/apps-script/guides/web),
[JSON responses and redirects](https://developers.google.com/apps-script/guides/content),
[Drive file replacement](https://developers.google.com/apps-script/reference/drive/file#setContent(String)),
[Sheet row appending](https://developers.google.com/apps-script/reference/spreadsheet/sheet#appendRow(Object)),
and [script locks](https://developers.google.com/apps-script/reference/lock/lock-service).
