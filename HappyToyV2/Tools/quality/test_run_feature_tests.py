"""Checks that focused verification cannot accept absent, stale or partial results."""
import importlib.util
import contextlib
import io
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest
from unittest.mock import patch

MODULE = Path(__file__).with_name("run_feature_tests.py")
SPEC = importlib.util.spec_from_file_location("feature_runner", MODULE)
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class FeatureRoutingTests(unittest.TestCase):
    def test_every_profile_defaults_to_batch_input_and_cursor_isolation(self):
        for profile in runner.PROFILES:
            for stage in runner.commands(profile, Path("Project"), Path("Fresh")):
                arguments = stage["editorArguments"]
                self.assertIn("-batchmode", arguments, profile)
                self.assertIn("-v2-audit-no-desktop-interaction", arguments, profile)
                self.assertEqual(arguments[arguments.index("-job-worker-count") + 1], "2")
        core = runner.commands("core", Path("Project"), Path("Fresh"))[0]
        self.assertIn("-nographics", core["editorArguments"])
        self.assertIn("-noaudio", core["editorArguments"])

    def test_cli_keeps_isolation_when_the_user_omits_the_old_optional_flag(self):
        output = io.StringIO()
        with patch.object(sys, "argv", [str(MODULE), "--profile", "core", "--dry-run"]), \
                patch.object(runner.subprocess, "run") as start_process, \
                contextlib.redirect_stdout(output):
            self.assertEqual(runner.main(), 0)
        start_process.assert_not_called()
        self.assertTrue(json.loads(output.getvalue())["noDesktopInteractionRequested"])

    @unittest.skipUnless(sys.platform == "win32", "Windows process startup policy")
    def test_windows_children_are_hidden_without_a_console_and_below_normal(self):
        options = runner.desktop_process_options()
        self.assertTrue(options["startupinfo"].dwFlags & runner.subprocess.STARTF_USESHOWWINDOW)
        self.assertEqual(options["startupinfo"].wShowWindow, runner.subprocess.SW_HIDE)
        for flag in (runner.subprocess.CREATE_NO_WINDOW, runner.subprocess.BELOW_NORMAL_PRIORITY_CLASS):
            self.assertEqual(options["creationflags"] & flag, flag)

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


class SelectedRecheckTests(unittest.TestCase):
    def setUp(self):
        spec=importlib.util.spec_from_file_location("selected_retry",MODULE.with_name("retry_failed_tests.py"))
        self.retry=importlib.util.module_from_spec(spec);spec.loader.exec_module(self.retry)

    def sample(self, directory, content):
        path=Path(directory)/"current.xml";path.write_text(content,encoding="utf-8");return path

    def test_retry_cli_also_defaults_to_isolation_without_starting_unity_in_dry_run(self):
        with tempfile.TemporaryDirectory() as directory:
            report = self.sample(directory, '<test-run result="Failed">'
                '<test-case fullname="A.Fail" result="Failed" /></test-run>')
            output = io.StringIO()
            with patch.object(sys, "argv", [str(self.retry.__file__), "--results", str(report),
                    "--mode", "PlayMode", "--dry-run"]), \
                    patch.object(self.retry.subprocess, "run") as start_process, \
                    contextlib.redirect_stdout(output):
                self.assertEqual(self.retry.main(), 0)
            start_process.assert_not_called()
            plan = json.loads(output.getvalue())
            self.assertTrue(plan["noDesktopInteractionRequested"])
            self.assertIn("-batchmode", plan["stages"][0]["editorArguments"])
            self.assertIn("-v2-audit-no-desktop-interaction", plan["stages"][0]["editorArguments"])

    def test_only_nonpassing_exact_cases_are_selected_and_passing_cases_stay_out(self):
        with tempfile.TemporaryDirectory() as directory:
            report=self.sample(directory,'<test-run result="Failed">'
                '<test-case fullname="A.Pass" result="Passed" />'
                '<test-case fullname="A.Fail(2)" result="Failed" />'
                '<test-case fullname="A.Skip" result="Skipped" /></test-run>')
            source,selected=self.retry.retry_selectors(report)
            self.assertEqual(selected,("A.Fail(2)","A.Skip"))
            self.assertEqual(source["passed"],1)
            plan=runner.commands("retry-failed",Path("Project"),Path("Fresh"),
                selected_stages=[("PlayMode",selected)])
            self.assertEqual(len(plan),1);self.assertEqual(plan[0]["mode"],"PlayMode")
            command=plan[0]["command"]
            self.assertEqual(command[command.index("--filter")+1],"A.Fail(2);A.Skip")

    def test_empty_or_fixture_only_failure_cannot_become_a_passing_empty_rerun(self):
        with tempfile.TemporaryDirectory() as directory:
            for content in ('<test-run result="Failed" />',
                '<test-run result="Failed"><test-case fullname="A.Pass" result="Passed" /></test-run>'):
                with self.assertRaises(ValueError):self.retry.retry_selectors(self.sample(directory,content))
            report=self.sample(directory,'<test-run result="Passed"><test-case fullname="A.Pass" result="Passed" /></test-run>')
            _,selected=self.retry.retry_selectors(report)
            self.assertEqual(selected,())

    def test_selector_separator_and_missing_identity_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            for identity in ('fullname="A.Fail;Other"',''):
                report=self.sample(directory,'<test-run result="Failed"><test-case '+identity+' result="Failed" /></test-run>')
                with self.assertRaises(ValueError):self.retry.retry_selectors(report)

    def test_timings_retain_results_and_ignore_unavailable_nonfinite_or_negative_values(self):
        with tempfile.TemporaryDirectory() as directory:
            report=self.sample(directory,'<test-run result="Passed" duration="70.2">'
                '<test-case fullname="A.Small" result="Passed" duration="0.2" />'
                '<test-case fullname="A.Slow" result="Passed" duration="70" />'
                '<test-case fullname="A.Nan" result="Passed" duration="NaN" />'
                '<test-case fullname="A.Negative" result="Passed" duration="-4" />'
                '<test-case fullname="A.Missing" result="Passed" /></test-run>')
            result=runner.read_results(report,())
            self.assertEqual(result["passed"],5)
            timing=result["timing"]
            self.assertEqual(timing["reportedCases"],2)
            self.assertAlmostEqual(timing["summedCaseSeconds"],70.2)
            self.assertEqual(timing["over60Count"],1)
            self.assertEqual(timing["slowest"][0]["name"],"A.Slow")


if __name__ == "__main__":
    unittest.main()
