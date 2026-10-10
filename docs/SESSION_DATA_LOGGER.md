# Session data logger

The Unity logger (TG-263) starts automatically after the first scene loads; no
scene or prefab setup is needed. The Apps Script receiver (TG-280/281) stores
uploaded logs in Drive and summaries in Sheets. Automatic headset upload is
separate work (TG-283); the current logger writes locally without deleting logs.

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

## Add gameplay events

Call from Unity's main thread at the gameplay action you want to record:

```csharp
using PsycheVR.Data;

SessionDataLogger.LogEvent("puzzle_piece_placed", "left_solar_panel");
```

The optional details string is JSON-escaped. The call returns `false` when logging
is unavailable or the event name is blank. This example requires gameplay wiring.

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
