import contextlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import verify_upload


class VerificationTests(unittest.TestCase):
    def sample_zip(self, directory, count=10):
        target = Path(directory) / 'samples.zip'
        with zipfile.ZipFile(target, 'w') as archive:
            for index in range(count):
                record = dict(eventName='session_start', sessionId=str(index), mode='Event',
                              timestampUtc='2026-09-26T12:00:00Z', elapsedSeconds=0)
                archive.writestr(f'logs/session_{index}.jsonl', json.dumps(record) + '\n')
        return target

    def test_zip_count_and_summary(self):
        with tempfile.TemporaryDirectory() as directory:
            samples = verify_upload.load_samples(self.sample_zip(directory))
            self.assertEqual(len(samples), 10)
            self.assertEqual(samples[0]['expected']['endReason'], 'suspended')
            self.assertEqual(samples[0]['expected']['eventCount'], 1)
            with self.assertRaises(ValueError):
                verify_upload.load_samples(self.sample_zip(directory, 9))

    def test_curl_uses_stdin_and_safe_redirect_method(self):
        response = subprocess.CompletedProcess([], 0, '{"ok":true}', '')
        with patch('verify_upload.subprocess.run', return_value=response) as run:
            self.assertEqual(verify_upload.post('https://example.test', {'token': 'secret'}), {'ok': True})
        args, kwargs = run.call_args
        self.assertNotIn('secret', ' '.join(args[0]))
        self.assertNotIn('-X', args[0])
        self.assertIn('--location', args[0])
        self.assertEqual(json.loads(kwargs['input']), {'token': 'secret'})

    def test_non_json_and_failed_curl_stop(self):
        for response in [subprocess.CompletedProcess([], 0, '<html>login</html>', ''),
                         subprocess.CompletedProcess([], 0, '{"ok":"true"}', ''),
                         subprocess.CompletedProcess([], 7, '', 'network error')]:
            with patch('verify_upload.subprocess.run', return_value=response):
                with self.assertRaises(RuntimeError):
                    verify_upload.post('https://example.test', {})

    def test_thirteen_requests_and_repeat_filename(self):
        with tempfile.TemporaryDirectory() as directory:
            samples = verify_upload.load_samples(self.sample_zip(directory))
        replies = [{'ok': False, 'error': 'Invalid token.'}] * 2 + [{'ok': True}] * 11
        with patch('verify_upload.post', side_effect=replies) as post, contextlib.redirect_stdout(io.StringIO()):
            result = verify_upload.verify('https://example.test', samples, 'secret', 'device', 'stamp')
        self.assertEqual(len(result), 10)
        self.assertEqual(post.call_count, 13)
        requests = [call.args[1] for call in post.call_args_list]
        self.assertNotIn('token', requests[0])
        self.assertNotEqual(requests[1]['token'], 'secret')
        self.assertEqual(requests[-1], requests[2])

    def test_misconfigured_endpoint_stops_before_uploads(self):
        with tempfile.TemporaryDirectory() as directory:
            samples = verify_upload.load_samples(self.sample_zip(directory))
        with patch('verify_upload.post', return_value={'ok': False, 'error': 'Upload is not configured.'}) as post:
            with self.assertRaises(RuntimeError):
                verify_upload.verify('https://example.test', samples, 'secret', 'device', 'stamp')
        self.assertEqual(post.call_count, 1)


if __name__ == '__main__':
    unittest.main()
