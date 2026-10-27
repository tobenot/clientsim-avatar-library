"""Local, standard-library-only client for an installed ClientSim Actors Editor.
No SSH, private machine names, Unity launch, Play or automatic asset installs.
"""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import sys
import time
import uuid

ACTIONS = ('list', 'status', 'settings', 'validate', 'select', 'disable', 'use-defaults', 'machine-set')


def exchange(project: Path, action: str, values: dict, timeout: float = 30,
             request_id: str | None = None) -> dict:
    project = project.resolve()
    mailbox = project / 'UserSettings' / 'LuobeiClientSimActors'
    marker = mailbox / 'editor.json'
    if not marker.is_file():
        raise RuntimeError('Plugin not loaded: open this project in Unity and finish script import. Missing ' + str(marker))
    editor = json.loads(marker.read_text(encoding='utf-8-sig'))
    if Path(editor['project']).resolve() != project:
        raise RuntimeError('Editor marker belongs to a different project')
    pid = int(editor['pid'])
    if os.name == 'nt':
        # Query only this PID. Opening a process handle is not needed.
        import ctypes
        from ctypes import wintypes
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel.OpenProcess.restype = wintypes.HANDLE
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        handle = kernel.OpenProcess(0x1000, False, pid)
        if not handle:
            raise RuntimeError('Editor marker is stale or inaccessible; open this project in Unity')
        kernel.CloseHandle(handle)
    rid = request_id or 'actor-' + uuid.uuid4().hex
    import re
    if not re.fullmatch(r'[a-z0-9-]{1,80}', rid):
        raise ValueError('Request ID must match [a-z0-9-]{1,80}')
    receipt = mailbox / rid / 'result.json'
    if receipt.is_file():
        return json.loads(receipt.read_text(encoding='utf-8-sig'))
    started = mailbox / rid / 'started.json'
    request = mailbox / 'request.json'
    if not started.is_file():
        if request.is_file():
            previous = json.loads(request.read_text(encoding='utf-8-sig'))
            old = previous.get('id', '')
            if old and old != rid and not (mailbox / old / 'result.json').is_file():
                raise RuntimeError('Mailbox busy with request ' + old + '; read its receipt first')
        payload = dict(id=rid, project=editor['project'], pid=pid, action=action, **values)
        partial = mailbox / ('request-' + rid + '.partial')
        partial.write_text(json.dumps(payload, ensure_ascii=False), encoding='utf-8')
        partial.replace(request)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if receipt.is_file():
            try:
                return json.loads(receipt.read_text(encoding='utf-8-sig'))
            except json.JSONDecodeError:
                pass  # Editor may still be finishing its small receipt write.
        time.sleep(.1)
    return dict(ok=False, pending=True, id=rid, receipt=str(receipt),
                error='Do not resubmit with a new ID. Read receipt or repeat with --request-id ' + rid)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=ACTIONS)
    parser.add_argument('--project', type=Path, default=Path.cwd(), help='Unity project; defaults to current directory')
    parser.add_argument('--actor')
    parser.add_argument('--library', help='Only needed once for machine-set, or a deliberate project override')
    parser.add_argument('--robot', action='store_true', help='machine-set: disable external actor as machine default')
    parser.add_argument('--timeout', type=float, default=30)
    parser.add_argument('--request-id', help='Resume an uncertain request; never change ID to retry a write')
    args = parser.parse_args(argv)
    if args.action in ('select', 'machine-set') and not args.actor and not args.robot:
        parser.error(args.action + ' requires --actor')
    if args.action == 'machine-set' and not args.library:
        parser.error('machine-set requires --library (configured once per OS user)')
    if args.robot and args.action != 'machine-set':
        parser.error('--robot is only for machine-set; use disable for this project')
    values = {}
    if args.actor:
        values['actorId'] = args.actor
    if args.library:
        values['libraryRoot'] = args.library.replace('\\', '/')
    if args.action == 'machine-set':
        values['enabled'] = not args.robot
    try:
        result = exchange(args.project, args.action, values, args.timeout, args.request_id)
    except (OSError, ValueError, RuntimeError, KeyError) as exc:
        result = dict(ok=False, error=str(exc))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result.get('ok') else 2 if result.get('pending') else 1


if __name__ == '__main__':
    sys.exit(main())
