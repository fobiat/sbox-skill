#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : library sync
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  editor-mcp/SboxMcpServer*.cs is canonical, two files (split at the 1000-line
#  mark most projects' own file-size gates enforce). library/ ships byte-identical
#  copies because an s&box library has to contain its own Editor/ folder and
#  cannot reference a file outside the project root. Run this after editing a
#  canonical file; verify_release.py fails if any pair ever disagrees.
# =============================================================================

import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "editor-mcp"
TARGET_DIR = ROOT / "library" / "Editor"
NAMES = ("SboxMcpServer.cs", "SboxMcpServer.Editor.cs")


def main():
    changed = False

    for name in NAMES:
        source = SOURCE_DIR / name
        target = TARGET_DIR / name

        if not source.is_file():
            print(f"missing canonical source: {source}")
            return 1

        if target.is_file() and target.read_bytes() == source.read_bytes():
            print(f"{name}: already in sync")
            continue

        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        print(f"synced {name} to {target.relative_to(ROOT)}")
        changed = True

    if not changed:
        print("all files already in sync")
    return 0


if __name__ == "__main__":
    sys.exit(main())
