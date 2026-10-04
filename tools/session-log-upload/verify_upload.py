"""TG-282: send the ten sample sessions with curl; never save the upload token."""

import argparse
import getpass
import json
from pathlib import Path
import re
import subprocess
import sys
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


def post(url, payload):
    # Body goes through stdin, keeping the secret out of process arguments and files.
    # Do not use -X POST: the ContentService redirect must switch to GET.
    result = subprocess.run([
        'curl', '--disable', '--silent', '--show-error', '--location', '--max-redirs', '5',
        '--proto', '=https', '--proto-redir', '=https', '--connect-timeout', '15',
        '--max-time', '90', '--header', 'Content-Type: application/json',
        '--data-binary', '@-', url,
    ], input=json.dumps(payload), capture_output=True, text=True, timeout=100)
    if result.returncode:
        raise RuntimeError(f'curl failed (exit {result.returncode}); the upload may have reached the server.')
    try:
        response = json.loads(result.stdout)
    except json.JSONDecodeError:
        raise RuntimeError('Non-JSON response. Check deployment URL, anonymous access, and authorization.') from None
    if not isinstance(response, dict) or type(response.get('ok')) is not bool:
        raise RuntimeError('Response must contain a boolean ok field.')
    return response


def verify(url, samples, token, device, stamp):
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
        response = post(url, request)
        if response != {'ok': False, 'error': 'Invalid token.'}:
            raise RuntimeError(f'{label} token was not rejected with Invalid token; stop and inspect deployment.')
        print(f'PASS: {label} token rejected', flush=True)
    results = []
    for sample in samples:
        if post(url, payload(sample))['ok'] is not True:
            raise RuntimeError(f"Upload rejected for {sample['fileName']}; inspect configuration and log format.")
        results.append({'fileName': sample['fileName'], 'ok': True, **sample['expected']})
        print(f"PASS: {sample['fileName']}", flush=True)
    if post(url, payload(samples[0]))['ok'] is not True:
        raise RuntimeError('Repeat upload failed.')
    print('PASS: repeat upload acknowledged. Now check Drive file count and Sheet rows.', flush=True)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('samples', type=Path, help='September 26 ZIP or extracted directory')
    parser.add_argument('--url', help='Deployed Apps Script /exec URL')
    parser.add_argument('--device-name', default='TG-282 sample verification')
    parser.add_argument('--build-stamp', default='legacy-0.1.0-sep26')
    parser.add_argument('--dry-run', action='store_true', help='Show expected summaries without sending anything')
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
    verify(args.url, samples, token, args.device_name, args.build_stamp)
    print('HTTP checks passed: 2 rejections, 10 uploads, 1 repeat. Storage/sharing checks are still required.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, RuntimeError, zipfile.BadZipFile, subprocess.TimeoutExpired) as error:
        print(f'Verification stopped: {error}', file=sys.stderr)
        sys.exit(1)
