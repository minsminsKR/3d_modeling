"""Checks that focused verification cannot accept absent, stale or partial results."""
import importlib.util
from pathlib import Path
import re
import tempfile
import unittest

MODULE = Path(__file__).with_name("run_feature_tests.py")
SPEC = importlib.util.spec_from_file_location("feature_runner", MODULE)
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class FeatureRoutingTests(unittest.TestCase):
    def test_full_allows_the_natural_survival_suite_and_explicit_limits_still_win(self):
        for profile, expected in (("full", 7200), ("core", 1800)):
            for stage in runner.commands(profile, Path("Project"), Path("Output")):
                command = stage["command"]
                self.assertEqual(command[command.index("--timeout") + 1], str(expected))
            for stage in runner.commands(profile, Path("Project"), Path("Output"), timeout=43):
                command = stage["command"]
                self.assertEqual(command[command.index("--timeout") + 1], "43")

    def test_full_runs_both_modes_without_a_filter_or_installer(self):
        planned = runner.commands("full", Path("Project"), Path("Output"))
        self.assertEqual([row["mode"] for row in planned], ["EditMode", "PlayMode"])
        for row in planned:
            self.assertNotIn("--filter", row["command"])
            self.assertNotIn("--allow-install", row["command"])
            self.assertEqual(row["command"][row["command"].index("--editor-version") + 1], runner.VERSION)

    def test_each_feature_includes_rules_without_restarting_editmode_twice(self):
        for profile in runner.PROFILES:
            if profile == "full":
                continue
            stages = runner.stages(profile)
            self.assertEqual(sum(mode == "EditMode" for mode, _ in stages), 1)
            self.assertEqual(stages[0][0], "EditMode")
            self.assertTrue(set(runner.CORE_FIXTURES).issubset(stages[0][1]))

    def test_editor_graphics_override_and_logs_are_explicit_and_optional(self):
        defaults = runner.commands("core", Path("Project"), Path("Output"))[0]
        self.assertNotIn("-force-d3d11", defaults["command"])
        forced = runner.commands("core", Path("Project"), Path("Output"), graphics_api="d3d11")[0]
        separator = forced["command"].index("--")
        self.assertEqual(forced["command"][separator + 1:], forced["editorArguments"])
        self.assertIn("-force-d3d11", forced["editorArguments"])
        self.assertEqual(forced["editorArguments"][forced["editorArguments"].index("-logFile") + 1],
                         str(Path(forced["report"]).with_suffix(".editor.log")))

    def test_all_required_selectors_resolve_to_real_source_tests(self):
        declarations = {}
        for path in (runner.PROJECT / "Assets/Tests").rglob("*.cs"):
            text = path.read_text(encoding="utf-8-sig")
            namespace = re.search(r"namespace\s+([\w.]+)", text)
            if namespace:
                for fixture in re.findall(r"\bclass\s+(\w+)", text):
                    name = namespace.group(1) + "." + fixture
                    declarations[name] = declarations.get(name, "") + text
        for profile in runner.PROFILES:
            for _, selectors in runner.stages(profile):
                for selector in selectors:
                    if selector in declarations:
                        self.assertTrue(re.search(r"\[(?:Test|UnityTest|TestCase)(?:,|\]|\()", declarations[selector]), selector)
                    else:
                        fixture, method = selector.rsplit(".", 1)
                        self.assertTrue(fixture in declarations, selector)
                        self.assertTrue(re.search(r"\b" + re.escape(method) + r"\s*\(", declarations[fixture]), selector)

    def report(self, directory, text):
        path = Path(directory) / "results.xml"
        path.write_text(text, encoding="utf-8")
        return path

    def test_missing_empty_skipped_failed_and_missing_required_cases_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                runner.read_results(Path(directory) / "absent.xml", ())
            samples = (
                '<test-run result="Passed" />',
                '<test-run result="Passed"><test-case fullname="A.B" result="Skipped" /></test-run>',
                '<test-run result="Failed"><test-case fullname="A.B" result="Passed" /></test-run>',
                '<test-run result="Passed"><test-case fullname="Other.B" result="Passed" /></test-run>')
            for text in samples:
                with self.assertRaises(ValueError):
                    runner.read_results(self.report(directory, text), ("A",))

    def test_fixture_method_and_parameterized_selectors_require_completed_cases(self):
        with tempfile.TemporaryDirectory() as directory:
            report = self.report(directory, '<test-run result="Passed" duration="1.2">'
                                 '<test-case fullname="A.B.Method(1)" result="Passed" />'
                                 '</test-run>')
            result = runner.read_results(report, ("A.B", "A.B.Method"))
            self.assertEqual(result["passed"], 1)

    def test_failed_report_retains_counts_without_accepting_incomplete_selection(self):
        with tempfile.TemporaryDirectory() as directory:
            report = self.report(directory, '<test-run result="Failed" duration="2.5">'
                                 '<test-case fullname="A.Pass" result="Passed"><output>diagnostic</output></test-case>'
                                 '<test-case fullname="A.Fail" result="Failed"><failure><message>bad</message></failure></test-case>'
                                 '<test-case fullname="A.Skip" result="Skipped" /></test-run>')
            result = runner.inspect_results(report, ("A", "Missing"))
            self.assertEqual((result["total"], result["passed"], result["failed"], result["skipped"]), (3, 1, 1, 1))
            self.assertEqual(result["missing"], ["Missing"])
            self.assertEqual(result["incomplete"], ["A.Fail", "A.Skip"])
            self.assertTrue(result["errors"])
            with self.assertRaises(ValueError):
                runner.read_results(report, ("A",))


if __name__ == "__main__":
    unittest.main()
