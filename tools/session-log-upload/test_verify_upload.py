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
    def test_redirect_retry_requires_opt_in_and_real_acknowledgment(self):
        failure = verify_upload.ResponseRedirectError('response lost')
        with patch('verify_upload.post', side_effect=[failure, {'ok': True}]) as post:
            with self.assertRaises(verify_upload.ResponseRedirectError):
                verify_upload.post_with_retries('url', {})
            self.assertEqual(post.call_count, 1)
        counter = [0]
        with patch('verify_upload.post', side_effect=[failure, {'ok': True}]) as post, \
                patch('verify_upload.time.sleep') as sleep, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(verify_upload.post_with_retries('url', {}, 1, counter), {'ok': True})
            self.assertEqual(post.call_count, 2)
            sleep.assert_called_once_with(2)
        self.assertEqual(counter, [1])

    def test_retry_is_bounded_and_other_errors_are_not_retried(self):
        with patch('verify_upload.post', side_effect=verify_upload.ResponseRedirectError('lost')) as post, \
                patch('verify_upload.time.sleep'), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaises(verify_upload.ResponseRedirectError):
                verify_upload.post_with_retries('url', {}, 3)
            self.assertEqual(post.call_count, 4)
        with patch('verify_upload.post', side_effect=RuntimeError('other error')) as post:
            with self.assertRaises(RuntimeError):
                verify_upload.post_with_retries('url', {}, 3)
            self.assertEqual(post.call_count, 1)
        with patch('verify_upload.post', return_value={'ok': False, 'error': 'Invalid token.'}) as post:
            self.assertFalse(verify_upload.post_with_retries('url', {}, 3)['ok'])
            self.assertEqual(post.call_count, 1)

    def test_redirect_summary_omits_private_url_components(self):
        headers = ('HTTP/2 302\r\nLocation: https://script.googleusercontent.com/macros/echo?key=secret\r\n'
                   'HTTP/2 302\r\nLocation: https://script.google.com/macros/s/private-id/exec\r\nHTTP/2 200\r\n')
        self.assertEqual(verify_upload.redirect_summary(headers),
                         '302 -> to script.googleusercontent.com -> 302 -> to script.google.com -> 200')

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

    def test_http_diagnostics_hide_scripts_and_token(self):
        output = ('<html><script>private script data</script><p>Access denied secret</p></html>'
                  '\nTG282_HTTP:403 text/html')
        with patch('verify_upload.subprocess.run', return_value=subprocess.CompletedProcess([], 0, output, '')):
            with self.assertRaises(RuntimeError) as error:
                verify_upload.post('https://example.test', {'token': 'secret'})
        message = str(error.exception)
        self.assertIn('HTTP 403 text/html', message)
        self.assertIn('Access denied [redacted]', message)
        self.assertNotIn('secret', message)
        self.assertNotIn('private script data', message)

    def test_json_with_http_metadata(self):
        output = '{"ok":true}\nTG282_HTTP:200 application/json'
        with patch('verify_upload.subprocess.run', return_value=subprocess.CompletedProcess([], 0, output, '')):
            self.assertEqual(verify_upload.post('https://example.test', {}), {'ok': True})

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

    def test_each_post_uses_unique_nonsecret_request_url(self):
        response = subprocess.CompletedProcess([], 0, '{"ok":true}', '')
        with patch('verify_upload.subprocess.run', return_value=response) as run:
            verify_upload.post('https://example.test/exec', {'token': 'private-token'})
            verify_upload.post('https://example.test/exec', {'token': 'private-token'})
        urls = [call.args[0][-1] for call in run.call_args_list]
        self.assertNotEqual(urls[0], urls[1])
        for url in urls:
            self.assertTrue(url.startswith('https://example.test/exec?verificationRequest='))
            self.assertNotIn('private-token', url)
        self.assertIn('Cache-Control: no-cache', run.call_args.args[0])

    def test_misconfigured_endpoint_stops_before_uploads(self):
        with tempfile.TemporaryDirectory() as directory:
            samples = verify_upload.load_samples(self.sample_zip(directory))
        with patch('verify_upload.post', return_value={'ok': False, 'error': 'Upload is not configured.'}) as post:
            with self.assertRaises(RuntimeError):
                verify_upload.verify('https://example.test', samples, 'secret', 'device', 'stamp')
        self.assertEqual(post.call_count, 1)


if __name__ == '__main__':
    unittest.main()
