# Session data logger

The Unity logger (TG-263) starts automatically after the first scene loads; no
scene or prefab setup is needed. The Apps Script receiver (TG-280/281) stores
uploaded logs in Drive and summaries in Sheets. The headset uploader (TG-283/TG-314) sends pending logs and archives them locally without deleting log data.

## Logging behavior

- Admin restart or mode change ends the session and starts another after reload.
  Ordinary scene loads, focus changes, and suspend/resume keep the session open.
- Elapsed time includes in-game pauses and is independent of `Time.timeScale`.
- Events flush immediately. A crash may leave an unfinished final line or omit
  `session_end`. Storage errors disable logging for the run; gameplay continues.

Files are UTF-8 JSONL (one JSON object per line):

```text
Application.persistentDataPath/SessionLogs/session_<UTC timestamp>_<session ID>.jsonl
```

The Console prints the path. `SessionDataLogger.CurrentLogPath` and
`CurrentSessionId` expose the active session, or return `null` when unavailable.

Each record contains `schemaVersion` (1), `sessionId`, `sequence` (starting at 0),
`timestampUtc`, `elapsedSeconds`, `eventName`, `mode` (`Story` or `Event`), `scene`,
`details`, `applicationVersion`, and `platform`.

Automatic events: `session_start`, `session_end`, `scene_loaded`,
`application_paused`, `application_resumed`, `application_focus_lost`, and
`application_focus_gained`. Pause/focus events refer to the OS, not the pause menu.
End reasons in `details`: `application_quit`, `restart`, `mode_change`, or
`logger_destroyed`.

Quality-plan events (TG-308), also automatic:

| Event | Details | Used for |
| --- | --- | --- |
| `scene_ready` | `loadSeconds`: app launch, restart or mode change to the first playable frame | Scene load time (≤ 10 s) |
| `interactables_present` | `count`, `objects` (every interactable, `Room/Object`, joined by `\|`) | Objects nobody found |
| `idle_started` | `after` (last action), `head` (x,y,z m), `yaw` (deg); logged after 20 s with no interaction | Where visitors stall |
| `idle_ended` | `seconds` idle, `next` (the action that ended it) | Stall length |
| `perf_sample` | every 60 s: `seconds`, `fps`, `overrunPct` (frames over 1.25 x the 72 Hz frame), `worstMs` | Frame rate in the field |
| `low_memory` | none | Crash diagnosis |

Gameplay events (TG-308), each with `object` (`Room/Object`, copy suffixes removed):

| Event | Extra details |
| --- | --- |
| `object_grabbed`, `object_released` | release adds `heldSeconds` |
| `drawer_grabbed`, `drawer_opened`, `drawer_closed` | |
| `book_opened`, `book_closed`, `page_turned` | page adds `page`, `forward` |
| `pen_clicked`, `paper_crumpled`, `ball_in_basket` | basket adds `basket` |
| `puzzle_piece_placed`, `puzzle_reset` | piece adds `zone` |
| `headset_to_ear` | |
| `key_pressed`, `console_tab_changed`, `console_power`, `ping_sent`, `ping_returned` | tab adds `direction`, power adds `on` |
| `instructions_paged`, `button_case_opened`, `video_button_pressed`, `teleported` | page adds `direction` |

Event mode also logs `kiosk_first_input`, `kiosk_puzzle_completed` and
`kiosk_clock_expired` (TG-283), each with `elapsedSeconds`. A kiosk reset ends the file,
so one Event file is one visitor.

Uploads (when the build has upload settings) happen at app start, every 3 minutes,
when the headset is taken off or the Quest menu opens, when it is put back on, when an
Event visitor finishes or runs out of time, and after every restart or mode change. A
quit from the Quest menu pauses the app first, so the pause upload covers it; anything
that still misses goes up at the next launch. Only new or grown files are sent.

The Sheet gets a row for every upload, and a session is uploaded again whenever its
file has grown, so use the latest row per `sessionId` (the Drive file is always the
latest copy).

## Local storage and offline collection (TG-314)

`SessionLogs/` contains pending `.jsonl` files, including the current session.
After a finished log receives `{"ok":true}`, it and its `.sent` receipt go into
`SessionLogs/Sent/`. Logs over 1,000,000 characters go into `SessionLogs/Rejected/`
when inactive, so they are preserved for manual collection instead of retried.
An active log never moves, even when oversized or acknowledged. The uploader
only enumerates the top-level pending folder; it never opens archived logs.
Each processed file is followed by a frame break, including failed/skipped files.
Upload triggers and the three-minute interval are unchanged.

A crash after server confirmation but before moving a log leaves it pending;
the next run uploads it again. Duplicate summary rows are possible. New receipts
are written inside `Sent/`, so an interrupted move cannot accidentally suppress
that retry. Existing 0.9.0 `.sent` receipts next to pending logs are migrated on
the first startup pass, even without upload configuration, only when their hash
matches the whole current file. A stale receipt never hides newly appended data.

Each session records `storage_status` immediately after `session_start`, using
the existing `details` field:

```text
freeBytes=123456789;waiting=3;sent=42;rejected=1
```

Counts include only session `.jsonl` files directly in each folder, not receipts.
Waiting includes the new active session. This is a snapshot before startup
migration/upload finishes. Free bytes refer to the filesystem holding the logs;
`-1` means a measurement was unavailable, and missing folders count as zero.

### Copy logs from a headset without Wi-Fi

Use a USB data cable, enable developer mode/USB debugging on the Quest, and
accept its USB debugging authorization prompt. Install Android SDK Platform-Tools
on the computer, then run:

```sh
adb devices
adb shell am force-stop com.missiontopsyche.psyche20d
adb pull /sdcard/Android/data/com.missiontopsyche.psyche20d/files/SessionLogs ./headset-session-logs
```

Stop the experience between visitors before collecting, so files cannot change
during the copy. Force-stop may leave the last log without `session_end`; its
already flushed events remain available. The pull copies pending logs, `Sent/`,
and `Rejected/` together and does not remove anything from the headset. Confirm
the copied files are present before disconnecting. If `adb devices` reports
`unauthorized`, unlock the headset and accept the prompt. For multiple headsets,
add `-s <serial>` after `adb` to select one. If the build's Android package ID
changes, replace `com.missiontopsyche.psyche20d` in both commands. No network or
upload credentials are needed for this USB export.

### EditMode regression tests

Open **Window → General → Test Runner → EditMode**, then run
`PsycheVR.Tests.SessionUploadTests`. Each test creates and removes a unique
temporary folder; no headset data or upload server is touched. Tests cover
archived-file exclusion, strict server acknowledgment, active-log protection,
interrupted-move recovery, legacy/stale receipts, the size boundary and rejection,
and storage counts. The tests resolve the runtime types from `Assembly-CSharp`
because Unity test assembly definitions cannot directly reference that assembly.
The existing **Tools → Session Logs → Check Upload Storage** checks remain available.

## Add gameplay events

Call from Unity's main thread at the gameplay action you want to record:

```csharp
using PsycheVR.Data;

SessionEvents.Interaction("puzzle_piece_placed", this, "zone=" + SessionEvents.ObjectId(snapAnchor));
```

`SessionEvents.Interaction` names the object, adds optional `key=value` pairs and
resets the idle clock; implement `ISessionInteractable` on the component so the object
is counted in `interactables_present`. `SessionDataLogger.LogEvent(name, details)` is
the raw call (JSON-escaped details; returns `false` when logging is unavailable or the
name is blank) for events that are not visitor actions.

## Upload receiver

Source: [`Code.gs`](../tools/session-log-upload/Code.gs) and
[`appsscript.json`](../tools/session-log-upload/appsscript.json).

POST JSON with these fields:

| Field | Requirement |
| --- | --- |
| `token` | Exact match for the `UPLOAD_TOKEN` Script Property |
| `deviceName` | Nonblank string, up to 256 characters |
| `buildStamp` | Nonblank string, up to 512 characters; use an explicit legacy label for old logs |
| `type` | `story` or `event`, matching the records' mode |
| `fileName` | Plain `.jsonl` filename, up to 255 characters; no path separators or control characters |
| `content` | Original JSONL text, up to 1,000,000 characters |

Follow response redirects and check the JSON result: `{"ok":true}` or
`{"ok":false,"error":"..."}`. Wrong/missing tokens store nothing. Never commit
or log the real token.

Uploads preserve the original content and overwrite an existing filename.
Each successful POST appends a Sheet row, including repeat uploads. Columns:
`uploadTimeUtc`, `deviceName`, `sessionId`, `buildStamp`, `flavor`, `startTimeUtc`,
`durationSeconds`, `endReason`, `eventCount`.

Duration is the last complete elapsed time minus the starting elapsed time,
rounded to milliseconds. Counts include lifecycle records. `application_quit`
becomes `quit`; a missing end event becomes `suspended`; other end reasons stay
unchanged. Interrupted-session duration covers only observed events.

The receiver requires schema 1, one session/mode, consecutive sequences, and
nondecreasing elapsed time. `session_start` must be first and `session_end` last.
An unparseable unterminated final line is omitted from the summary but retained
in Drive; malformed complete records reject the upload. Writes are locked, and
formula-like Sheet values are escaped. Drive and Sheets are not transactional:
a failed response may follow a saved file or row, so retries can add summary rows.

## Deployment reference

- [Apps Script project](https://script.google.com/d/1fJ_vAYaAAjqaAQ6EXEcVWBni80Ayl-oHSsk35tfPQ3mct0Hje4xJSJ-O/edit)
- [Upload endpoint](https://script.google.com/macros/s/AKfycbzNr5RFUGgPyLQkNL5OhjhrYH3NOmT46XedWqTwtIqGdrF_CYNsp7FHkDagHINuBEeHEA/exec)
- [Drive logs](https://drive.google.com/drive/folders/1_8R3-Up4ywlyvwTewwBlpy1_yeuF5egd) · [Session index](https://docs.google.com/spreadsheets/d/1Py2KuoibXNP-YirO_OWbCplOjV5ZFh-O5zhTvZr988c/edit#gid=0)

Configuration lives in **Project Settings → Script Properties**: `UPLOAD_TOKEN`,
`DRIVE_FOLDER_ID`, `SPREADSHEET_ID`, and `SHEET_NAME` (`Sessions`). The executing
account needs write access to both destinations. The tab must exist; an empty
tab gets headers automatically, and existing headers must match the columns above.

To redeploy, copy the source and manifest into the project, save, then choose
**Deploy → Manage deployments → Edit → New version → Deploy** to keep the URL.
The web app runs as the ASU owner and must allow callers without Google sign-in.
At semester end, archive the deployment and remove `UPLOAD_TOKEN`.

## Verification

Run from the repository root:

```sh
node --test tools/session-log-upload/Code.test.cjs
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/session-log-upload -p 'test_*.py'
python3 tools/session-log-upload/verify_upload.py /path/to/samples.zip --dry-run
```

For live checks, replace `--dry-run` with `--url "<upload endpoint above>"`.
The helper prompts privately for the token and tests rejection, ten samples, and
one repeat. Expect ten log files and 11 new Sheet rows; compare stored contents
and summaries. Retries are off by default because an uncertain response may
already have written a row.

Verified: Unity 6000.3.8f1 compilation and Play Mode lifecycle/storage checks;
17 receiver and 11 verifier tests; live curl rejection/upload/repeat checks.
On October 9, 2026, all ten downloaded logs matched the originals byte for byte,
and all 20 Sheet rows matched expected summaries and upload labels. Team access
was confirmed; both destinations currently allow anyone with the link to view.
Quest file creation and device suspend/resume still need a headset check.
