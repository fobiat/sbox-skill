#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : release verification
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Everything check_skill.py does not: that both deliverables are complete, that
#  no local path or private project name leaked in, and that the licence carries
#  the upstream notice. Run before tagging or before making the repo public.
#
#  check_skill.py is the per-commit gate. This is the pre-release one.
# =============================================================================

import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKILL = ROOT / "skills" / "sbox"
MCP = ROOT / "editor-mcp" / "SboxMcpServer.cs"
PLUGIN_DIR = ROOT / ".claude-plugin"
LIBRARY = ROOT / "library"

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
    check("fobiat (Kyle Tarff)" in licence, "author copyright")

    gate = subprocess.run([sys.executable, str(ROOT / "scripts" / "check_skill.py")],
                          capture_output=True, text=True)
    check(gate.returncode == 0, "check_skill.py passes")

    section("DISTRIBUTION")
    market = json.loads((PLUGIN_DIR / "marketplace.json").read_text(encoding="utf-8"))
    plugin = json.loads((PLUGIN_DIR / "plugin.json").read_text(encoding="utf-8"))
    entries = market["plugins"]
    check(len(entries) == 1, "one plugin entry", f"{len(entries)} found")

    entry = entries[0]
    check(entry["name"] == plugin["name"], "plugin name agrees", plugin["name"])
    check(entry["version"] == plugin["version"], "plugin version agrees", plugin["version"])

    # source is relative to the plugin root, so it has to reach the skills dir
    check((PLUGIN_DIR.parent / entry["source"] / "skills" / "sbox" / "SKILL.md").is_file(),
          "plugin source resolves to the skill")

    latest = re.search(r"^## \[([0-9.]+)\]", (ROOT / "CHANGELOG.md").read_text(encoding="utf-8"),
                       re.M)
    check(latest is not None and latest.group(1) == plugin["version"],
          "plugin version matches changelog", latest.group(1) if latest else "none")

    sbproj = json.loads((LIBRARY / "sbox_mcp_server.sbproj").read_text(encoding="utf-8"))
    check(sbproj["Type"] == "library", "sbproj is a library", sbproj["Type"])
    check(sbproj["Ident"] == "sbox_mcp_server", "sbproj ident", sbproj["Ident"])

    # the library ships a copy, and a drifted copy is a silently wrong package
    check((LIBRARY / "Editor" / "SboxMcpServer.cs").read_bytes() == MCP.read_bytes(),
          "library copy identical to editor-mcp")

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
