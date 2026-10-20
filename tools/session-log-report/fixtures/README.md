# TG-312 fixture set

These synthetic logs and expected answers were created for TG-312 with the user's
approval because the original set described in TG-309 was not supplied. They are
not captured visitor data or the original promised fixtures. The real Drive audit
remains separate under `../audits/2026-10-19/`.

`expected.json` contains fixed answers specified from the cases below, not generated
by importing or running the report. Tests must not rewrite these answers to make
a failing implementation pass. Changes to a scenario require reviewing its arithmetic
and explaining the reason for changing its expected answer.

## Main folder

| ID | Date (UTC midnight) | Mode | Last record | Expected result |
| --- | --- | --- | --- | --- |
| complete | October 1 | Event | session_end | Completion before expiry; ended |
| expired | October 2 | Event | completion | Expiry before completion; abnormal |
| suspended | October 3 | Story | application_paused | Suspended, not abnormal |
| open | October 4 | Event | object_grabbed | Newest on Fixture A; open |
| other | October 1 | Story | session_end | Ended on Fixture B |

All sessions use `fixture-v1` except `open`, which uses `fixture-v2`.
The complete session has a one-record duplicate; its seven-record nested copy wins.
The expired session has five valid records and a malformed unterminated sixth line.
The malformed file has a bad newline-terminated record and must be named and skipped.
`unrelated.jsonl` must not be discovered because its name does not match `session_*`.

- Completion: only `complete` succeeds among three Event sessions: 1/3 = 33.33%.
- Exit rate: exclude `open`; one abnormal among four remaining sessions = 25%.
- Loads are 4 and 12 seconds; maximum = 12 seconds.
- `complete` samples: (10 s, 60 FPS, 0.5%) and (20 s, 30 FPS, 2%).
  FPS = (600 + 600)/30 = 40; overruns = (5 + 40)/30 = 1.5%.
- `expired` sample: (30 s, 80 FPS, 0.5%). Combined FPS =
  (600 + 600 + 2400)/60 = 60; overruns = (5 + 40 + 15)/60 = 1%.
- Filtering to `fixture-v1` removes `open` from output but retains its evidence
  that `suspended` is not the newest device session. Completion becomes 1/2;
  exit rate remains 1/4 and measurement totals remain unchanged.
- Starting October 2 includes expired, suspended and open: completion 0/2,
  exit rate 1/2, load 12, FPS 80, overruns 0.5%.

Records have elapsed times spaced ten seconds apart to make durations easy to
check. Performance-window seconds are separate recorded measurements used as weights.

## Other folders and boundary cases

`legacy` has one unended Event session with a blank device name. Completion is
0/1; exit rate cannot be determined; load and performance are unavailable.

`invalid_metrics` has one ended Event session. Its negative load and zero-duration
performance window are omitted. A 10-second window has invalid FPS but valid 2%
overruns; a 20-second window has valid 60 FPS but invalid 101% overruns. The independent
denominators are therefore 10 seconds for overruns and 20 for FPS. Four diagnostic
lines name the invalid measurements. Completion and abnormal exits are both 0/1.

Additional reader-boundary tests create temporary copies to exercise encoding,
validation, duplicate ties, and a simulated permission failure. That failure is
mocked so the test works even under accounts that can read mode-000 files.

Run from the repository root:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/session-log-report
```
