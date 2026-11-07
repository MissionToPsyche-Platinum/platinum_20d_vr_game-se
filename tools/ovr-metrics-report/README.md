# OVR Metrics CSV report (TG-350)

Python 3.8+; standard library only. From the repository root:

```sh
python3 tools/ovr-metrics-report/report.py /path/to/capture.csv
python3 -m unittest discover -s tools/ovr-metrics-report
```

Prints frame-overrun percentage (stale-frame proxy), first and last ten-minute
average FPS, FPS drop percentage, and maximum battery temperature. Input is
read-only. Missing measurements print `n/a`; diagnostics go to stderr. Invalid
or unreadable CSV files exit with status 2.

Defaults match Zach's TG-303 capture:

| Column | Meaning |
| --- | --- |
| `Time Stamp` | Numeric milliseconds |
| `average_frame_rate` | FPS in each one-second sample |
| `stale_frame_count` | Stale frames in each one-second sample |
| `battery_temperature_celcius` | Battery temperature in Celsius (export spelling) |

The requested over-budget calculation is
`100 * sum(stale_frame_count) / sum(average_frame_rate)`.
It uses paired valid, nonnegative stale-count/FPS measurements; a zero denominator
gives `n/a`. This is the agreed stale-frame proxy for the 17.4 ms metric, not a
measurement of individual frame durations. Each row represents one second.

FPS averages use valid readings in `[start, start + 600)` and `(end - 600, end]`
seconds. Bounds include all valid timestamps. Short captures use available
samples, so the windows can overlap. Drop is `100 * (first - last) / first`;
a zero first average gives `n/a`, and improvement gives a negative drop.
Nonfinite/nonnumeric readings and negative FPS/counts are ignored. Temperature
is the highest valid battery reading, not a surface-temperature measurement.

Alternate exports can use `--time-column`, `--time-unit s`, `--fps-column`,
`--stale-column`, and `--temperature-column`. The count calculation assumes
one-second sampling; it does not compensate for other sampling intervals.

`fixtures/trial.csv` is a small synthetic fixture with the real column names
and sparse timestamps to test window boundaries. Expected results: 29 / 292 *
100 = 9.93%, first FPS 70, last FPS 56, drop 20%, peak 35 C.

## TG-303 verification

Verified on `TG-303-trial-capture-2026-10-28.csv` with default arguments:

Rechecked on November 7, 2026. Source SHA-256:
`dd94428a8cb2b98199f663d19ce33d194bce5f1f8c38183077c3c94582bcb0a2`.

| Metric | Result |
| --- | --- |
| Over-budget frames (stale proxy) | 0.70% (131 / 18,654 * 100) |
| First ten-minute average FPS | 72.30 |
| Last ten-minute average FPS | 72.30 |
| FPS drop | 0.00% |
| Highest battery temperature | 42.00 C |

The capture has 258 samples spanning 258.022 seconds. Both windows therefore
include the entire capture; the zero drop does not establish two-hour stability.
The original capture remains outside the repository.
