#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : library sync
#
#  Author   : Kyle (fobiat) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  editor-mcp/SboxDevTools.cs is canonical. library/ ships a byte-identical copy
#  because an s&box library has to contain its own Editor/ folder and cannot
#  reference a file outside the project root. Run this after editing the
#  canonical file; verify_release.py fails if the two ever disagree.
# =============================================================================

import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "editor-mcp" / "SboxDevTools.cs"
TARGET = ROOT / "library" / "Editor" / "SboxDevTools.cs"


def main():
    if not SOURCE.is_file():
        print(f"missing canonical source: {SOURCE}")
        return 1

    if TARGET.is_file() and TARGET.read_bytes() == SOURCE.read_bytes():
        print("already in sync")
        return 0

    TARGET.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(SOURCE, TARGET)
    print(f"synced {SOURCE.name} to {TARGET.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
