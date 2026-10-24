"""TG-312: regression checks against committed, independently specified fixtures."""

import csv
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from reader import load_sessions, read_session
from metrics import calculate, exit_states

HERE = Path(__file__).resolve().parent
FIXTURES = HERE / 'fixtures'
EXPECTED = json.loads((FIXTURES / 'expected.json').read_text())
LABELS = ['Event Completion Rate', 'Field Abnormal Exit Rate', 'Scene load time', 'Field frame rate']


class ReportTests(unittest.TestCase):
    def run_report(self, group='main', *options):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(
                [sys.executable, str(HERE / 'report.py'), str(FIXTURES / group), *options],
                cwd=directory, capture_output=True, text=True,
                env={**os.environ, 'PYTHONDONTWRITEBYTECODE': '1'})
            output = Path(directory) / 'sessions.csv'
            rows = None
            if output.exists():
                with output.open(newline='') as source:
                    rows = list(csv.DictReader(source))
            return result, rows

    def assert_metrics(self, result, expected):
        self.assertEqual(result.returncode, 0, result.stderr)
        lines = [line for line in result.stdout.splitlines()
                 if any(line.startswith(label + ' ') for label in LABELS)]
        self.assertEqual(len(lines), 4)
        self.assertEqual([line.split('|')[0].strip() for line in lines], LABELS)
        self.assertEqual([line.split('|')[1].strip() for line in lines], expected)
        self.assertEqual([line.split('|')[2].strip() for line in lines],
                         ['>= 70%', '0%', '<= 10 s', 'overruns < 1%'])

    def test_fixture_answers_and_csv(self):
        result, rows = self.run_report()
        self.assert_metrics(result, EXPECTED['main']['metrics'])
        by_id = {row['sessionId']: row for row in rows}
        self.assertEqual(len(rows), 5)
        self.assertEqual(set(by_id), set(EXPECTED['main']['rows']))
        for sid, answers in EXPECTED['main']['rows'].items():
            for field, answer in answers.items():
                with self.subTest(session=sid, field=field):
                    self.assertEqual(by_id[sid][field], answer)
        self.assertTrue(by_id['complete']['sourceFile'].endswith('nested/session_complete.jsonl'))

    def test_diagnostics_name_bad_duplicate_and_truncated_files(self):
        result, _ = self.run_report()
        self.assertIn('session_malformed.jsonl', result.stderr)
        self.assertIn('Skipped ', result.stderr)
        self.assertIn('Duplicate complete:', result.stderr)
        self.assertIn('session_short.jsonl', result.stderr)
        self.assertIn('session_expired.jsonl:6: ignored unfinished final record', result.stderr)
        self.assertEqual(len(result.stderr.splitlines()), 3)
        self.assertNotIn('unrelated.jsonl', result.stderr)

    def test_build_filter_keeps_global_device_history(self):
        result, rows = self.run_report('main', '--build', 'fixture-v1')
        self.assert_metrics(result, EXPECTED['build_v1']['metrics'])
        self.assertEqual(sorted(r['sessionId'] for r in rows), EXPECTED['build_v1']['ids'])
        self.assertEqual(next(r for r in rows if r['sessionId'] == 'suspended')['exitState'], 'suspended')

    def test_since_is_inclusive_and_normalizes_offsets(self):
        for cutoff in ['2026-10-02', '2026-10-01T17:00:00-07:00']:
            with self.subTest(cutoff=cutoff):
                result, rows = self.run_report('main', '--since', cutoff)
                self.assert_metrics(result, EXPECTED['since_oct2']['metrics'])
                self.assertEqual(sorted(r['sessionId'] for r in rows), EXPECTED['since_oct2']['ids'])

    def test_empty_filter_writes_header_and_all_metrics(self):
        result, rows = self.run_report('main', '--build', 'absent')
        self.assert_metrics(result, EXPECTED['empty']['metrics'])
        self.assertEqual(rows, [])

    def test_legacy_device_is_not_assumed_abnormal(self):
        result, rows = self.run_report('legacy')
        self.assert_metrics(result, EXPECTED['legacy']['metrics'])
        self.assertEqual(rows[0]['exitState'], 'unknown_device')

    def test_invalid_measurements_preserve_independent_valid_fields(self):
        result, rows = self.run_report('invalid_metrics')
        self.assert_metrics(result, EXPECTED['invalid_metrics']['metrics'])
        self.assertEqual(len(result.stderr.splitlines()), 4)
        self.assertEqual(rows[0]['fpsSampleSeconds'], '20.0')
        self.assertEqual(rows[0]['overrunSampleSeconds'], '10.0')

    def test_bad_date_and_missing_folder_fail_clearly(self):
        for group, options in [('main', ['--since', 'not-a-date']), ('missing', [])]:
            with self.subTest(group=group):
                result, rows = self.run_report(group, *options)
                self.assertEqual(result.returncode, 2)
                self.assertIn('error:', result.stderr)
                self.assertIsNone(rows)

    def test_sources_and_expected_answers_remain_unchanged(self):
        before = {p: p.read_bytes() for p in FIXTURES.rglob('*') if p.is_file()}
        self.run_report()
        result, _ = self.run_report('main', '--output', str(FIXTURES / 'main/session_open.jsonl'))
        self.assertEqual(result.returncode, 2)
        self.assertEqual(before, {p: p.read_bytes() for p in FIXTURES.rglob('*') if p.is_file()})


class ReaderBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.records = [json.loads(line) for line in
                        (FIXTURES / 'main/nested/session_complete.jsonl').read_text().splitlines()]

    def write(self, records, name='session_test.jsonl'):
        path = self.root / name
        path.write_text(''.join(json.dumps(record) + '\n' for record in records))
        return path

    def test_bom_crlf_and_complete_unterminated_line(self):
        path = self.write(self.records)
        path.write_bytes(('\ufeff' + '\r\n\r\n'.join(json.dumps(r) for r in self.records)).encode())
        session, warnings = read_session(path)
        self.assertEqual(len(session.records), 7)
        self.assertEqual(warnings, [])

    def test_invalid_complete_records_reject_whole_file(self):
        cases = [('sequence', 99), ('sessionId', 'different'), ('mode', 'Story'),
                 ('elapsedSeconds', -1), ('schemaVersion', 2), ('timestampUtc', 'invalid'),
                 ('details', {}), ('eventName', '')]
        for field, value in cases:
            with self.subTest(field=field):
                records = [dict(r) for r in self.records]
                records[1][field] = value
                self.write(records)
                sessions, warnings = load_sessions(self.root)
                self.assertEqual(sessions, [])
                self.assertIn('session_test.jsonl', warnings[0])

    def test_ended_session_cannot_have_more_records(self):
        records = self.records + [{**self.records[-1], 'sequence': 7}]
        with self.assertRaisesRegex(ValueError, 'inconsistent'):
            read_session(self.write(records))

    def test_duplicate_ties_keep_lexical_first(self):
        self.write(self.records, 'session_z.jsonl')
        self.write(self.records, 'session_a.jsonl')
        sessions, warnings = load_sessions(self.root)
        self.assertEqual(len(sessions), 1)
        self.assertEqual(sessions[0].path.name, 'session_a.jsonl')
        self.assertEqual(len(warnings), 1)

    def test_completion_without_expiry_and_focus_loss(self):
        records = self.records[:5]
        records[-1]['eventName'] = 'kiosk_puzzle_completed'
        records.append({**records[-1], 'sequence': 5, 'elapsedSeconds': 50,
                        'eventName': 'application_focus_lost'})
        self.write(records)
        newer = [{**r, 'sessionId': 'newer', 'timestampUtc': '2026-10-05T00:00:00Z'}
                 for r in self.records]
        self.write(newer, 'session_newer.jsonl')
        sessions, _ = load_sessions(self.root)
        rows, _, _ = calculate(sessions, exit_states(sessions))
        row = next(r for r in rows if r['sessionId'] == 'complete')
        self.assertEqual(row['eventCompletedBeforeExpiry'], 'true')
        self.assertEqual(row['exitState'], 'suspended')

    def test_latest_ended_session_leaves_older_unended_abnormal(self):
        self.write(self.records[:2])
        newer = [{**r, 'sessionId': 'newer', 'timestampUtc': '2026-10-05T00:00:00Z'}
                 for r in self.records]
        self.write(newer, 'session_newer.jsonl')
        sessions, _ = load_sessions(self.root)
        self.assertEqual(exit_states(sessions), {'complete': 'abnormal', 'newer': 'ended'})

    def test_unreadable_file_is_named_and_other_logs_survive(self):
        bad = self.write(self.records, 'session_bad.jsonl')
        self.write(self.records, 'session_good.jsonl')
        real_open = Path.open

        def controlled_open(path, *args, **kwargs):
            if path == bad:
                raise PermissionError('fixture access denied')
            return real_open(path, *args, **kwargs)

        with patch.object(Path, 'open', controlled_open):
            sessions, warnings = load_sessions(self.root)
        self.assertEqual(len(sessions), 1)
        self.assertIn('session_bad.jsonl', warnings[0])
        self.assertIn('fixture access denied', warnings[0])


if __name__ == '__main__':
    unittest.main()
