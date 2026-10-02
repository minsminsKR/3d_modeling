"""Regression tests of the validator itself, using deliberately corrupt mini-projects."""
import json
from pathlib import Path
import tempfile
import unittest

import validate_project as quality


class FixtureTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.assets = self.root / "Assets"
        self.assets.mkdir()

    def tearDown(self):
        self.temporary.cleanup()

    def asset(self, name="Example.cs", data="public class Example {}", guid="a" * 32):
        path = self.assets / name
        path.write_text(data)
        Path(str(path) + ".meta").write_text(f"fileFormatVersion: 2\nguid: {guid}\n")
        return path

    def test_valid_asset_is_indexed(self):
        self.asset()
        issues, guids = quality.metadata_issues(self.assets)
        self.assertEqual(issues, [])
        self.assertEqual(guids["a" * 32], "Assets/Example.cs")

    def test_missing_script_metadata_fails(self):
        (self.assets / "New.cs").write_text("class New {}")
        self.assertTrue(any("Missing metadata" in item for item in quality.metadata_issues(self.assets)[0]))

    def test_missing_folder_metadata_fails(self):
        (self.assets / "NewFolder").mkdir()
        self.assertTrue(any("Missing metadata" in item for item in quality.metadata_issues(self.assets)[0]))

    def test_duplicate_guid_fails(self):
        self.asset("First.cs"); self.asset("Second.cs")
        self.assertTrue(any("Duplicate GUID" in item for item in quality.metadata_issues(self.assets)[0]))

    def test_invalid_guid_fails(self):
        self.asset(guid="not-a-guid")
        self.assertTrue(any("exactly one" in item for item in quality.metadata_issues(self.assets)[0]))

    def test_multiple_guid_lines_fail(self):
        path = self.asset()
        Path(str(path) + ".meta").write_text("guid: " + "a" * 32 + "\nguid: " + "b" * 32 + "\n")
        self.assertTrue(quality.metadata_issues(self.assets)[0])

    def test_orphaned_metadata_fails(self):
        self.asset().unlink()
        self.assertTrue(any("Orphaned" in item for item in quality.metadata_issues(self.assets)[0]))

    def test_lfs_pointer_is_not_an_asset(self):
        self.asset("Model.fbx", quality.LFS_HEADER.decode() + "\noid sha256:abc\nsize 123\n")
        self.assertTrue(any("LFS pointer" in item for item in quality.metadata_issues(self.assets)[0]))

    def test_hidden_editor_files_do_not_require_metadata(self):
        (self.assets / ".DS_Store").write_bytes(b"temporary")
        self.assertEqual(quality.metadata_issues(self.assets)[0], [])

    def test_baseline_preserves_bytes(self):
        path = self.asset("Scene.unity", "authored content\n")
        baseline = {"protected_files": {"Assets/Scene.unity": quality.sha256(path)}}
        self.assertEqual(quality.preservation_issues(self.root, baseline), [])
        path.write_text("regenerated content\n")
        self.assertTrue(any("authored file changed" in item for item in quality.preservation_issues(self.root, baseline)))

    def test_missing_protected_file_fails(self):
        baseline = {"protected_files": {"Assets/Missing.unity": "0" * 64}}
        self.assertTrue(any("is missing" in item for item in quality.preservation_issues(self.root, baseline)))

    def test_contract_member_missing_fails(self):
        self.assertEqual(quality.contract_issues({"Shell": {"Begin"}}, {"Shell": ["Begin"]}), [])
        self.assertTrue(quality.contract_issues({"Shell": {"Begin"}}, {"Shell": ["Resume"]}))

    def test_contract_type_missing_fails(self):
        self.assertTrue(quality.contract_issues({}, {"Shell": []}))

    def manifest(self, source=None):
        self.asset("Audit.cs", source or '[RuntimeInitializeOnLoadMethod] class Audit { string flag="-v2-fixture-output"; string output="result.json"; void Stop() { Application.Quit(0); } }')
        return {"audits": [{"name": "fixture", "flag": "-v2-fixture-output", "source": "Assets/Audit.cs", "argument_kind": "directory", "result": "result.json", "status": "NOT RUN"}]}

    def test_manifest_matches_opt_in_contract(self):
        self.assertEqual(quality.manifest_issues(self.root, self.manifest()), [])

    def test_manifest_refuses_runtime_pass_claim(self):
        manifest = self.manifest(); manifest["audits"][0]["status"] = "PASS"
        self.assertTrue(any("cannot assert" in item for item in quality.manifest_issues(self.root, manifest)))

    def test_manifest_catches_missing_flag(self):
        manifest = self.manifest(); manifest["audits"][0]["flag"] = "-v2-typo-output"
        self.assertTrue(any("opt-in flag" in item for item in quality.manifest_issues(self.root, manifest)))

    def test_manifest_catches_wrong_output_filename(self):
        manifest = self.manifest(); manifest["audits"][0]["result"] = "wrong.json"
        self.assertTrue(any("filename" in item for item in quality.manifest_issues(self.root, manifest)))

    def test_manifest_duplicate_entry_fails(self):
        manifest = self.manifest(); manifest["audits"] *= 2
        self.assertTrue(any("Duplicate audit" in item for item in quality.manifest_issues(self.root, manifest)))


@unittest.skipUnless(quality.load_parser(), "tree-sitter not installed; source parsing tests NOT RUN")
class ParserTest(unittest.TestCase):
    def setUp(self):
        self.parser = quality.load_parser()

    def test_valid_csharp_and_public_members(self):
        errors, declarations = quality.parse_source(self.parser, b"class Shell { public int field; public bool Playing => true; public void Begin() {} }")
        self.assertEqual(errors, [])
        self.assertEqual(declarations["Shell"], {"field", "Playing", "Begin"})

    def test_syntax_error_detected(self):
        errors, _ = quality.parse_source(self.parser, b"class Broken { public void Begin( { }")
        self.assertTrue(errors)

    def test_comments_and_strings_cannot_satisfy_api(self):
        source = b'class Shell { /* public void Begin() {} */ string fake = "public bool Playing => true;"; }'
        errors, declarations = quality.parse_source(self.parser, source)
        self.assertEqual(errors, [])
        self.assertEqual(declarations["Shell"], set())

    def test_private_members_cannot_satisfy_public_api(self):
        _, declarations = quality.parse_source(self.parser, b"class Shell { private void Begin() {} public void Back() {} }")
        self.assertEqual(declarations["Shell"], {"Back"})

    def test_multiple_fields_and_events(self):
        _, declarations = quality.parse_source(self.parser, b"class Session { public int first, second; public event System.Action Changed; }")
        self.assertEqual(declarations["Session"], {"first", "second", "Changed"})

    def test_nested_type_member_does_not_leak(self):
        _, declarations = quality.parse_source(self.parser, b"class Session { class Inner { public void Begin() {} } }")
        self.assertNotIn("Begin", declarations["Session"])
        self.assertIn("Begin", declarations["Inner"])


if __name__ == "__main__":
    unittest.main()
