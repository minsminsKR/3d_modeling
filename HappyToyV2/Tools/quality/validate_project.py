#!/usr/bin/env python3
"""Read-only Unity source checks. This is not a C# compiler or a Unity test runner."""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import re
import sys

GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)
SCRIPT_REFERENCE = re.compile(r"m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}")
LFS_HEADER = b"version https://git-lfs.github.com/spec/v1"
DEFAULT_PROJECT = Path(__file__).resolve().parents[2]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def metadata_issues(assets: Path) -> tuple[list[str], dict[str, str]]:
    issues, paths_by_guid = [], defaultdict(list)
    for path in sorted(assets.rglob("*")):
        if any(part.startswith(".") for part in path.relative_to(assets).parts):
            continue
        if path.suffix != ".meta":
            if not Path(str(path) + ".meta").is_file():
                issues.append(f"Missing metadata: {path.relative_to(assets.parent)}")
            if path.is_file():
                with path.open("rb") as stream:
                    if stream.read(len(LFS_HEADER)) == LFS_HEADER:
                        issues.append(f"Unmaterialized Git LFS pointer: {path.relative_to(assets.parent)}")
            continue
        target = Path(str(path)[:-5])
        if not target.exists():
            issues.append(f"Orphaned metadata: {path.relative_to(assets.parent)}")
        matches = GUID.findall(path.read_text(encoding="utf-8-sig"))
        if len(matches) != 1:
            issues.append(f"Expected exactly one lowercase 32-character GUID: {path.relative_to(assets.parent)}")
        else:
            paths_by_guid[matches[0]].append(str(target.relative_to(assets.parent)))
    for guid, paths in paths_by_guid.items():
        if len(paths) > 1:
            issues.append(f"Duplicate GUID {guid}: {', '.join(paths)}")
    return issues, {guid: paths[0] for guid, paths in paths_by_guid.items()}


def preservation_issues(project: Path, baseline: dict) -> list[str]:
    issues = []
    for relative, expected in baseline["protected_files"].items():
        path = project / relative
        if not path.is_file():
            issues.append(f"Protected file is missing: {relative}")
        elif sha256(path) != expected:
            issues.append(f"Protected authored file changed: {relative}. Review it; do not regenerate the baseline to silence this check.")
    return issues


def load_parser():
    try:
        from tree_sitter import Language, Parser
        import tree_sitter_c_sharp
    except ImportError:
        return None
    return Parser(Language(tree_sitter_c_sharp.language()))


def walk_nodes(root):
    stack = [root]
    while stack:
        node = stack.pop()
        yield node
        stack.extend(reversed(node.children))


def parse_source(parser, source: bytes) -> tuple[list[str], dict[str, set[str]]]:
    """Extract declared public API names from an AST, excluding comments/string lookalikes."""
    root = parser.parse(source).root_node
    errors, declarations = [], defaultdict(set)
    for node in walk_nodes(root):
        if node.type == "ERROR" or node.is_missing:
            errors.append(f"{node.type} at {node.start_point.row + 1}:{node.start_point.column + 1}")
        if node.type not in {"class_declaration", "struct_declaration"}:
            continue
        name, body = node.child_by_field_name("name"), node.child_by_field_name("body")
        if name is None or body is None:
            continue
        type_name = name.text.decode("utf-8")
        declarations[type_name]  # Also index types with no public members.
        for member in body.named_children:
            if not any(child.type == "modifier" and child.text == b"public" for child in member.children):
                continue
            member_name = member.child_by_field_name("name")
            if member.type in {"method_declaration", "property_declaration", "event_declaration"} and member_name:
                declarations[type_name].add(member_name.text.decode("utf-8"))
            if member.type in {"field_declaration", "event_field_declaration"}:
                for child in walk_nodes(member):
                    if child.type == "variable_declarator":
                        field = child.child_by_field_name("name")
                        if field:
                            declarations[type_name].add(field.text.decode("utf-8"))
    return errors, dict(declarations)


def contract_issues(declarations: dict[str, set[str]], contracts: dict[str, list[str]]) -> list[str]:
    issues = []
    for type_name, required in contracts.items():
        if type_name not in declarations:
            issues.append(f"Missing declared type: {type_name}")
            continue
        for name in required:
            if name not in declarations[type_name]:
                issues.append(f"Missing public source API: {type_name}.{name}")
    return issues


def manifest_issues(project: Path, manifest: dict) -> list[str]:
    issues, names, flags = [], set(), set()
    for audit in manifest["audits"]:
        name, flag = audit["name"], audit["flag"]
        if name in names or flag in flags:
            issues.append(f"Duplicate audit name or command-line flag: {name} / {flag}")
        names.add(name); flags.add(flag)
        source_path = project / audit["source"]
        if not source_path.is_file():
            issues.append(f"Audit source missing: {audit['source']}")
            continue
        source = source_path.read_text(encoding="utf-8-sig")
        if f'"{flag}"' not in source or "RuntimeInitializeOnLoadMethod" not in source:
            issues.append(f"Audit is not installed with its documented opt-in flag: {name}")
        if "Application.Quit(" not in source:
            issues.append(f"Audit has no explicit exit status: {name}")
        if audit["argument_kind"] not in {"file", "directory"}:
            issues.append(f"Invalid audit output argument kind: {name}")
        if audit["argument_kind"] == "directory" and f'"{audit["result"]}"' not in source:
            issues.append(f"Audit result filename not found in source: {name}")
        if audit["status"] != "NOT RUN":
            issues.append(f"Manifest cannot assert a runtime result: {name}")
    return issues


def validate(project: Path, require_parser: bool = False) -> dict:
    quality = project / "Tools/quality"
    baseline = json.loads((quality / "scene_baseline.json").read_text())
    manifest = json.loads((quality / "audit_manifest.json").read_text())
    errors, warnings, checks = [], [], []
    problems, guids = metadata_issues(project / "Assets")
    errors.extend(problems); checks.append({"name": "asset_metadata_and_lfs", "status": "FAIL" if problems else "PASS", "guid_count": len(guids)})
    problems = preservation_issues(project, baseline)
    errors.extend(problems); checks.append({"name": "authored_scene_preserved", "status": "FAIL" if problems else "PASS", "files": list(baseline["protected_files"])})
    scene_path = project / baseline["selected_scene"]
    if scene_path.is_file():
        scene = scene_path.read_text(encoding="utf-8-sig")
        references = Counter(SCRIPT_REFERENCE.findall(scene))
        expected_external = set(baseline["preexisting_external_script_guids"])
        unknown = set(references) - set(guids) - expected_external
        bad_type = [guids[g] for g in references if g in guids and not guids[g].endswith(".cs")]
        errors.extend(f"Unresolved scene script GUID: {g}" for g in sorted(unknown))
        errors.extend(f"Scene script GUID points to a non-script asset: {p}" for p in bad_type)
        checks.append({"name": "scene_script_reference_inventory", "status": "FAIL" if unknown or bad_type else "PASS", "component_count": sum(references.values()), "preexisting_external_guid_count": len(expected_external & set(references))})
        if expected_external:
            warnings.append("Four pre-existing package script GUIDs need Unity's PackageCache/AssetDatabase to resolve; the static inventory does not verify those package types.")
    else:
        errors.append("Selected scene is missing")
    parser = load_parser()
    scripts = sorted((project / "Assets").rglob("*.cs"))
    if parser:
        syntax_errors, filename_errors, declarations = [], [], defaultdict(set)
        for path in scripts:
            problems, declared = parse_source(parser, path.read_bytes())
            syntax_errors.extend(f"{path.relative_to(project)}: {problem}" for problem in problems)
            if path.stem not in declared:
                filename_errors.append(f"Script filename has no matching declared class/struct: {path.relative_to(project)}")
            for type_name, members in declared.items():
                declarations[type_name].update(members)
        errors.extend(syntax_errors); errors.extend(filename_errors)
        checks.append({"name": "script_file_type_names", "status": "FAIL" if filename_errors else "PASS", "scripts": len(scripts)})
        checks.append({"name": "csharp_syntax_tree", "status": "FAIL" if syntax_errors else "PASS", "scripts": len(scripts), "compiler": False})
        problems = contract_issues(declarations, manifest["public_api_names"])
        errors.extend(problems); checks.append({"name": "public_api_names", "status": "FAIL" if problems else "PASS", "types": len(manifest["public_api_names"]), "type_checked": False})
    else:
        message = "C# syntax/API-name checks were NOT RUN; install Tools/quality/requirements.txt."
        (errors if require_parser else warnings).append(message)
        checks.extend([{"name": "csharp_syntax_tree", "status": "NOT RUN"}, {"name": "public_api_names", "status": "NOT RUN"}])
    problems = manifest_issues(project, manifest)
    errors.extend(problems); checks.append({"name": "opt_in_audit_manifest", "status": "FAIL" if problems else "PASS", "audits": len(manifest["audits"])})
    return {"schema": 1, "scope": "Static source and asset checks only", "status": "FAIL" if errors else "PASS" if parser else "PASS_WITH_SKIPS", "checks": checks, "errors": errors, "warnings": warnings, "unity_compilation": "NOT RUN", "unity_runtime_audits": "NOT RUN", "rendered_visual_review": "NOT RUN"}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    ap.add_argument("--require-parser", action="store_true", help="Fail rather than skip C# syntax/API-name checks if dependencies are absent")
    ap.add_argument("--output", type=Path, help="Optionally write machine-readable results; does not change Assets")
    args = ap.parse_args(argv)
    report = validate(args.project.resolve(), args.require_parser)
    rendered = json.dumps(report, indent=2, ensure_ascii=False)
    print(rendered)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered + "\n", encoding="utf-8")
    return 1 if report["errors"] else 0


if __name__ == "__main__":
    sys.exit(main())
