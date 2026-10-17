"""TG-309: read a folder of session logs for quality-plan reporting."""

import argparse
from pathlib import Path
import sys

from reader import load_sessions


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', nargs='?', type=Path, default=Path('.'),
                        help='extracted log folder, searched recursively (default: current directory)')
    args = parser.parse_args(argv)
    try:
        sessions, diagnostics = load_sessions(args.folder)
    except ValueError as error:
        parser.error(str(error))
    for message in diagnostics:
        print(message, file=sys.stderr)
    print(f'Loaded {len(sessions)} unique sessions')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
