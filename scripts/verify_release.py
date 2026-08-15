#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : release verification
#
#  Author   : Kyle (fobiat) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Everything check_skill.py does not: that both deliverables are complete, that
#  no local path or private project name leaked in, and that the licence carries
#  the upstream notice. Run before tagging or before making the repo public.
#
#  check_skill.py is the per-commit gate. This is the pre-release one.
# =============================================================================

import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKILL = ROOT / "skills" / "sbox"
MCP = ROOT / "editor-mcp" / "SboxDevTools.cs"

TOOLS = {
    "project_find_type", "project_type_members", "project_input_actions",
    "project_info", "project_compilers", "project_source_changes", "project_compile_errors",
    "project_reload_config", "project_reload_settings", "project_rebuild", "project_build",
}

REFLECTED = ("RebuildCompilers", "LoadMinimal", "UpdateCompiler", "CompileAsync",
             "GetCompileDiagnostics", "GetCompileSettings", "ClearCache", "ChangeSummary")

COVERAGE = {
    "editor extensions": r"EditorTool", "services": r"Leaderboard",
    "persistence": r"FileSystem\.Data", "localization": r"Phrase\b",
    "avatars": r"ClothingContainer", "shaders": r"\.shader\b",
    "audio mixer": r"\bMixer\b", "packages": r"Package\.",
    "actiongraph": r"ActionGraphNode", "vr": r"VRController", "voice": r"VoiceComponent",
}

LEAKS = ("applejack", "/home/", "C:\\", "worktree")

failures = []


def check(ok, label, detail=""):
    print(f"  {'PASS' if ok else 'FAIL'}  {label}{(' : ' + detail) if detail else ''}")
    if not ok:
        failures.append(label)


def section(name):
    print(f"\n{'=' * 58}\n{name}\n{'=' * 58}")


def main():
    section("MCP TOOLSET")
    src = MCP.read_text(encoding="utf-8")
    declared = set(re.findall(r'\[McpTool(?:\.ReadOnly)?\(\s*"([a-z_]+)"', src))
    check(declared == TOOLS, f"all {len(TOOLS)} tools declared", f"{len(declared)} found")
    if TOOLS - declared:
        print(f"        missing: {sorted(TOOLS - declared)}")
    check(len(re.findall(r'\[McpTool\.ReadOnly\(', src)) == 7, "7 read-only hints")
    for member in REFLECTED:
        check(member in src, f"reflects {member}")
    check(src.count("{") == src.count("}"), "braces balanced")
    check(src.count("(") == src.count(")"), "parens balanced")
    check("kyle@fobiat.dev" in src, "author header")

    section("SKILL")
    refs = sorted((SKILL / "references").glob("*.md"))
    check(len(refs) == 16, "16 reference files", f"{len(refs)} found")

    router = (SKILL / "SKILL.md").read_text(encoding="utf-8")
    check(router.startswith("---\n"), "frontmatter first")
    check("name: sbox" in router and "description:" in router, "frontmatter complete")

    routed = set(re.findall(r"references/([0-9A-Z_]+\.md)", router))
    check(routed == {p.name for p in refs}, "every reference routed", f"{len(routed)} pointers")

    files = [SKILL / "SKILL.md"] + refs
    check(all("kyle@fobiat.dev" in p.read_text(encoding="utf-8") for p in files),
          f"author header on all {len(files)} files")

    blob = "\n".join(p.read_text(encoding="utf-8") for p in files)
    for term in LEAKS:
        check(term.lower() not in blob.lower(), f"no leakage of {term!r}")

    ledger = sorted(set(re.findall(r"\bFN-(\d)\b", blob)))
    check(ledger == list("1234567"), "ledger FN-1 to FN-7 contiguous", ",".join(ledger))
    check(not re.search(r"\bA-\d\d\b", blob), "no stale ledger ids")

    for topic, pattern in COVERAGE.items():
        check(bool(re.search(pattern, blob)), f"covers {topic}")

    section("REPO")
    for name in ("README.md", "LICENSE", "CONTRIBUTING.md", "CHANGELOG.md", ".gitignore"):
        check((ROOT / name).is_file(), f"{name} present")
    check((ROOT / ".github" / "workflows" / "ci.yml").is_file(), "CI workflow")
    check(len(list((ROOT / ".github" / "ISSUE_TEMPLATE").glob("*"))) == 3, "3 issue templates")

    licence = (ROOT / "LICENSE").read_text(encoding="utf-8")
    check("Copyright (c) 2025 Facepunch Studios Ltd" in licence, "upstream MIT notice carried")
    check("Kyle (fobiat)" in licence, "author copyright")

    gate = subprocess.run([sys.executable, str(ROOT / "scripts" / "check_skill.py")],
                          capture_output=True, text=True)
    check(gate.returncode == 0, "check_skill.py passes")

    print()
    if failures:
        print(f"{len(failures)} FAILURES")
        for f in failures:
            print(f"  - {f}")
        return 1
    print("ALL CHECKS PASSED, ready to tag")
    return 0


if __name__ == "__main__":
    sys.exit(main())
