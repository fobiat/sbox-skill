#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : release verification
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Everything check_skill.py does not: that both deliverables are complete, that
#  no local path or private project name leaked in, that every document naming
#  the toolset names all of it, and that the licence carries the upstream notice.
#  Run before tagging or before making the repo public.
#
#  check_skill.py is the per-commit gate. This is the pre-release one.
#
#  Two severities. A plain check is always fatal. A release check is fatal only
#  when a release is actually being cut, because "there are unreleased changes"
#  is the normal state of the repository between tags. Pass --allow-unreleased
#  on ordinary pushes; leave it off before tagging.
# =============================================================================

import argparse
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKILL = ROOT / "skills" / "sbox"
MCP_FILES = (ROOT / "editor-mcp" / "SboxMcpServer.cs", ROOT / "editor-mcp" / "SboxMcpServer.Editor.cs")
PLUGIN_DIR = ROOT / ".claude-plugin"
LIBRARY = ROOT / "library"
SELF = "scripts/verify_release.py"

# The toolset at the last release. Additions are reported; a name that
# disappears breaks anyone who scripted against it. Docs are checked against the
# names parsed from the C#, never against this set.
BASELINE_TOOLS = {
    "project_find_type", "project_type_members", "project_find_member", "project_enum_values",
    "project_input_actions", "project_console_commands", "project_content_path",
    "project_content_search",
    "project_info", "project_compilers", "project_source_changes", "project_compile_errors",
    "project_assembly_freshness", "project_package_references",
    "project_reload_config", "project_reload_settings", "project_rebuild", "project_build",
}

BASELINE_READONLY = BASELINE_TOOLS - {
    "project_reload_config", "project_reload_settings", "project_rebuild", "project_build",
}

# Documents that present the toolset as a catalogue, so every tool has to appear
# in each. Prose naming one or two in passing is not, which is why QUICKSTART.md
# is absent; if it grows into a catalogue the scan below says so.
CATALOGUE_DOCS = ("README.md", "editor-mcp/README.md",
                  "skills/sbox/references/14_VERIFICATION.md")

# A changelog records the toolset at each past release, so it neither has to keep
# up with new tools nor to stop naming one since renamed away.
HISTORICAL_DOCS = ("CHANGELOG.md",)

# The Agent Skills spec caps the frontmatter description at 1024 characters and
# truncates silently past it.
DESCRIPTION_LIMIT = 1000
NAME_LIMIT = 64

REFLECTED = ("RebuildCompilers", "LoadMinimal", "UpdateCompiler",
             "GetCompileSettings", "ClearCache", "ChangeSummary",
             "lastCompilerHash", "ReadConfig")

COVERAGE = {
    "editor extensions": r"EditorTool", "services": r"Leaderboard",
    "persistence": r"FileSystem\.Data", "localization": r"Phrase\b",
    "avatars": r"ClothingContainer", "shaders": r"\.shader\b",
    "audio mixer": r"\bMixer\b", "packages": r"Package\.",
    "actiongraph": r"ActionGraphNode", "vr": r"VRController", "voice": r"VoiceComponent",
}

# Regexes, so a Steam install path passes while this machine's own user and
# project directories do not. A blanket "C:\" banned documenting Windows at all.
LEAKS = (
    r"applejack",
    r"/home/[a-z]",
    r"C:\\Users\\Shadow",
    r"Desktop[/\\]Projects",
    r"worktrees?[/\\]",
)
LEAK_PATTERNS = [(text, re.compile(text, re.IGNORECASE)) for text in LEAKS]

BINARY_SUFFIXES = (".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".dll", ".vpk",
                   ".mp4", ".webm")

failures = []
notes = []
release_mode = True


def check(ok, label, detail=""):
    print(f"  {'PASS' if ok else 'FAIL'}  {label}{(' : ' + detail) if detail else ''}")
    if not ok:
        failures.append(label)


def release_check(ok, label, detail=""):
    if ok or release_mode:
        check(ok, label, detail)
        return
    print(f"  NOTE  {label}{(' : ' + detail) if detail else ''}")
    notes.append(label)


def section(name):
    print(f"\n{'=' * 58}\n{name}\n{'=' * 58}")


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True,
                          text=True, encoding="utf-8", errors="replace")


def tracked_files():
    result = git("ls-files", "-z")
    if result.returncode != 0:
        return None, (result.stderr.strip() or f"git exited {result.returncode}")
    return [name for name in result.stdout.split("\0") if name], ""


def changelog_sections(text):
    heads = list(re.finditer(r"^## \[([^\]]+)\]", text, re.M))
    sections = []
    for index, head in enumerate(heads):
        end = heads[index + 1].start() if index + 1 < len(heads) else len(text)
        sections.append((head.group(1), text[head.end():end].strip()))
    return sections


def frontmatter_field(frontmatter, key):
    match = re.search(rf"^{key}:[ \t]*(.*(?:\n[ \t]+\S.*)*)", frontmatter, re.M)
    return " ".join(match.group(1).split()) if match else ""


def check_toolset():
    section("MCP TOOLSET")
    src = "\n".join(f.read_text(encoding="utf-8") for f in MCP_FILES)

    readonly = set(re.findall(r'\[McpTool\.ReadOnly\(\s*"([a-z0-9_]+)"', src))
    mutating = set(re.findall(r'\[McpTool\(\s*"([a-z0-9_]+)"', src))
    declared = readonly | mutating

    check(bool(declared), "tools declared", f"{len(declared)} found "
          f"({len(readonly)} read-only, {len(mutating)} mutating)")
    check(not (readonly & mutating), "no tool declared both ways",
          ", ".join(sorted(readonly & mutating)) or "none")

    missing = sorted(BASELINE_TOOLS - declared)
    check(not missing, "every baseline tool still declared",
          f"gone: {', '.join(missing)}" if missing else f"all {len(BASELINE_TOOLS)}")
    added = sorted(declared - BASELINE_TOOLS)
    if added:
        print(f"        new since the baseline, update BASELINE_TOOLS at the next tag: {added}")

    lost = sorted(BASELINE_READONLY - readonly)
    check(not lost, "baseline read-only hints intact",
          f"no longer read-only: {', '.join(lost)}" if lost else f"all {len(BASELINE_READONLY)}")

    for member in REFLECTED:
        check(member in src, f"reflects {member}")
    check(src.count("{") == src.count("}"), "braces balanced")
    check(src.count("(") == src.count(")"), "parens balanced")
    check("kyle@fobiat.dev" in src, "author header")
    return declared


def check_skill_tree():
    section("SKILL")
    refs = sorted((SKILL / "references").glob("*.md"))
    check(len(refs) == 17, "17 reference files", f"{len(refs)} found")

    router = (SKILL / "SKILL.md").read_text(encoding="utf-8")
    check(router.startswith("---\n"), "frontmatter first")
    check("name: sbox" in router and "description:" in router, "frontmatter complete")

    end = router.find("\n---\n", 4)
    frontmatter = router[4:end] if end != -1 else ""
    name = frontmatter_field(frontmatter, "name")
    description = frontmatter_field(frontmatter, "description")
    check(0 < len(name) <= NAME_LIMIT, f"frontmatter name within {NAME_LIMIT} chars",
          f"{len(name)} chars")
    check(0 < len(description) <= DESCRIPTION_LIMIT,
          f"frontmatter description within {DESCRIPTION_LIMIT} chars", f"{len(description)} chars")

    routed = set(re.findall(r"references/([0-9A-Z_]+\.md)", router))
    check(routed == {p.name for p in refs}, "every reference routed", f"{len(routed)} pointers")

    files = [SKILL / "SKILL.md"] + refs
    check(all("kyle@fobiat.dev" in p.read_text(encoding="utf-8") for p in files),
          f"author header on all {len(files)} files")

    blob = "\n".join(p.read_text(encoding="utf-8") for p in files)
    ledger = sorted(set(re.findall(r"\bFN-(\d)\b", blob)))
    check(ledger == list("1234567"), "ledger FN-1 to FN-7 contiguous", ",".join(ledger))
    check(not re.search(r"\bA-\d\d\b", blob), "no stale ledger ids")

    for topic, pattern in COVERAGE.items():
        check(bool(re.search(pattern, blob)), f"covers {topic}")


def check_repo():
    section("REPO")
    for name in ("README.md", "LICENSE", "CONTRIBUTING.md", "CHANGELOG.md",
                 ".gitignore", ".gitattributes"):
        check((ROOT / name).is_file(), f"{name} present")
    check((ROOT / ".github" / "workflows" / "ci.yml").is_file(), "CI workflow")
    templates = list((ROOT / ".github" / "ISSUE_TEMPLATE").glob("*"))
    check(len(templates) >= 3, "issue templates present", f"{len(templates)} found")

    licence = (ROOT / "LICENSE").read_text(encoding="utf-8")
    check("Copyright (c) 2025 Facepunch Studios Ltd" in licence, "upstream MIT notice carried")
    check("fobiat (Kyle Tarff)" in licence, "author copyright")

    gate = subprocess.run([sys.executable, str(ROOT / "scripts" / "check_skill.py")],
                          capture_output=True, text=True, encoding="utf-8")
    check(gate.returncode == 0, "check_skill.py passes")
    if gate.returncode != 0:
        for line in (gate.stdout + gate.stderr).splitlines():
            print(f"        {line}")


def check_distribution(plugin):
    section("DISTRIBUTION")
    market = json.loads((PLUGIN_DIR / "marketplace.json").read_text(encoding="utf-8"))
    entries = market["plugins"]
    check(len(entries) == 1, "one plugin entry", f"{len(entries)} found")

    entry = entries[0]
    check(entry["name"] == plugin["name"], "plugin name agrees", plugin["name"])
    check(entry["version"] == plugin["version"], "plugin version agrees", plugin["version"])

    # source is relative to the plugin root, so it has to reach the skills dir
    check((PLUGIN_DIR.parent / entry["source"] / "skills" / "sbox" / "SKILL.md").is_file(),
          "plugin source resolves to the skill")

    sbproj = json.loads((LIBRARY / "sbox_mcp_server.sbproj").read_text(encoding="utf-8"))
    check(sbproj["Type"] == "library", "sbproj is a library", sbproj["Type"])
    check(sbproj["Ident"] == "sbox_mcp_server", "sbproj ident", sbproj["Ident"])
    check(sbproj["Org"] == "fobiat", "sbproj org", sbproj["Org"])

    # Title is the display name on asset.party, so the ident is the wrong thing to see there
    check(sbproj["Title"] != sbproj["Ident"], "sbproj title is a display name", sbproj["Title"])

    # the library ships a copy of each, and a drifted copy is a silently wrong package
    for source in MCP_FILES:
        target = LIBRARY / "Editor" / source.name
        check(target.is_file() and target.read_bytes() == source.read_bytes(),
              f"library copy of {source.name} identical to editor-mcp")


def check_changelog(plugin):
    section("CHANGELOG AND TAG")
    text = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    sections = changelog_sections(text)
    check(bool(sections), "changelog has version sections", f"{len(sections)} found")

    unreleased = [body for name, body in sections if name.strip().lower() == "unreleased"]
    pending = "\n".join(unreleased).strip()
    release_check(not pending, "changelog Unreleased section is empty",
                  f"{len(pending.splitlines())} lines pending" if pending else "empty")

    released = [name for name, _ in sections if re.fullmatch(r"\d+(?:\.\d+)*", name.strip())]
    check(bool(released), "changelog has a released version", released[0] if released else "none")
    check(bool(released) and released[0] == plugin["version"],
          "plugin version matches the latest released changelog entry",
          f"changelog {released[0] if released else 'none'}, plugin {plugin['version']}")

    described = git("describe", "--tags", "--dirty", "--always")
    if described.returncode != 0:
        release_check(False, "git describe --tags usable",
                      described.stderr.strip() or f"git exited {described.returncode}")
        return

    text = described.stdout.strip()
    match = re.match(r"^v?(\d+(?:\.\d+)*)(?:-(\d+)-g[0-9a-f]+)?(-dirty)?$", text)
    if not match:
        release_check(False, "repository has a version tag to compare against", text or "none")
        return

    tag, ahead, dirty = match.group(1), int(match.group(2) or 0), bool(match.group(3))
    moved = ahead > 0 or dirty
    state = f"{tag}, {ahead} commit(s) ahead{', dirty' if dirty else ''}"
    release_check(not moved or plugin["version"] != tag,
                  "content since the last tag carries a version bump", state)


def check_tracked(declared):
    section("TRACKED FILES")
    tracked, error = tracked_files()
    check(tracked is not None, "git ls-files ran", error or "ok")
    if tracked is None:
        check(False, "tracked files enumerated", "nothing examined, every check below is void")
        return
    check(bool(tracked), "git tracks files", f"{len(tracked)} tracked")
    if not tracked:
        return

    generated = [f for f in tracked
                 if f.endswith((".slnx", ".editor.csproj"))
                 or re.search(r"(^|/)\.(sbox|vscode)/", f)
                 or f.endswith("Properties/launchSettings.json")]
    check(not generated, "no editor-generated file tracked", ", ".join(generated) or "none")

    bodies = {}
    unreadable = []
    for name in tracked:
        if name.lower().endswith(BINARY_SUFFIXES):
            continue
        try:
            bodies[name] = (ROOT / name).read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as problem:
            unreadable.append(f"{name} ({type(problem).__name__})")
    check(not unreadable, "every tracked text file reads as utf-8",
          ", ".join(unreadable) or f"{len(bodies)} read")

    leaked = []
    for name, body in bodies.items():
        if name == SELF:
            continue
        leaked += [f"{name} ({pattern})" for pattern, regex in LEAK_PATTERNS if regex.search(body)]
    check(not leaked, "no local path leaked repo-wide", ", ".join(leaked) or "none")

    check_tool_names(declared, bodies)


def check_tool_names(declared, bodies):
    section("TOOL NAMES IN THE DOCS")
    if not declared:
        check(False, "tool names parsed from the C#", "nothing to compare the docs against")
        return

    for rel in CATALOGUE_DOCS:
        body = bodies.get(rel)
        if body is None:
            check(False, f"{rel} tracked and readable")
            continue
        absent = sorted(tool for tool in declared if tool not in body)
        check(not absent, f"{rel} names every tool",
              ", ".join(absent) or f"all {len(declared)} present")

    # Prefixes come from the declared names, so a tool that does not follow the
    # project_ convention is still covered.
    prefixes = sorted({tool.split("_")[0] for tool in declared})
    token = re.compile(r"\b(?:" + "|".join(map(re.escape, prefixes)) + r")_[a-z0-9_]+\b")

    stale = []
    catalogues = []
    for name, body in bodies.items():
        if name == SELF or name in HISTORICAL_DOCS:
            continue
        unknown = sorted(set(token.findall(body)) - declared)
        if unknown:
            stale.append(f"{name} ({', '.join(unknown)})")
        if name.endswith(".md") and name not in CATALOGUE_DOCS:
            named = sum(1 for tool in declared if tool in body)
            if named * 2 > len(declared) and named < len(declared):
                catalogues.append(f"{name} ({named}/{len(declared)})")

    check(not stale, "no document names a tool that does not exist", ", ".join(stale) or "none")
    check(not catalogues, "no unregistered catalogue is missing tools",
          ", ".join(catalogues) or "none")


def main():
    global release_mode
    parser = argparse.ArgumentParser(description="Pre-release gate for the s&box skill repo.")
    parser.add_argument("--allow-unreleased", action="store_true",
                        help="downgrade release-readiness checks to notes, for ordinary pushes")
    release_mode = not parser.parse_args().allow_unreleased

    plugin = json.loads((PLUGIN_DIR / "plugin.json").read_text(encoding="utf-8"))

    declared = check_toolset()
    check_skill_tree()
    check_repo()
    check_distribution(plugin)
    check_changelog(plugin)
    check_tracked(declared)

    print()
    if failures:
        print(f"{len(failures)} FAILURES")
        for name in failures:
            print(f"  - {name}")
        return 1
    if notes:
        print(f"ALL CHECKS PASSED, {len(notes)} release-readiness note(s) deferred")
        for name in notes:
            print(f"  - {name}")
        return 0
    print("ALL CHECKS PASSED, ready to tag")
    return 0


if __name__ == "__main__":
    sys.exit(main())
