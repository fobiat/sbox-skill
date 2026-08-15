#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : compile check driver
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Builds SboxMcpServer.cs twice, once with Nullable=enable and once with
#  Nullable=disable, both with warnings as errors. A drop-in file lands in
#  whatever project the reader already has, and the .sbproj "Nullables" field
#  that decides which of the two they get defaults to off.
#
#    python editor-mcp/compilecheck/run.py
#    python editor-mcp/compilecheck/run.py --sbox-managed "D:/Steam/.../bin/managed/"
#
#  SBOX_MANAGED in the environment does the same as --sbox-managed.
# =============================================================================

import argparse
import os
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = HERE / "compilecheck.csproj"
CONFIGURATIONS = ("enable", "disable")


def build(nullable, managed, extra):
    # Separate obj/ and bin/ per configuration, or the second build reuses the
    # first one's intermediate output and reports a pass it never earned.
    command = [
        "dotnet", "build", str(PROJECT),
        "--nologo",
        "-v", "minimal",
        f"-p:Nullable={nullable}",
        f"-p:BaseIntermediateOutputPath=obj/{nullable}/",
        f"-p:MSBuildProjectExtensionsPath={HERE.as_posix()}/obj/{nullable}/",
        f"-p:BaseOutputPath=bin/{nullable}/",
    ]
    if managed:
        command.append(f"-p:SboxManaged={managed}")
    command += extra

    print(f"\n{'=' * 58}\nNullable={nullable}\n{'=' * 58}", flush=True)
    return subprocess.run(command, cwd=HERE).returncode


def main():
    parser = argparse.ArgumentParser(description="Compile SboxMcpServer.cs in both nullable modes.")
    parser.add_argument("--sbox-managed", default=os.environ.get("SBOX_MANAGED", ""),
                        help="s&box bin/managed/ directory, defaults to $SBOX_MANAGED")
    args, extra = parser.parse_known_args()

    managed = args.sbox_managed.replace("\\", "/")
    if managed and not managed.endswith("/"):
        managed += "/"
    if not managed:
        print("SBOX_MANAGED is not set and --sbox-managed was not given.")
        return 1
    if not (Path(managed) / "Sandbox.Tools.dll").is_file():
        print(f"No Sandbox.Tools.dll under {managed}, that is not an s&box bin/managed/ directory.")
        return 1

    results = {name: build(name, managed, extra) for name in CONFIGURATIONS}

    print(f"\n{'=' * 58}")
    for name, code in results.items():
        print(f"  {'PASS' if code == 0 else 'FAIL'}  Nullable={name}")
    failed = [name for name, code in results.items() if code != 0]
    if failed:
        print(f"\ncompile check FAILED in {len(failed)} of {len(results)} configurations")
        return 1
    print("\ncompile check passed in both configurations")
    return 0


if __name__ == "__main__":
    sys.exit(main())
