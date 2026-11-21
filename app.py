"""School timetable reader: read-only Google Sheets XLSX export, including live merged ranges."""
import io
import json
import os
import re
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.request import Request, urlopen

from openpyxl import load_workbook

BASE = Path(__file__).resolve().parent
SHEET_ID = '1hANIlno8mchmA2L6P0F-i-BQv6lORFeBuXAYMWv-e5w'
EXPORT = 'https://docs.google.com/spreadsheets/d/%s/export?format=xlsx' % SHEET_ID
CACHE_SECONDS = 3600
DAY_NAMES = {'ПОНЕДІЛОК': 'Понеділок', 'ВІВТОРОК': 'Вівторок', 'СЕРЕДА': 'Середа',
             'ЧЕТВЕР': 'Четвер', 'П’ЯТНИЦЯ': 'П’ятниця', 'ПЯТНИЦЯ': 'П’ятниця',
             'П\'ЯТНИЦЯ': 'П’ятниця', 'ПОНЕДЕЛЬНИК': 'Понеділок'}
LOCK = threading.Lock()
STATE = {'payload': None, 'checked': 0, 'error': None}


def clean(value):
    return str(value).strip() if value is not None else ''


def parse_workbook(data):
    wb = load_workbook(io.BytesIO(data), data_only=True, read_only=False)
    output = {}
    for ws in wb.worksheets:
        # The starting cell of an Excel merge owns the value. Resolve its entire area.
        merged_anchors = {}
        for region in ws.merged_cells.ranges:
            value = clean(ws.cell(region.min_row, region.min_col).value)
            if value:
                for r in range(region.min_row, region.max_row + 1):
                    for c in range(region.min_col, region.max_col + 1):
                        merged_anchors[(r, c)] = value

        def cell(r, c):
            return clean(ws.cell(r, c).value) or merged_anchors.get((r, c), '')

        classes = []
        for col in range(4, min(ws.max_column, 60) + 1):
            title = cell(1, col)
            # School classes are labelled with a leading digit and a grade name.
            if re.match(r'^\d{1,2}(?:\s*[-–А-Яа-яA-Za-z]|\s)', title) and not re.match(r'^\d+(?:[.,]\d+)?$', title):
                classes.append((col, title))
        if not classes:
            continue
        lessons = {name: {d: [] for d in DAY_NAMES.values()} for _, name in classes}
        current_day = None
        for row in range(2, min(ws.max_row, 1000) + 1):
            maybe_day = re.sub(r'\s+', ' ', cell(row, 1)).strip().upper().replace('’', "'")
            for candidate, pretty in DAY_NAMES.items():
                if candidate.replace('’', "'") == maybe_day or candidate.replace('’', "'") in maybe_day and len(maybe_day) < 22:
                    current_day = pretty
                    break
            raw_num = cell(row, 2)
            try:
                number = int(float(raw_num.replace(',', '.')))
            except (ValueError, TypeError):
                continue
            if not (1 <= number <= 15) or not current_day:
                continue
            raw_time = cell(row, 3)
            match = re.search(r'(\d{1,2})[.:](\d{2})\s*[-–—]\s*(\d{1,2})[.:](\d{2})', raw_time)
            period = f'{int(match[1]):02}:{match[2]}–{int(match[3]):02}:{match[4]}' if match else raw_time
            for col, title in classes:
                subject = cell(row, col)
                if subject:
                    lessons[title][current_day].append({'number': number, 'time': period, 'subject': subject})
        if any(any(v for v in d.values()) for d in lessons.values()):
            output[ws.title] = {'classes': lessons, 'days': ['Понеділок', 'Вівторок', 'Середа', 'Четвер', 'П’ятниця'],
                                'mergeCount': len(ws.merged_cells.ranges)}
    if not output:
        raise ValueError('Не знайдено розклад у таблиці')
    return output


def refresh(force=False):
    now = time.time()
    with LOCK:
        if not force and STATE['payload'] and now - STATE['checked'] < CACHE_SECONDS:
            return STATE['payload']
        if not force and STATE['error'] and now - STATE['checked'] < 90:
            return STATE['payload']
        STATE['checked'] = now
        try:
            req = Request(EXPORT, headers={'User-Agent': 'Mozilla/5.0 school-schedule/1.0', 'Cache-Control': 'no-cache'})
            with urlopen(req, timeout=25) as response:
                data = response.read(12 * 1024 * 1024 + 1)
            if len(data) > 12 * 1024 * 1024 or not data.startswith(b'PK'):
                raise ValueError('Google не надав XLSX (перевірте доступ на завантаження)')
            parsed = parse_workbook(data)
            STATE['payload'] = {'sheets': parsed, 'updatedAt': datetime.now(timezone.utc).isoformat(),
                                'source': 'google', 'warning': None}
            STATE['error'] = None
        except Exception as exc:
            STATE['error'] = str(exc)
            if STATE['payload']:
                STATE['payload']['warning'] = 'Не вдалося оновити Google: ' + str(exc) + '. Показані останні успішні дані.'
            elif (BASE / 'sample.xlsx').exists():
                try:
                    STATE['payload'] = {'sheets': parse_workbook((BASE / 'sample.xlsx').read_bytes()),
                                        'updatedAt': None, 'source': 'sample',
                                        'warning': 'Це ЗРАЗОК зі старого Excel. Google не завантажився: ' + str(exc)}
                except Exception:
                    pass
        return STATE['payload']


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        path = self.path.split('?')[0]
        if path == '/api/schedule':
            data = refresh(force=('refresh=1' in self.path))
            if data is None:
                self.reply(503, json.dumps({'error': STATE['error'] or 'Немає даних'}, ensure_ascii=False), 'application/json')
            else:
                self.reply(200, json.dumps(data, ensure_ascii=False), 'application/json')
        elif path in ('/', '/index.html'):
            self.reply(200, (BASE / 'index.html').read_bytes(), 'text/html; charset=utf-8')
        else:
            self.reply(404, b'Not found', 'text/plain; charset=utf-8')

    def reply(self, status, data, content_type):
        if isinstance(data, str):
            data = data.encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Cache-Control', 'no-store')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)


if __name__ == '__main__':
    port = int(os.environ.get('PORT', '8000'))
    print('Відкрийте http://localhost:%s' % port, flush=True)
    ThreadingHTTPServer(('0.0.0.0', port), Handler).serve_forever()
