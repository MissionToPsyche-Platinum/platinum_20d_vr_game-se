const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const source = fs.readFileSync(path.join(__dirname, 'Code.gs'), 'utf8');
const headers = ['uploadTimeUtc', 'deviceName', 'sessionId', 'buildStamp', 'flavor',
    'startTimeUtc', 'durationSeconds', 'endReason', 'eventCount'];

function records(reason = 'application_quit') {
    return ['session_start', 'scene_loaded', 'session_end'].map((eventName, sequence) => ({
        schemaVersion: 1, sessionId: 'session-123', sequence,
        timestampUtc: `2026-09-26T12:00:0${sequence}.0000000Z`,
        elapsedSeconds: sequence * 1.25, eventName, mode: 'Event',
        scene: 'Bedroom', details: eventName === 'session_end' ? reason : '',
        applicationVersion: '0.1.0', platform: 'Android'
    }));
}

function jsonl(entries) { return entries.map(entry => JSON.stringify(entry)).join('\n') + '\n'; }
function payload() {
    return { token: 'test-only-token', deviceName: 'Quest 2', buildStamp: 'abc1234 UTC event',
        type: 'event', fileName: 'session_test.jsonl', content: jsonl(records()) };
}

// Run the actual Apps Script source with in-memory Google service boundaries.
function harness(options = {}) {
    const state = { files: new Map(), rows: [], accesses: 0, releases: 0, flushes: 0, locked: false };
    const checkLock = () => assert.equal(state.locked, true);
    const context = vm.createContext({
        PropertiesService: { getScriptProperties: () => ({ getProperties: () => ({
            UPLOAD_TOKEN: 'test-only-token', DRIVE_FOLDER_ID: 'folder', SPREADSHEET_ID: 'spreadsheet',
            SHEET_NAME: 'Sessions', ...options.config
        }) }) },
        LockService: { getScriptLock: () => ({
            tryLock: () => (state.locked = !options.busy),
            releaseLock: () => { state.releases++; state.locked = false; }
        }) },
        DriveApp: { getFolderById: id => {
            state.accesses++; checkLock(); assert.equal(id, 'folder');
            if (options.driveFailure) throw new Error('private service details');
            return {
                getFilesByName: name => {
                    let remaining = state.files.has(name) ? (options.duplicates ? 2 : 1) : 0;
                    return {
                        hasNext: () => remaining > 0,
                        next: () => { remaining--; return { setContent: content => {
                            checkLock(); state.files.set(name, content);
                        } }; }
                    };
                },
                createFile: (name, content, mime) => {
                    checkLock(); assert.equal(mime, 'text/plain'); state.files.set(name, content);
                }
            };
        } },
        SpreadsheetApp: {
            openById: id => {
                state.accesses++; checkLock(); assert.equal(id, 'spreadsheet');
                return { getSheetByName: name => {
                    assert.equal(name, 'Sessions');
                    if (options.missingSheet) return null;
                    return {
                        getLastRow: () => state.rows.length,
                        getRange: () => ({ getValues: () => [state.rows[0]] }),
                        appendRow: row => {
                            checkLock();
                            if (options.appendFailure) throw new Error('private Sheet error');
                            state.rows.push(Array.from(row));
                        }
                    };
                } };
            },
            flush: () => { checkLock(); state.flushes++; }
        },
        MimeType: { PLAIN_TEXT: 'text/plain' },
        ContentService: {
            MimeType: { JSON: 'application/json' },
            createTextOutput: text => ({ setMimeType: mime => ({ text, mime }) })
        }
    });
    vm.runInContext(source, context);
    function raw(event) {
        const result = context.doPost(event);
        assert.equal(result.mime, 'application/json');
        return JSON.parse(result.text);
    }
    return { state, raw, post: request => raw({ postData: { contents: JSON.stringify(request) } }) };
}

test('valid upload stores exact content and all nine summary columns', () => {
    const h = harness(); const request = payload();
    assert.deepEqual(h.post(request), { ok: true });
    assert.equal(h.state.files.get(request.fileName), request.content);
    assert.deepEqual(h.state.rows[0], headers);
    assert.ok(Number.isFinite(Date.parse(h.state.rows[1][0])));
    assert.deepEqual(h.state.rows[1].slice(1), ['Quest 2', 'session-123', request.buildStamp,
        'event', records()[0].timestampUtc, 2.5, 'quit', 3]);
    assert.equal(h.state.releases, 1); assert.equal(h.state.flushes, 1);
});

test('retry replaces one Drive file but appends another Sheet row', () => {
    const h = harness(); const request = payload();
    assert.equal(h.post(request).ok, true);
    request.content = jsonl(records('restart'));
    assert.equal(h.post(request).ok, true);
    assert.equal(h.state.files.size, 1); assert.equal(h.state.rows.length, 3);
    assert.equal(h.state.files.get(request.fileName), request.content);
    assert.equal(h.state.rows[2][7], 'restart');
});

for (const token of [undefined, '', 'wrong', 123]) {
    test(`reject token ${String(token)} without touching storage`, () => {
        const h = harness();
        assert.deepEqual(h.post({ ...payload(), token }), { ok: false, error: 'Invalid token.' });
        assert.equal(h.state.accesses, 0);
    });
}

test('malformed bodies and incomplete configuration return JSON without storage access', () => {
    const h = harness();
    for (const event of [undefined, {}, { postData: { contents: '{' } }, { postData: { contents: 'null' } }]) {
        assert.equal(h.raw(event).ok, false);
    }
    for (const key of ['UPLOAD_TOKEN', 'DRIVE_FOLDER_ID', 'SPREADSHEET_ID', 'SHEET_NAME']) {
        const missing = harness({ config: { [key]: '' } });
        assert.equal(missing.post(payload()).ok, false);
        assert.equal(missing.state.accesses, 0);
    }
    assert.equal(h.state.accesses, 0);
});

for (const reason of ['restart', 'mode_change', 'logger_destroyed']) {
    test(`preserve end reason ${reason}`, () => {
        const h = harness();
        assert.equal(h.post({ ...payload(), content: jsonl(records(reason)) }).ok, true);
        assert.equal(h.state.rows[1][7], reason);
    });
}

test('unfinished session uses last complete event; saves truncated content unchanged', () => {
    for (const tail of ['', '{"schemaVersion":']) {
        const h = harness(); const request = payload();
        request.content = jsonl(records().slice(0, 2)) + tail;
        assert.equal(h.post(request).ok, true);
        assert.equal(h.state.files.get(request.fileName), request.content);
        assert.deepEqual(h.state.rows[1].slice(6), [1.25, 'suspended', 2]);
    }
});

test('story flavor, BOM, CRLF, blank lines, and no final newline are supported', () => {
    const h = harness();
    const entries = records().map(entry => ({ ...entry, mode: 'Story' }));
    const content = '\uFEFF' + jsonl(entries).trimEnd().replaceAll('\n', '\r\n\r\n');
    assert.equal(h.post({ ...payload(), type: 'story', content }).ok, true);
    assert.equal(h.state.rows[1][4], 'story'); assert.equal(h.state.rows[1][8], 3);
});

test('invalid request fields do not write files or rows', () => {
    const invalid = [
        { deviceName: '' }, { buildStamp: {} }, { type: 'unknown' }, { fileName: '../bad.jsonl' },
        { fileName: 'bad.txt' }, { fileName: 'bad\\file.jsonl' }, { content: ' ' },
        { content: 'a'.repeat(1000001) }, { deviceName: 'a'.repeat(257) }
    ];
    for (const patch of invalid) {
        const h = harness(); assert.equal(h.post({ ...payload(), ...patch }).ok, false);
        assert.equal(h.state.accesses, 0);
    }
});

test('corrupt records, mixed sessions, and inconsistent sequences fail before storage', () => {
    const changes = [
        entries => { entries[1].sessionId = 'other'; },
        entries => { entries[1].sequence = 9; },
        entries => { entries[1].elapsedSeconds = -1; },
        entries => { entries[2].elapsedSeconds = 0; },
        entries => { entries[0].eventName = 'scene_loaded'; },
        entries => { entries[1].schemaVersion = 2; },
        entries => { entries[1].timestampUtc = 'bad'; },
        entries => { entries[1].mode = 'Story'; },
        entries => { entries[2].details = 'unknown'; },
        entries => { entries.push({ ...entries[2], sequence: 3 }); }
    ];
    const contents = changes.map(change => { const entries = records(); change(entries); return jsonl(entries); });
    contents.push('{broken}\n', jsonl(records().slice(0, 1)) + '{broken}\n',
        JSON.stringify(records()[0]) + '\n{broken}\n' + JSON.stringify(records()[2]));
    for (const content of contents) {
        const h = harness(); assert.equal(h.post({ ...payload(), content }).ok, false);
        assert.equal(h.state.accesses, 0);
    }
});

test('formula-like metadata is written as literal text', () => {
    const h = harness();
    assert.equal(h.post({ ...payload(), deviceName: '=IMPORTXML("url")', buildStamp: ' +123' }).ok, true);
    assert.equal(h.state.rows[1][1], '\'=IMPORTXML("url")');
    assert.equal(h.state.rows[1][3], "' +123");
});

test('lock contention and service failures never acknowledge success', () => {
    for (const options of [{ busy: true }, { driveFailure: true }, { missingSheet: true }, { appendFailure: true }]) {
        const h = harness(options); const result = h.post(payload());
        assert.equal(result.ok, false); assert.ok(result.error);
        assert.ok(!result.error.includes('private'));
        assert.equal(h.state.releases, options.busy ? 0 : 1);
        if (options.busy || options.missingSheet) assert.equal(h.state.files.size, 0);
    }
});

test('unexpected headers or pre-existing duplicate filenames fail without replacing data', () => {
    const h = harness(); h.state.rows.push(['unrelated sheet']);
    assert.equal(h.post(payload()).ok, false); assert.equal(h.state.files.size, 0);
    const duplicate = harness({ duplicates: true });
    duplicate.state.files.set(payload().fileName, 'original');
    assert.equal(duplicate.post(payload()).ok, false);
    assert.equal(duplicate.state.files.get(payload().fileName), 'original');
    assert.equal(duplicate.state.rows.length, 0);
});
