"""TG-309: read a folder of session logs for quality-plan reporting."""

import argparse
import csv
from pathlib import Path
import sys

from reader import load_sessions, utc_time
from metrics import CSV_FIELDS, calculate, exit_states


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', nargs='?', type=Path, default=Path('.'),
                        help='extracted log folder, searched recursively (default: current directory)')
    parser.add_argument('--build', help='exact buildStamp to include')
    parser.add_argument('--since', help='inclusive session start date/time, ISO 8601 (default timezone: UTC)')
    parser.add_argument('--output', type=Path, default=Path('sessions.csv'), help='CSV destination (default: sessions.csv)')
    args = parser.parse_args(argv)
    try:
        since = utc_time(args.since) if args.since else None
        sessions, diagnostics = load_sessions(args.folder)
    except (ValueError, OverflowError) as error:
        parser.error(str(error))
    # Never overwrite a source log, including via an existing output symlink.
    if args.output.suffix.lower() == '.jsonl' or args.output.resolve().suffix.lower() == '.jsonl':
        parser.error('CSV output must not overwrite a JSONL log')
    states = exit_states(sessions)
    sessions = [session for session in sessions
                if (args.build is None or session.first.get('buildStamp') == args.build)
                and (since is None or session.started >= since)]
    rows, metrics, metric_diagnostics = calculate(sessions, states)
    diagnostics.extend(metric_diagnostics)
    for message in diagnostics:
        print(message, file=sys.stderr)
    print(f'Selected {len(sessions)} unique sessions; '
          f'{sum(states[s.session_id] == "open" for s in sessions)} open sessions excluded from exit rate')
    width = max(len(value) for _, value, _ in metrics)
    print(f'{"Metric":<25} | {"Value":<{width}} | Target')
    print('-' * (width + 48))
    for label, value, target in metrics:
        print(f'{label:<25} | {value:<{width}} | {target}')
    try:
        with args.output.open('w', encoding='utf-8', newline='') as output:
            writer = csv.DictWriter(output, fieldnames=CSV_FIELDS)
            writer.writeheader()
            writer.writerows(rows)
    except OSError as error:
        print(f'Cannot write {args.output}: {error}', file=sys.stderr)
        return 1
    print(f'Wrote {args.output}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
