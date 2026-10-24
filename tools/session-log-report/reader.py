"""Read schema-1 Unity session logs without changing the source files."""

from dataclasses import dataclass
from datetime import datetime, timezone
import fnmatch
import json
import math
import os
from pathlib import Path


def utc_time(value):
    """Parse an ISO date/time; date-only filters mean midnight UTC."""
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


@dataclass
class Session:
    path: Path
    records: list

    @property
    def first(self):
        return self.records[0]

    @property
    def session_id(self):
        return self.first['sessionId']

    @property
    def started(self):
        return utc_time(self.first['timestampUtc'])


def _reject_constant(value):
    raise ValueError('non-finite JSON number: ' + value)


def read_session(path):
    """Return a session and warnings; only a malformed unfinished tail is ignored."""
    records, warnings = [], []
    with path.open('rb') as source:
        for number, raw in enumerate(source, 1):
            try:
                line = raw.decode('utf-8-sig' if number == 1 else 'utf-8')
                if not line.strip():
                    continue
                entry = json.loads(line, parse_constant=_reject_constant)
            except (UnicodeError, ValueError) as error:
                if records and not raw.endswith(b'\n'):
                    warnings.append(f'{path}:{number}: ignored unfinished final record')
                    break
                raise ValueError(f'line {number}: invalid UTF-8/JSON ({error})') from error
            try:
                if not isinstance(entry, dict):
                    raise ValueError('record must be an object')
                if type(entry.get('schemaVersion')) is not int or entry['schemaVersion'] != 1:
                    raise ValueError('expected schemaVersion 1')
                for key in ('sessionId', 'eventName', 'timestampUtc'):
                    if not isinstance(entry.get(key), str) or not entry[key].strip():
                        raise ValueError(f'missing or invalid {key}')
                if not entry['timestampUtc'].endswith('Z'):
                    raise ValueError('timestampUtc must be UTC with a Z suffix')
                utc_time(entry['timestampUtc'])
                if entry.get('mode') not in ('Story', 'Event'):
                    raise ValueError('mode must be Story or Event')
                if type(entry.get('sequence')) is not int or entry['sequence'] != len(records):
                    raise ValueError('sequence must start at zero and be consecutive')
                elapsed = entry.get('elapsedSeconds')
                if type(elapsed) not in (int, float) or not math.isfinite(elapsed) or elapsed < 0:
                    raise ValueError('invalid elapsedSeconds')
                for key in ('details', 'buildStamp', 'deviceName'):
                    if key in entry and not isinstance(entry[key], str):
                        raise ValueError(f'{key} must be a string')
                if not records:
                    if entry['eventName'] != 'session_start':
                        raise ValueError('first record must be session_start')
                elif (entry['sessionId'] != records[0]['sessionId'] or
                      entry['mode'] != records[0]['mode'] or
                      entry['eventName'] == 'session_start' or
                      records[-1]['eventName'] == 'session_end' or
                      elapsed < records[-1]['elapsedSeconds']):
                    raise ValueError('inconsistent session records')
            except (ValueError, OverflowError) as error:
                raise ValueError(f'line {number}: {error}') from error
            records.append(entry)
    if not records:
        raise ValueError('no complete session records')
    return Session(path, records), warnings


def load_sessions(folder):
    """Recursively load session_*.jsonl, keeping the longest copy of each ID.

    Equal record counts keep the lexicographically first path. All skips,
    truncations, duplicate decisions and directory access failures are reported.
    """
    folder = Path(folder)
    if not folder.is_dir():
        raise ValueError(f'not a readable log directory: {folder}')
    diagnostics, paths = [], []
    for root, directories, files in os.walk(
            folder, onerror=lambda error: diagnostics.append(f'Skipped directory: {error}')):
        directories.sort()
        paths.extend(Path(root) / name for name in files
                     if fnmatch.fnmatchcase(name, 'session_*.jsonl'))
    selected = {}
    for path in sorted(paths):
        try:
            session, warnings = read_session(path)
        except (OSError, ValueError) as error:
            diagnostics.append(f'Skipped {path}: {error}')
            continue
        diagnostics.extend(warnings)
        previous = selected.get(session.session_id)
        if previous is None:
            selected[session.session_id] = session
            continue
        kept, dropped = (session, previous) if len(session.records) > len(previous.records) else (previous, session)
        selected[session.session_id] = kept
        diagnostics.append(f'Duplicate {session.session_id}: kept {kept.path} '
                           f'({len(kept.records)} records); skipped {dropped.path} '
                           f'({len(dropped.records)} records)')
    return sorted(selected.values(), key=lambda session: (session.started, session.session_id)), diagnostics
