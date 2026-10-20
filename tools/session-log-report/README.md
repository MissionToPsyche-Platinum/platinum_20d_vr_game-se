# Session log report

Run with Python 3 (standard library only), against an extracted Drive log folder:

```sh
python3 tools/session-log-report/report.py /path/to/logs --since 2026-10-01 --build 'exact build stamp'
```

The folder defaults to the current directory. `--since` includes sessions starting
at or after an ISO date/time (a date or timezone-free time uses UTC). `--build`
matches the complete `buildStamp`, not `applicationVersion`; logs without a stamp
cannot match. Filters apply after duplicate resolution and open-session detection.
`--output /path/to/sessions.csv` changes the CSV destination from `./sessions.csv`.
The CSV contains one row per selected session, including sessions excluded from the
exit-rate denominator. Existing CSV output is replaced; input logs are read-only.

The reader searches recursively for `session_*.jsonl`. It validates schema 1,
session/mode consistency, consecutive sequences, lifecycle boundaries and elapsed
times. An unparseable, unterminated final record is ignored with a warning; a bad
complete record rejects the file. Files that cannot be read and directory access
failures are named on stderr. Duplicate IDs keep the copy with the most complete
records; ties keep the first path in lexical order. Duplicate decisions are printed.

* Event completion uses all selected Event-mode sessions as visitors. Completion
  must occur before the first clock expiry in record order; no expiry is required.
* Exit rate includes both modes. The latest session by start timestamp on each
  device is excluded if unended. Tied latest unended sessions are all excluded.
  Among remaining sessions, no end and a last event other than pause/focus loss is
  abnormal. An unended log without a device name makes this metric unavailable,
  because its open status cannot be established. Device names must be distinct
  across headsets, and the input should include the newest logs for each device.
* Scene load is the maximum valid `scene_ready` `loadSeconds`.
* FPS and overrun percentage are independently weighted by positive sample
  `seconds` across all selected logs, not by session averages. Overruns are labeled
  `> 17.4 ms`. Invalid metric values are diagnosed and omitted; valid fields still
  contribute. Missing measurements print `n/a` with a reason.

Every metric prints even for an empty folder/filter result; the CSV still gets its
header. CSV durations cover observed records only. Open, suspended, ended,
abnormal and unknown-device states remain explicit for manual review.

The supplied fixture-answer verification (#312) is pending the original fixtures;
temporary synthetic checks are not that acceptance evidence. The completed
[October 19 Drive-log audit (#313)](audits/2026-10-19/README.md) includes the
201-session output, index reconciliation, three raw-log checks and data limitations.
