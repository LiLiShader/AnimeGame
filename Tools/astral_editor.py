"""Explicit local editor command client (no network, no UI automation)."""
import sys,time
from pathlib import Path
root=Path(__file__).resolve().parents[1]
command=' '.join(sys.argv[1:])
request=root/'Temp/astral-command.txt'; result=root/'Temp/astral-command-result.txt'
start=time.time()
while request.exists():
 if time.time()-start>30:raise SystemExit('Editor still has a pending request')
 time.sleep(.15)
previous=result.stat().st_mtime_ns if result.exists() else 0
request.write_text(command)
while time.time()-start<45:
 if result.exists() and result.stat().st_mtime_ns!=previous:
  text=result.read_text();print(text);raise SystemExit(0 if text.startswith('OK') else 1)
 time.sleep(.2)
raise SystemExit('Editor did not respond; focus the editor to refresh assets')
