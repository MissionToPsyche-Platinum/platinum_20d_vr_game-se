"""TG-350: summarize one OVR Metrics CSV for the quality plan."""

import argparse
import csv
import math
from pathlib import Path
import statistics
import sys


def number(value):
    try:
        result = float(value)
    except (TypeError, ValueError):
        return None
    return result if math.isfinite(result) else None


def calculate(rows, time_column, fps_column, temperature_column,
              stale_column, time_scale=0.001):
    """Scale numeric timestamps to seconds; use independent valid measurements."""
    timed = []
    temperatures = []
    stale_total = fps_total = 0.0
    invalid = 0
    for row in rows:
        timestamp = number(row.get(time_column))
        fps = number(row.get(fps_column))
        temperature = number(row.get(temperature_column))
        stale = number(row.get(stale_column))
        if timestamp is not None:
            timestamp *= time_scale
            timed.append((timestamp, fps if fps is not None and fps >= 0 else None))
        if temperature is not None:
            temperatures.append(temperature)
        # Sum matched one-second samples; never divide counts by row count.
        if stale is not None and stale >= 0 and fps is not None and fps >= 0:
            stale_total += stale
            fps_total += fps
        for column, value, valid in (
                (time_column, timestamp, timestamp is not None),
                (fps_column, fps, fps is not None and fps >= 0),
                (temperature_column, temperature, temperature is not None),
                (stale_column, stale, stale is not None and stale >= 0)):
            if column and row.get(column, '').strip() and not valid:
                invalid += 1

    first = last = drop = None
    if timed:
        start = min(t for t, _ in timed)
        end = max(t for t, _ in timed)
        # Half-open windows avoid counting a boundary sample in adjacent windows.
        first_samples = [fps for t, fps in timed if t < start + 600 and fps is not None]
        last_samples = [fps for t, fps in timed if t > end - 600 and fps is not None]
        first = statistics.fmean(first_samples) if first_samples else None
        last = statistics.fmean(last_samples) if last_samples else None
        if first is not None and first > 0 and last is not None:
            drop = (first - last) / first * 100
    return {
        'Frames > 17.4 ms (stale proxy)': stale_total / fps_total * 100 if fps_total > 0 else None,
        'First 10 minutes average FPS': first,
        'Last 10 minutes average FPS': last,
        'FPS drop': drop,
        'Highest temperature': max(temperatures) if temperatures else None,
    }, invalid


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path, help='CSV capture (read-only)')
    parser.add_argument('--time-column', default='Time Stamp', help='numeric timestamp column (default: %(default)s)')
    parser.add_argument('--time-unit', choices=['s', 'ms'], default='ms')
    parser.add_argument('--fps-column', default='average_frame_rate', help='FPS column (default: %(default)s)')
    parser.add_argument('--temperature-column', default='battery_temperature_celcius',
                        help='temperature column in Celsius (default: %(default)s)')
    parser.add_argument('--stale-column', default='stale_frame_count',
                        help='stale frames per one-second sample (default: %(default)s)')
    args = parser.parse_args(argv)
    try:
        with args.capture.open(encoding='utf-8-sig', newline='') as source:
            reader = csv.DictReader(source, strict=True)
            headers = reader.fieldnames
            if not headers or any(not h.strip() for h in headers) or len(set(headers)) != len(headers):
                raise ValueError('CSV must have nonempty, unique column names')
            rows = []
            for row in reader:
                if None in row or any(value is None for value in row.values()):
                    raise ValueError(f'CSV row ending at line {reader.line_num} has the wrong number of fields')
                rows.append(row)
    except (OSError, UnicodeError, csv.Error, ValueError) as error:
        parser.error(str(error))
    for column in [args.time_column, args.fps_column, args.temperature_column, args.stale_column]:
        if column and column not in headers:
            print(f'Missing column: {column}', file=sys.stderr)
    metrics, invalid = calculate(rows, args.time_column, args.fps_column,
                                 args.temperature_column, args.stale_column,
                                 0.001 if args.time_unit == 'ms' else 1)
    if invalid:
        print(f'Ignored {invalid} invalid measurement(s).', file=sys.stderr)
    print(f'Read {len(rows)} samples from {args.capture.name}')
    width = max(len(label) for label in metrics)
    print(f'{"Metric":<{width}} | Value')
    print('-' * (width + 16))
    for (label, value), unit in zip(metrics.items(), ['%', '', '', '%', ' C']):
        formatted = 'n/a' if value is None else f'{value:.2f}{unit}'
        print(f'{label:<{width}} | {formatted}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
