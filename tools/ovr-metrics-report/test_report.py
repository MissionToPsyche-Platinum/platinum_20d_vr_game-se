"""Independent fixture answers and CLI boundary checks for TG-350."""

from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

HERE = Path(__file__).resolve().parent


class ReportTests(unittest.TestCase):
    def run_report(self, path, *options):
        return subprocess.run([sys.executable, str(HERE / 'report.py'), str(path), *options],
                              capture_output=True, text=True)

    def values(self, result):
        self.assertEqual(result.returncode, 0, result.stderr)
        return [line.split('|')[1].strip() for line in result.stdout.splitlines()[3:]]

    def test_fixture(self):
        path = HERE / 'fixtures/trial.csv'
        before = path.read_bytes()
        result = self.run_report(path)
        # Stale sum 29 / FPS sum 292. FPS windows: (80+60)/2,
        # (60+52)/2; drop = (70-56)/70. Interior temperature peaks at 35.
        self.assertEqual(self.values(result), ['9.93%', '70.00', '56.00', '20.00%', '35.00 C'])
        self.assertEqual(result.stderr, '')
        self.assertEqual(path.read_bytes(), before)

    def check_csv(self, content, *options):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'capture.csv'
            path.write_text(content, encoding='utf-8')
            return self.run_report(path, *options)

    def test_missing_columns_and_no_inferred_overruns(self):
        result = self.check_csv('Time Stamp,average_frame_rate\n0,60\n700000,30\n')
        self.assertEqual(self.values(result), ['n/a', '60.00', '30.00', '50.00%', 'n/a'])
        self.assertIn('Missing column: battery_temperature_celcius', result.stderr)

    def test_bom_custom_headers_milliseconds_and_unsorted_rows(self):
        result = self.check_csv('\ufeffclock,rate,heat\n700000,30,32\n0,60,31\n',
                                '--time-column', 'clock', '--time-unit', 'ms',
                                '--fps-column', 'rate', '--temperature-column', 'heat')
        self.assertEqual(self.values(result), ['n/a', '60.00', '30.00', '50.00%', '32.00 C'])

    def test_invalid_values_do_not_discard_independent_temperature(self):
        result = self.check_csv('Time Stamp,average_frame_rate,battery_temperature_celcius\n0,0,30\n700000,nan,40\nbad,-1,inf\n')
        self.assertEqual(self.values(result), ['n/a', '0.00', 'n/a', 'n/a', '40.00 C'])
        self.assertIn('Ignored 4 invalid measurement(s)', result.stderr)

    def test_short_capture_windows_overlap_and_improvement_is_negative_drop(self):
        result = self.check_csv('Time Stamp,average_frame_rate\n0,60\n5000,80\n')
        self.assertEqual(self.values(result)[1:4], ['70.00', '70.00', '0.00%'])
        result = self.check_csv('Time Stamp,average_frame_rate\n0,40\n700000,60\n')
        self.assertEqual(self.values(result)[3], '-50.00%')

    def test_empty_measurements(self):
        result = self.check_csv('Time Stamp,average_frame_rate\n')
        self.assertEqual(self.values(result), ['n/a'] * 5)

    def test_seconds_and_exact_ten_minute_boundaries(self):
        result = self.check_csv(
            'Time Stamp,average_frame_rate\n'
            '0,80\n599,60\n600,10\n1200,20\n1201,50\n1800,70\n',
            '--time-unit', 's')
        # 600 is outside the first window; 1200 is outside the last.
        self.assertEqual(self.values(result)[1:4], ['70.00', '60.00', '14.29%'])

    def test_missing_time_preserves_independent_metrics(self):
        result = self.check_csv(
            'average_frame_rate,stale_frame_count,battery_temperature_celcius\n'
            '80,8,30\n20,2,40\n')
        self.assertEqual(self.values(result), ['10.00%', 'n/a', 'n/a', 'n/a', '40.00 C'])
        self.assertIn('Missing column: Time Stamp', result.stderr)

    def test_blank_measurements_are_unavailable_without_invalid_warning(self):
        result = self.check_csv(
            'Time Stamp,average_frame_rate,stale_frame_count,battery_temperature_celcius\n'
            '0,,,\n700000,,,\n')
        self.assertEqual(self.values(result), ['n/a'] * 5)
        self.assertEqual(result.stderr, '')

    def test_stale_counts_use_sum_of_fps_not_rows_or_mean_of_ratios(self):
        result = self.check_csv(
            'Time Stamp,average_frame_rate,stale_frame_count\n'
            '1000,80,0\n2000,20,10\n')
        self.assertEqual(self.values(result)[0], '10.00%')

    def test_stale_ratio_requires_valid_pairs_and_nonzero_denominator(self):
        for measurements, expected in [
                ('1000,0,2\n', 'n/a'),
                ('1000,60,0\n', '0.00%'),
                ('1000,80,8\n2000,nan,100\n3000,20,\n4000,20,-1\n', '10.00%')]:
            with self.subTest(measurements=measurements):
                result = self.check_csv(
                    'Time Stamp,average_frame_rate,stale_frame_count\n' + measurements)
                self.assertEqual(self.values(result)[0], expected)

    def test_unreadable_and_malformed_inputs(self):
        for content in ['', 'FPS,FPS\n1,2\n', 'a,b\n1,2,3\n', 'a,b\n1\n', 'a,b\n"unclosed,2\n']:
            with self.subTest(content=content):
                result = self.check_csv(content)
                self.assertEqual(result.returncode, 2)
                self.assertNotIn('Traceback', result.stderr)
        result = self.run_report(HERE / 'fixtures/missing.csv')
        self.assertEqual(result.returncode, 2)


if __name__ == '__main__':
    unittest.main()
