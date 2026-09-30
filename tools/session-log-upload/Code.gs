// TG-281: standalone Apps Script web app. Configuration lives in Script Properties.
const SESSION_HEADERS = [
    'uploadTimeUtc', 'deviceName', 'sessionId', 'buildStamp', 'flavor',
    'startTimeUtc', 'durationSeconds', 'endReason', 'eventCount'
];
const MAX_CONTENT_CHARACTERS = 1000000;

/** Accept one JSONL session. Only acknowledge after both storage writes succeed. */
function doPost(e) {
    let lock;
    let locked = false;
    try {
        let request;
        try {
            request = JSON.parse(e.postData.contents);
        } catch (_) {
            invalid_('Expected a JSON request body.');
        }
        if (!request || typeof request !== 'object' || Array.isArray(request)) {
            invalid_('Expected a JSON object.');
        }

        const config = PropertiesService.getScriptProperties().getProperties();
        if (!config.UPLOAD_TOKEN) return json_({ ok: false, error: 'Upload is not configured.' });
        if (typeof request.token !== 'string' || request.token !== config.UPLOAD_TOKEN) {
            return json_({ ok: false, error: 'Invalid token.' });
        }
        if (!config.DRIVE_FOLDER_ID || !config.SPREADSHEET_ID || !config.SHEET_NAME) {
            return json_({ ok: false, error: 'Upload is not configured.' });
        }

        validateRequest_(request);
        const summary = summarize_(request.content, request.type);
        const row = [
            new Date().toISOString(), request.deviceName, summary.sessionId,
            request.buildStamp, request.type, summary.startTimeUtc,
            summary.durationSeconds, summary.endReason, summary.eventCount
        ].map(sheetValue_);

        // A script lock covers lookup/create and Sheet header/append across requests.
        lock = LockService.getScriptLock();
        locked = lock.tryLock(30000);
        if (!locked) return json_({ ok: false, error: 'Upload is busy. Retry later.' });

        const folder = DriveApp.getFolderById(config.DRIVE_FOLDER_ID);
        const sheet = SpreadsheetApp.openById(config.SPREADSHEET_ID).getSheetByName(config.SHEET_NAME);
        if (!sheet) throw new Error('Missing configured sheet.');
        const emptySheet = sheet.getLastRow() === 0;
        if (!emptySheet) {
            const headers = sheet.getRange(1, 1, 1, SESSION_HEADERS.length).getValues()[0];
            if (headers.some((value, index) => value !== SESSION_HEADERS[index])) {
                throw new Error('Unexpected sheet headers.');
            }
        }

        const files = folder.getFilesByName(request.fileName);
        if (files.hasNext()) {
            const file = files.next();
            // Do not choose arbitrarily if this folder already contains duplicates.
            if (files.hasNext()) throw new Error('Duplicate destination filenames.');
            file.setContent(request.content);
        } else {
            folder.createFile(request.fileName, request.content, MimeType.PLAIN_TEXT);
        }
        if (emptySheet) sheet.appendRow(SESSION_HEADERS);
        sheet.appendRow(row);
        SpreadsheetApp.flush();
        return json_({ ok: true });
    } catch (error) {
        // Never echo service errors, configuration, tokens, or log contents.
        return json_({
            ok: false,
            error: error.name === 'InvalidUpload' ? error.message : 'Unable to store upload. Retry later.'
        });
    } finally {
        if (locked) lock.releaseLock();
    }
}

function validateRequest_(request) {
    for (const field of ['deviceName', 'buildStamp', 'fileName', 'content']) {
        if (typeof request[field] !== 'string' || !request[field].trim()) {
            invalid_('Missing or invalid ' + field + '.');
        }
    }
    if (request.type !== 'story' && request.type !== 'event') invalid_('type must be story or event.');
    if (request.deviceName.length > 256 || request.buildStamp.length > 512) invalid_('Metadata is too long.');
    if (request.fileName.length > 255 || !/^[^/\\\x00-\x1f\x7f]+\.jsonl$/.test(request.fileName)) {
        invalid_('fileName must be a plain .jsonl filename.');
    }
    if (request.content.length > MAX_CONTENT_CHARACTERS) invalid_('Session content is too large.');
}

/** Parse schema v1, including an interrupted session with an unfinished last line. */
function summarize_(content, flavor) {
    const lines = content.replace(/^\uFEFF/, '').split('\n');
    const entries = [];
    for (let index = 0; index < lines.length; index++) {
        if (!lines[index].trim()) continue;
        let entry;
        try {
            entry = JSON.parse(lines[index]);
        } catch (_) {
            // StreamWriter terminates complete records with a newline. Only an
            // unterminated, unparseable final record can be crash truncation.
            if (index === lines.length - 1 && entries.length > 0 && !content.endsWith('\n')) break;
            invalid_('Invalid JSONL at line ' + (index + 1) + '.');
        }
        if (!entry || Array.isArray(entry) || entry.schemaVersion !== 1 ||
            typeof entry.sessionId !== 'string' || !entry.sessionId.trim() || entry.sessionId.length > 256 ||
            typeof entry.eventName !== 'string' || !entry.eventName.trim() ||
            !Number.isInteger(entry.sequence) || entry.sequence !== entries.length ||
            typeof entry.elapsedSeconds !== 'number' || !Number.isFinite(entry.elapsedSeconds) || entry.elapsedSeconds < 0 ||
            typeof entry.timestampUtc !== 'string' || !/Z$/.test(entry.timestampUtc) || !Number.isFinite(Date.parse(entry.timestampUtc)) ||
            typeof entry.mode !== 'string' || entry.mode.toLowerCase() !== flavor) {
            invalid_('Invalid session record at line ' + (index + 1) + '.');
        }
        if (entries.length === 0) {
            if (entry.eventName !== 'session_start') invalid_('First record must be session_start.');
        } else {
            const previous = entries[entries.length - 1];
            if (entry.sessionId !== entries[0].sessionId || entry.eventName === 'session_start' ||
                previous.eventName === 'session_end' || entry.elapsedSeconds < previous.elapsedSeconds) {
                invalid_('Inconsistent session records.');
            }
        }
        if (entry.eventName === 'session_end' &&
            !['application_quit', 'quit', 'restart', 'mode_change', 'logger_destroyed'].includes(entry.details)) {
            invalid_('Invalid session end reason.');
        }
        entries.push(entry);
    }
    if (!entries.length) invalid_('No complete session records.');
    const first = entries[0];
    const last = entries[entries.length - 1];
    return {
        sessionId: first.sessionId,
        startTimeUtc: first.timestampUtc,
        durationSeconds: Math.round((last.elapsedSeconds - first.elapsedSeconds) * 1000) / 1000,
        endReason: last.eventName === 'session_end'
            ? (last.details === 'application_quit' ? 'quit' : last.details) : 'suspended',
        eventCount: entries.length
    };
}

// appendRow interprets '=' as a formula. Treat all uploaded metadata as text.
function sheetValue_(value) {
    return typeof value === 'string' && /^\s*[=+@-]/.test(value) ? "'" + value : value;
}

function invalid_(message) {
    const error = new Error(message);
    error.name = 'InvalidUpload';
    throw error;
}

function json_(value) {
    return ContentService.createTextOutput(JSON.stringify(value)).setMimeType(ContentService.MimeType.JSON);
}
