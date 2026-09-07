#!/usr/bin/env python3
"""Independently acquire the exact K-J providers and SJ presentation releases."""
from pathlib import Path
import subprocess
import sys
ROOT = Path(__file__).resolve().parents[1]
raise SystemExit(subprocess.run([sys.executable, str(ROOT / 'scripts/fetch-apple-everest-stage25kj-inputs.py'), '--include-presentation', *sys.argv[1:]]).returncode)
