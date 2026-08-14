#!/usr/bin/env python3
"""Start one builder child command in its own process group.

The builder uses the group only to forward an interrupt to the command and all
of its descendants.  This wrapper does not change the command's environment,
working directory, output streams, or eventual exit status.
"""

from __future__ import annotations

import os
import sys


def main() -> int:
    if len(sys.argv) < 2:
        print("builder command launcher: no command supplied", file=sys.stderr)
        return 127

    try:
        os.setsid()
        os.execvp(sys.argv[1], sys.argv[1:])
    except OSError as exc:
        print(f"builder command launcher: {exc}", file=sys.stderr)
        return 127


if __name__ == "__main__":
    raise SystemExit(main())
