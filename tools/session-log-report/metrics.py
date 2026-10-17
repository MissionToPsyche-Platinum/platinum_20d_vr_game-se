"""Quality-plan calculations, shared by the printed report and CSV rows."""

import math


def number(details, key, minimum=0, maximum=None):
    try:
        value = float(details[key])
    except (KeyError, TypeError, ValueError):
        return None
    if not math.isfinite(value) or value < minimum or (maximum is not None and value > maximum):
        return None
    return value


def weighted(samples):
    seconds = sum(duration for duration, value in samples)
    return sum(duration * value for duration, value in samples) / seconds if seconds else None


def text(value, unit=''):
    return f'{value:.2f}{unit}' if value is not None else 'n/a: no valid samples'


def exit_states(sessions):
    """Identify the latest session on each known device before applying filters."""
    latest = {}
    for session in sessions:
        device = session.first.get('deviceName', '').strip()
        if device:
            latest[device] = max(latest.get(device, session.started), session.started)
    states = {}
    for session in sessions:
        device = session.first.get('deviceName', '').strip()
        last = session.records[-1]['eventName']
        if last == 'session_end':
            state = 'ended'
        elif not device:
            state = 'unknown_device'
        elif session.started == latest[device]:
            state = 'open'
        elif last in ('application_paused', 'application_focus_lost'):
            state = 'suspended'
        else:
            state = 'abnormal'
        states[session.session_id] = state
    return states


def summarize(session, state, diagnostics):
    completion = False
    expired = False
    loads, fps_samples, overrun_samples = [], [], []
    for record in session.records:
        event = record['eventName']
        if event == 'kiosk_clock_expired':
            expired = True
        elif event == 'kiosk_puzzle_completed' and not expired:
            completion = True
        if event not in ('scene_ready', 'perf_sample'):
            continue
        details = dict(part.split('=', 1) for part in record.get('details', '').split(';') if '=' in part)
        if event == 'scene_ready':
            value = number(details, 'loadSeconds')
            if value is not None:
                loads.append(value)
            else:
                diagnostics.append(f'{session.path}: sequence {record["sequence"]}: invalid scene_ready loadSeconds')
        else:
            seconds = number(details, 'seconds')
            fps = number(details, 'fps')
            overrun = number(details, 'overrunPct', maximum=100)
            if seconds is not None and seconds > 0:
                if fps is not None:
                    fps_samples.append((seconds, fps))
                if overrun is not None:
                    overrun_samples.append((seconds, overrun))
            if not seconds or fps is None or overrun is None:
                diagnostics.append(f'{session.path}: sequence {record["sequence"]}: '
                                   'invalid perf_sample fields; only valid values with positive seconds are used')
    visitor = session.first['mode'] == 'Event'
    row = {
        'sessionId': session.session_id,
        'sourceFile': str(session.path),
        'deviceName': session.first.get('deviceName', ''),
        'buildStamp': session.first.get('buildStamp', ''),
        'mode': session.first['mode'],
        'startTimeUtc': session.first['timestampUtc'],
        'recordCount': len(session.records),
        'durationSeconds': session.records[-1]['elapsedSeconds'] - session.first['elapsedSeconds'],
        'lastEvent': session.records[-1]['eventName'],
        'exitState': state,
        'eventCompletedBeforeExpiry': str(completion).lower() if visitor else 'n/a: not Event mode',
        'worstSceneLoadSeconds': text(max(loads) if loads else None),
        'weightedFps': text(weighted(fps_samples)),
        'weightedOverrunPct': text(weighted(overrun_samples)),
        'fpsSampleSeconds': sum(seconds for seconds, value in fps_samples),
        'overrunSampleSeconds': sum(seconds for seconds, value in overrun_samples),
    }
    return row, visitor, completion, loads, fps_samples, overrun_samples


CSV_FIELDS = ['sessionId', 'sourceFile', 'deviceName', 'buildStamp', 'mode',
              'startTimeUtc', 'recordCount', 'durationSeconds', 'lastEvent',
              'exitState', 'eventCompletedBeforeExpiry', 'worstSceneLoadSeconds',
              'weightedFps', 'weightedOverrunPct', 'fpsSampleSeconds', 'overrunSampleSeconds']


def calculate(sessions, states):
    rows, diagnostics, loads, fps, overruns = [], [], [], [], []
    visitors = completed = abnormal = eligible = unknown = 0
    for session in sessions:
        state = states[session.session_id]
        row, visitor, completion, scene, frame, overrun = summarize(session, state, diagnostics)
        rows.append(row)
        visitors += visitor
        completed += visitor and completion
        eligible += state not in ('open', 'unknown_device')
        abnormal += state == 'abnormal'
        unknown += state == 'unknown_device'
        loads.extend(scene)
        fps.extend(frame)
        overruns.extend(overrun)
    completion_rate = f'{100 * completed / visitors:.2f}% ({completed}/{visitors})' if visitors else 'n/a: no visitor sessions'
    if unknown:
        exit_rate = f'n/a: {unknown} unended sessions lack deviceName; cannot identify open sessions'
    elif eligible:
        exit_rate = f'{100 * abnormal / eligible:.2f}% ({abnormal}/{eligible})'
    else:
        exit_rate = 'n/a: no eligible sessions (newest open sessions excluded)'
    metrics = [
        ('Event Completion Rate', completion_rate, '>= 70%'),
        ('Field Abnormal Exit Rate', exit_rate, '0%'),
        ('Scene load time', text(max(loads) if loads else None, ' s'), '<= 10 s'),
        ('Field frame rate', f'fps: {text(weighted(fps))}; overruns > 17.4 ms: {text(weighted(overruns), "%")}', 'overruns < 1%'),
    ]
    return rows, metrics, diagnostics
