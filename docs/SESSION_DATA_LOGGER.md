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

## Deployment and verification how-to — TG-282

### Create the destinations and web app

1. Sign in to the intended ASU Google account. Create a Drive folder named
   `Psyche VR Session Logs` and a spreadsheet named `Psyche VR Session Index`.
   Keep the spreadsheet outside the log folder so the folder contains only logs.
   Rename the spreadsheet's initial tab to `Sessions` and leave it empty.
2. Record the folder ID from its URL and the spreadsheet ID between `/d/` and
   `/edit` in its URL. Create a standalone project at `script.google.com`, named
   `Psyche VR Session Upload`. Copy the repository's `Code.gs` into its editor.
   Enable the manifest in Project Settings and copy `appsscript.json` as well.
3. In Project Settings > Script Properties, set the four properties documented
   above. Generate a random upload token locally, for example with
   `openssl rand -hex 32`, and store it in `UPLOAD_TOKEN`. Keep it in the team's
   approved secret storage for the future client configuration. Do not put it in
   the Sheet, this document, commit messages, or command arguments.
4. Choose Deploy > New deployment > Web app. Set **Execute as: Me** (the ASU
   owner) and access to **Anyone**, including callers without Google sign-in.
   This exposes the POST handler; its token gates writes. The Drive folder and
   Sheet themselves should remain restricted to the team. Review and authorize
   the script's Drive and Sheets permissions, then deploy.
5. Copy the deployed URL ending in `/exec`. Do not use the editor-only `/dev`
   test URL. If the ASU account does not offer anonymous access, stop and resolve
   that with the project owner; a Google sign-in page cannot serve the headset's
   token-only POST requests.

These settings follow Google's [web app deployment documentation](https://developers.google.com/apps-script/guides/web).
Script Properties are edited as described in the
[Properties Service guide](https://developers.google.com/apps-script/guides/properties).

### Share with the team

Use Share on both the folder and spreadsheet. Add the confirmed team addresses
or team Google Group with the agreed viewer/editor role. Keep General access
restricted. Check the resulting access list on **both** items, and have a teammate
verify they can open them. Folder access covers the uploaded files; the index
spreadsheet needs its own sharing because it is outside that folder. Script
edit access is separate and gives access to Script Properties, including the token;
only give that role to intended maintainers.

### Verify the ten September 26 samples using curl

The helper uses Python 3.9+ and `curl`. It reads the ZIP without extracting it,
requires exactly ten distinct `.jsonl` filenames, and asks for the token through
a hidden prompt. The token is passed to curl through standard input; it is not
saved in a request file or exposed in process arguments.

First inspect the expected session summaries without network requests:

```sh
python3 tools/session-log-upload/verify_upload.py "/absolute/path/session-logs-2026-09-26.zip" --dry-run
```

Then run against the deployed URL (replace `DEPLOYMENT_ID`):

```sh
python3 tools/session-log-upload/verify_upload.py "/absolute/path/session-logs-2026-09-26.zip" \
  --url "https://script.google.com/macros/s/DEPLOYMENT_ID/exec"
```

The helper sends two negative requests (missing/wrong token), the ten samples,
and a repeat of the first sample. It stops at the first unexpected response.
It uses `curl --location --data-binary @-`, without `-X POST`, so the initial
request is POST and the response redirect can switch to GET. Apps Script's
[ContentService requires following redirects](https://developers.google.com/apps-script/guides/content).
The default upload labels are `TG-282 sample verification` and
`legacy-0.1.0-sep26`; override them with `--device-name` and `--build-stamp` if needed.

HTTP acknowledgments alone do not prove the stored data is correct. After a
successful run against initially empty destinations, verify:

- The Drive folder contains **10 files**, not 11, with the original filenames.
  Neither `tg282_missing_token.jsonl` nor `tg282_wrong_token.jsonl` exists.
- Download the stored files and compare them with the originals byte for byte.
  Repeating the first file must keep its Drive file ID and update its contents.
- The `Sessions` tab has the header plus **11 data rows**: ten initial uploads
  and the repeated upload. No row comes from either negative request.
- Compare all nine columns with the dry-run summaries and upload labels. Check
  that timestamps are UTC, elapsed duration and counts match, and missing end
  events yield `suspended`. The last row repeats the first sample's summary with
  a new upload time.
- Confirm the team's access to the folder, files, and Sheet.

Each repeat of the whole verification run adds another 11 rows. Record the
starting row count if testing existing destinations. The helper does not delete
test data or automatically retry ambiguous network failures.

The verifier's local tests can be run without Google access:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/session-log-upload -p 'test_*.py'
```

### Redeploy and retire

After editing the script, save it, then use Deploy > Manage deployments > Edit >
New version > Deploy to update the existing deployment and preserve its URL.
Rerun verification and record the version/date. Saving source alone does not
update the versioned `/exec` deployment. Changing Script Properties changes
configuration shared by the script, so coordinate token changes with clients.

At semester end, archive the deployment through Manage deployments and remove
the upload token from Script Properties. Agree with the team on retention and
ownership of collected files before removing any stored data or access.

### Live deployment record

Fill these in only after live deployment and verification:

| Item | Status |
| --- | --- |
| ASU deployment owner | `etomasi@asu.edu` (user-reported; account permissions not independently verified) |
| Apps Script project URL | Pending |
| Deployed `/exec` URL and version | [Replacement upload endpoint](https://script.google.com/macros/s/AKfycbzNr5RFUGgPyLQkNL5OhjhrYH3NOmT46XedWqTwtIqGdrF_CYNsp7FHkDagHINuBEeHEA/exec); version not yet confirmed |
| Drive folder URL | Pending |
| Index spreadsheet URL | Pending |
| Token location | Script Properties > `UPLOAD_TOKEN`; value never recorded here |
| Team recipients and roles | Pending confirmation |
| Ten September 26 samples | ZIP received; all 10 files pass local endpoint parsing (58 records; 2 Story and 8 Event sessions) |
| curl rejection/upload/repeat checks | Passed on the replacement endpoint, evidenced by user-provided Terminal screenshot dated October 6, 2026, 10:12 PM Arizona time: missing/wrong token rejected, all ten samples acknowledged, repeat acknowledged. Retry option enabled but zero retries needed; this run should add exactly 11 data rows. |
| Drive contents and Sheet summary checks | User confirms 10 JSON log files in Drive after repeated uploads. October 6, 2026, 10:55 PM screenshot shows 20 data rows; rows 11–21 match the successful run's ten samples plus repeat. Visible flavors, durations, end reasons, and event counts match the originals. Downloaded-file byte comparison and full untruncated field comparison remain pending. |
| Team access check | Not performed |

TG-282 remains incomplete until the live checks and sharing are verified.

### Redirect troubleshooting observed during verification

The editor setup check now confirms access to the configured folder, spreadsheet,
and `Sessions` tab. On both deployments, some POST responses redirected from
`script.googleusercontent.com` back to the script as a GET, producing an HTML
`Script function not found: doGet` error. The replacement deployment acknowledged
sample 3 in a single-sample test, but the full run encountered the redirect again.
An HTTP response failure does not establish whether the underlying writes happened.

The local verifier now includes safe redirect diagnostics and a `--diagnose
--sample-number 3` option for a single upload. It also adds a random non-secret
`verificationRequest` query parameter and `Cache-Control: no-cache` to avoid
cached redirect reuse. Three tokenless probes and individual authenticated uploads
succeeded, but a subsequent full run still hit the redirect failure. Unique URLs
did not resolve the problem.

Retries remain disabled by default. `--retry-response-errors 3` explicitly enables
up to three additional attempts per request, with 2/4/8-second delays, only for
the observed ContentService-to-script redirect followed by the missing-doGet error.
Other failures stop immediately. Each upload still requires an actual `ok: true`
response. This tolerates the response delivery problem; it does not fix Google’s
redirect behavior. The full live run on October 6, 2026 at 10:12 PM Arizona time
passed with the option enabled but zero retries needed. Retry logic is locally
tested; live recovery through an actual retry has not yet been demonstrated.

Retries may append additional rows because the previous POST may have succeeded.
The verifier reports the upload retry count and expected row-count range (11
through 11 plus upload retries). Count baseline rows before running and compare
all new summaries afterward. Drive must still contain exactly ten distinct sample
filenames. Keep retry rows as verification evidence; do not assume each row is a
distinct session when analyzing the index.
