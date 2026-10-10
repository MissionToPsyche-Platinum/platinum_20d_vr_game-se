"""TG-282: send the ten sample sessions with curl; never save the upload token."""

import argparse
import getpass
import html
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import uuid
from urllib.parse import urlsplit
import zipfile


def load_samples(source):
    """Read the ZIP in place (no extraction) or an extracted sample directory."""
    if source.is_dir():
        files = [(p.name, p.read_bytes()) for p in sorted(source.rglob('*.jsonl'))]
    else:
        with zipfile.ZipFile(source) as archive:
            files = [(Path(name).name, archive.read(name)) for name in sorted(archive.namelist())
                     if name.endswith('.jsonl') and not name.startswith('__MACOSX/')]
    if len(files) != 10 or len({name for name, _ in files}) != 10:
        raise ValueError('Expected exactly ten JSONL files with distinct filenames.')
    samples = []
    for name, raw in files:
        content = raw.decode('utf-8')
        lines = content.lstrip('\ufeff').split('\n')
        entries = []
        for index, line in enumerate(lines):
            if not line.strip():
                continue
            try:
                entries.append(json.loads(line))
            except json.JSONDecodeError:
                if index == len(lines) - 1 and entries and not content.endswith('\n'):
                    break
                raise ValueError(f'{name}: malformed complete JSONL record.') from None
        if not entries or entries[0].get('eventName') != 'session_start':
            raise ValueError(f'{name}: missing session_start.')
        first, last = entries[0], entries[-1]
        flavor = first.get('mode', '').lower()
        if flavor not in ('story', 'event'):
            raise ValueError(f'{name}: unknown mode.')
        reason = last.get('details') if last.get('eventName') == 'session_end' else 'suspended'
        samples.append({
            'fileName': name, 'content': content, 'type': flavor,
            'expected': {
                'sessionId': first['sessionId'], 'flavor': flavor,
                'startTimeUtc': first['timestampUtc'],
                'durationSeconds': round(last['elapsedSeconds'] - first['elapsedSeconds'], 3),
                'endReason': 'quit' if reason == 'application_quit' else reason,
                'eventCount': len(entries),
            }
        })
    return samples


def redirect_summary(headers):
    """Keep status and destination host only; redirect queries contain private keys."""
    steps = []
    for line in headers.splitlines():
        if line.startswith('HTTP/'):
            steps.append(line.split(' ', 2)[1])
        elif line.lower().startswith('location:'):
            destination = urlsplit(line.split(':', 1)[1].strip())
            steps.append('to ' + (destination.hostname or '[relative URL]'))
    return ' -> '.join(steps) or '[no response headers]'


class ResponseRedirectError(RuntimeError):
    """Google redirected a response back to the POST-only script as a GET."""


def post(url, payload):
    # Body goes through stdin, keeping the secret out of process arguments and files.
    # Do not use -X POST: the ContentService redirect must switch to GET.
    # Avoid reusing a cached ContentService redirect. No secret goes in this URL.
    request_url = url + ('&' if '?' in url else '?') + 'verificationRequest=' + uuid.uuid4().hex
    with tempfile.NamedTemporaryFile(mode='r+', encoding='utf-8') as headers:
        result = subprocess.run([
            'curl', '--disable', '--silent', '--show-error', '--location', '--max-redirs', '5',
            '--proto', '=https', '--proto-redir', '=https', '--connect-timeout', '15',
            '--max-time', '90', '--header', 'Content-Type: application/json',
            '--header', 'Cache-Control: no-cache',
            '--dump-header', headers.name,
            '--write-out', '\nTG282_HTTP:%{http_code} %{content_type}',
            '--data-binary', '@-', request_url,
        ], input=json.dumps(payload), capture_output=True, text=True, timeout=100)
        redirects = redirect_summary(headers.read())
    if result.returncode:
        raise RuntimeError(f'curl failed (exit {result.returncode}); the upload may have reached the server.')
    body, separator, metadata = result.stdout.rpartition('\nTG282_HTTP:')
    if not separator:
        body, metadata = result.stdout, 'unknown'
    try:
        response = json.loads(body)
    except json.JSONDecodeError:
        # Show visible page text only; never dump raw HTML, links, or the token.
        text = re.sub(r'<(script|style)\b[^>]*>.*?</\1>', '', body, flags=re.I | re.S)
        text = html.unescape(re.sub(r'<[^>]+>', ' ', text))
        for key in ('token', 'content'):
            value = payload.get(key)
            if value:
                text = text.replace(value, '[redacted]')
        text = ' '.join(text.split())[:600]
        error_type = ResponseRedirectError if (
            'Script function not found: doGet' in text and
            'to script.googleusercontent.com -> 302 -> to script.google.com' in redirects
        ) else RuntimeError
        raise error_type(f'Non-JSON response (HTTP {metadata}). Redirects: {redirects}. '
                         f'Page text: {text or "[empty]"}') from None
    if not isinstance(response, dict) or type(response.get('ok')) is not bool:
        raise RuntimeError('Response must contain a boolean ok field.')
    return response


def post_with_retries(url, payload, retries=0, retry_counter=None):
    for attempt in range(retries + 1):
        try:
            return post(url, payload)
        except ResponseRedirectError:
            if attempt == retries:
                raise
            if retry_counter is not None:
                retry_counter[0] += 1
            delay = 2 ** (attempt + 1)
            print(f'Response redirect failed; retry {attempt + 1}/{retries} in {delay}s. '
                  'The previous POST may already have added a Sheet row.', flush=True)
            time.sleep(delay)


def verify(url, samples, token, device, stamp, retries=0):
    upload_retries = [0]
    def payload(sample):
        return {key: sample[key] for key in ('fileName', 'content', 'type')} | {
            'token': token, 'deviceName': device, 'buildStamp': stamp}

    # Negative checks use unique names, making unintended writes easy to spot.
    for label in ('missing', 'wrong'):
        request = payload(samples[0])
        request['fileName'] = f'tg282_{label}_token.jsonl'
        if label == 'missing':
            del request['token']
        else:
            request['token'] = token + '-incorrect'
        response = post_with_retries(url, request, retries)
        if response != {'ok': False, 'error': 'Invalid token.'}:
            raise RuntimeError(f'{label} token was not rejected with Invalid token; stop and inspect deployment.')
        print(f'PASS: {label} token rejected', flush=True)
    results = []
    for sample in samples:
        print(f"Uploading: {sample['fileName']}", flush=True)
        if post_with_retries(url, payload(sample), retries, upload_retries)['ok'] is not True:
            raise RuntimeError(f"Upload rejected for {sample['fileName']}; inspect configuration and log format.")
        results.append({'fileName': sample['fileName'], 'ok': True, **sample['expected']})
        print(f"PASS: {sample['fileName']}", flush=True)
    if post_with_retries(url, payload(samples[0]), retries, upload_retries)['ok'] is not True:
        raise RuntimeError('Repeat upload failed.')
    print('PASS: repeat upload acknowledged. Now check Drive file count and Sheet rows.', flush=True)
    print(f'Upload retries: {upload_retries[0]}. Expected new data rows: 11 to {11 + upload_retries[0]}; '
          'compare summaries and keep retry rows in the audit.', flush=True)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('samples', type=Path, help='September 26 ZIP or extracted directory')
    parser.add_argument('--url', help='Deployed Apps Script /exec URL')
    parser.add_argument('--device-name', default='TG-282 sample verification')
    parser.add_argument('--build-stamp', default='legacy-0.1.0-sep26')
    parser.add_argument('--dry-run', action='store_true', help='Show expected summaries without sending anything')
    parser.add_argument('--diagnose', action='store_true', help='Send only the first sample to diagnose a response failure')
    parser.add_argument('--sample-number', type=int, default=1, choices=range(1, 11),
                        help='Sample to send with --diagnose, in filename order (default: 1)')
    parser.add_argument('--retry-response-errors', type=int, default=0, choices=range(4),
                        help='Opt in to 0–3 retries per Google response-redirect failure; may add extra Sheet rows')
    args = parser.parse_args()
    samples = load_samples(args.samples)
    if args.dry_run:
        print(json.dumps([{'fileName': s['fileName'], **s['expected']} for s in samples], indent=2))
        return
    if not args.url or not re.fullmatch(r'https://script\.google\.com/macros/s/[A-Za-z0-9_-]+/exec', args.url):
        parser.error('--url must be a deployed https://script.google.com/macros/s/.../exec URL')
    token = getpass.getpass('Upload token (hidden): ')
    if not token:
        parser.error('Token cannot be empty.')
    if args.diagnose:
        request = {key: samples[args.sample_number - 1][key] for key in ('fileName', 'content', 'type')}
        request.update(token=token, deviceName=args.device_name, buildStamp=args.build_stamp)
        response = post_with_retries(args.url, request, args.retry_response_errors)
        print('Single-sample result: ' + json.dumps(response).replace(token, '[redacted]'))
        print('This request may have written one file and one summary row. Check both destinations.')
        return
    verify(args.url, samples, token, args.device_name, args.build_stamp, args.retry_response_errors)
    print('HTTP checks passed: 2 rejections, 10 uploads, 1 repeat. Storage/sharing checks are still required.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, RuntimeError, zipfile.BadZipFile, subprocess.TimeoutExpired) as error:
        print(f'Verification stopped: {error}', file=sys.stderr)
        sys.exit(1)
