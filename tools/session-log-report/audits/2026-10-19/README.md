# TG-313: real Drive log audit

Audit date: October 19, 2026 (America/Phoenix). Reporter source: commit
`24909c3`, with reader from `072ff00`. No production code or source logs were
changed for this audit. TG-312 remains pending the original fixture set.

## Source and reproduction

User-provided archive: `drive-download-20261020T011006Z-1-001.zip`.
The UTC date in the filename is October 20; the local audit date is October 19.
It contains 201 JSONL files and `Psyche VR Session Index.xlsx`.

SHA-256:

```text
archive: e83ea64734d04c4dd35aed7f554d5a950c8179f0b5c8b59d64d3391c5e00f0a8
index:   3120bbcb025503ac82c0d0c38fdac40250a7690527adb40d320569f35878249d
```

Extract the archive into a separate directory. From that directory run:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 /path/to/repo/tools/session-log-report/report.py . --output sessions.csv
```

No build/date filters were applied. [report.txt](report.txt) contains stdout
(only the machine-specific output destination was normalized to `sessions.csv`).
[sessions.csv](sessions.csv) contains all 201 rows with source filenames relative
to the extracted folder. The process exited successfully with empty stderr: no
skipped files, duplicate IDs, truncated tails, or invalid metric diagnostics.
All extracted source files were compared byte-for-byte against the ZIP after
the run. The ZIP and spreadsheet were not modified.

## Results and independent checks

| Metric | Full-archive result | Target |
| --- | --- | --- |
| Event Completion Rate | 7.54% (15/199) | >= 70% |
| Field Abnormal Exit Rate | n/a: 43 unended sessions lack deviceName | 0% |
| Scene load time | 10.95 s (raw maximum 10.945 s) | <= 10 s |
| Field frame rate | 18.67 FPS; 73.30% overruns > 17.4 ms | overruns < 1% |

Independent parsing and decimal arithmetic, without importing the report modules,
confirmed 4,237 records, 199 Event sessions and two Story sessions. Fifteen Event
sessions complete before their first expiry (or complete with no expiry).
There are 186 performance samples and 45 scene-load samples, each metric present
in 45 sessions. Performance totals are:

```text
sum(seconds)              = 25,296.5
sum(seconds * fps)        = 472,177.54
sum(seconds * overrunPct) = 1,854,126.555
weighted fps             = 18.665726088589...
weighted overrunPct      = 73.295774316605...
```

Exit states: 130 ended, 13 suspended, 14 abnormal, 43 unknown-device and one open.
The unknown-device group prevents a full-archive exit rate. The one open session
is identified below; it is excluded only from the exit-rate denominator.
The state labels follow the specified log rule, not a diagnosis of a confirmed crash.

The `Sessions` worksheet has 401 upload rows, covering exactly the same 201 IDs.
Selecting the latest `uploadTimeUtc` for each ID yields zero discrepancies in
`startTimeUtc`, `durationSeconds`, `eventCount`, `flavor`, and `endReason` against
the raw logs. Spreadsheet rows are uploads, not additional visitor sessions.
For index comparison, `application_quit` maps to `quit`; every missing end maps
to the receiver's `suspended`. That index label does not distinguish the report's
open, abnormal, suspended, and unknown-device states. Spreadsheet metadata was
not injected into legacy JSONL files or used to change the report results.

## Three raw-log checks

Line numbers below are one-based file lines; `sequence` is zero-based.
Each check reviewed raw lifecycle and quality-event records and calculated the
expected CSV values independently. The worksheet references include the header
in row 1.

### 1. Completion followed by an abnormal exit

File: `session_20261017T001350223Z_2103d74a8cb24e30a8cc549e4ad3d89a.jsonl`.

SHA-256: `d874c2d8a90e5b88f9bed73c612c0a1c152c211c899b76be45d95ae97256ff64`.

- Line 1 starts an Event session on `Quest 3`, at `2026-10-17T00:13:50.2241640Z`.
- Line 59 completes the puzzle at elapsed 50.260 s. There is no clock-expired
  record anywhere in the file, so completion is `true`.
- Line 95 is `application_focus_gained`; there is no `session_end`. Newer logs
  exist for this device, so this session is `abnormal`, not open or suspended.
- Line 2 reports `loadSeconds=4.909`, displayed as 4.91 s.
- There are 95 records and observed duration `969.621 - 0 = 969.621` s.
  Index `Sessions!A247:I247` agrees on start, duration, count, mode and receiver
  end reason (`suspended`).

| Raw line | seconds | fps | overrunPct |
| --- | ---: | ---: | ---: |
| 61 | 60 | 66.1 | 9.83 |
| 86 | 44.6 | 71.3 | 1.42 |
| 94 | 865.1 | 0 | 100 |

The weights total 969.7 s. FPS is `7145.98 / 969.7 = 7.37` rounded;
overruns are `87163.132 / 969.7 = 89.89%` rounded. Both match the CSV.
The sample durations are rounded values logged by Unity and need not sum exactly
to the session's observed elapsed duration.

### 2. Worst load, with a normal suspend/focus-loss ending

File: `session_20261016T233432143Z_543fc1c8c3584e9683d69201de581041.jsonl`.

SHA-256: `3a83ce9c9a1b8c4cb80573b5eb2798f7a286895c4ab72930ec549a3c1cf5290d`.

- Line 1 starts an Event session on `Quest 3` at `2026-10-16T23:34:32.1447090Z`.
- Line 2 contains `loadSeconds=10.945`, the maximum across all 45 load samples.
  The report prints 10.95 s, exceeding the 10 s target.
- Line 5 is the only performance sample: 15 s, 65.7 FPS, 3.66% overruns.
  The weighted outputs are therefore 65.70 FPS and 3.66%.
- No puzzle completion appears. Line 14 ends at `application_focus_lost` without
  `session_end`. Newer device sessions exist, so it is `suspended`, counted in
  the exit denominator but not as an abnormal exit.
- Duration is `22.333 - 0.001 = 22.332` s over 14 records. Index
  `Sessions!A229:I229` agrees on start, duration, count, mode and end reason.

### 3. Newest open session; completion precedes a later expiry

File: `session_20261019T043728957Z_9a17f1215b004100ae2daee14c6dc6a3.jsonl`.

SHA-256: `6f07e44c569079dcc2bbc76da8769c50567175947edeca653e3bc75b94972df2`.

- Line 1 starts at `2026-10-19T04:37:28.9579600Z`, the latest start among all
  `Quest 3` logs in this archive.
- Line 24 completes the puzzle at elapsed 25.466 s. Line 41 expires the clock
  at elapsed 179.627 s. Completion remains `true` because it precedes expiry.
- Line 61 is `application_paused`, with no end record. As the newest session for
  its device, it is `open`, excluded from the exit denominator. This classification
  describes the archive snapshot; it does not establish the device's current state.
- Line 2 gives load 4.774 s, printed as 4.77 s.
- Duration is `2949.002 - 0.004 = 2948.998` s over 61 records. Index
  `Sessions!A402:I402` agrees on start, duration, count, mode and receiver end reason.

| Raw line | seconds | fps | overrunPct |
| --- | ---: | ---: | ---: |
| 29 | 60 | 71.1 | 0.54 |
| 30 | 60 | 72 | 0.25 |
| 32 | 0.7 | 67.9 | 2.17 |
| 42 | 60 | 71.3 | 0.56 |
| 47 | 60 | 72 | 0.09 |
| 49 | 6.4 | 71.7 | 0.44 |
| 60 | 16.7 | 69.5 | 2.07 |

The weights total 263.8 s. FPS is `18851.06 / 263.8 = 71.46` rounded;
overruns are `125.304 / 263.8 = 0.47%` rounded. Both match the CSV.

## Doubts and limitations to carry into the PR

- This archive spans starts from September 24 through October 19 UTC. It includes
  154 Android sessions and 47 LinuxEditor sessions, multiple builds, and 152 logs
  with no embedded build/device metadata. These are full-archive measurements,
  not a claim about one release or a clean visitor study.
- Legacy sessions remain in the Event denominator under the stated definition,
  even when their builds did not emit the newer quality events. Only 45 sessions
  supply load and performance measurements. Do not interpret missing events as
  measured zero load or zero FPS.
- The 865.1-second, zero-FPS sample in check 1 follows pause/resume records.
  Its `worstMs=865113.3` suggests that suspension may be included in this older
  performance window. The cause has not been verified on a headset. The required
  seconds-weighted calculation includes this sample; none were removed to improve
  the aggregate. Consider reviewing logger pause handling separately.
- Device identity is the logged name, not a hardware identifier. Open-session
  detection assumes each name identifies one device. The spreadsheet's upload
  labels do not establish that assumption for legacy sessions.
- No fixture answers were invented or modified. TG-312's fixture regression
  tests are still pending and this real-data audit does not replace them.

## PR description excerpt

TG-313 runs the report against the supplied 201-log Drive snapshot and records all
201 CSV rows plus three raw-log checks. The latest of 401 index uploads reconciles
to every session with no summary discrepancies. Output is 7.54% completion,
10.95 s worst load, 18.67 weighted FPS and 73.30% overruns; the abnormal-exit rate
is unavailable because 43 unended logs lack device identity. See the limitations
above for mixed builds/editor runs and the long zero-FPS sample. TG-312 remains
pending the supplied fixtures.
