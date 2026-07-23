"""
Smoke tests for DevHunt Analyzer.
Verifies that all rule modules load, rules fire on known-bad snippets,
and the CLI entry-point works.

Run:  pytest tests/ -v
"""
from __future__ import annotations

from pathlib import Path
from textwrap import dedent

import pytest

# Trigger registration
import devhunt_analyzer.rules  # noqa: F401

from devhunt_analyzer.engine.models import (
    Category,
    FileContext,
    Language,
    Severity,
)
from devhunt_analyzer.engine.rule_engine import analyze_file, run_analysis
from devhunt_analyzer.engine.rule_registry import get_all_rules


# ── helpers ────────────────────────────────────────────────────────────────

def _ctx(code: str, lang: Language, filename: str = "test_file") -> FileContext:
    """Build a FileContext from a code snippet (no AST)."""
    return FileContext(
        path=Path(filename),
        content=dedent(code),
        lines=dedent(code).splitlines(),
        language=lang,
    )


def _find_rule(rule_id: str):
    rules = get_all_rules()
    for r in rules:
        if r.rule_id == rule_id:
            return r
    raise ValueError(f"Rule {rule_id} not found")


def _run_rule(rule_id: str, code: str, lang: Language, filename: str = "test_file"):
    """Run a single rule on a code snippet and return issues."""
    rule = _find_rule(rule_id)
    ctx = _ctx(code, lang, filename)
    return rule.check(ctx)


# ── Module-level tests ────────────────────────────────────────────────────

class TestRuleRegistry:
    def test_all_rules_loaded(self):
        rules = get_all_rules()
        assert len(rules) >= 500, f"Expected ≥500 rules, got {len(rules)}"

    def test_unique_rule_ids(self):
        rules = get_all_rules()
        ids = [r.rule_id for r in rules]
        duplicates = [rid for rid in ids if ids.count(rid) > 1]
        assert not duplicates, f"Duplicate rule IDs: {set(duplicates)}"

    def test_all_rules_have_required_fields(self):
        for rule in get_all_rules():
            assert rule.rule_id, f"Rule missing rule_id: {rule}"
            assert rule.name, f"Rule {rule.rule_id} missing name"
            assert rule.severity in Severity, f"Rule {rule.rule_id} bad severity"
            assert rule.category in Category, f"Rule {rule.rule_id} bad category"
            assert rule.languages, f"Rule {rule.rule_id} has no languages"

    def test_rule_prefixes_match_modules(self):
        """Each module should have a consistent prefix."""
        rules = get_all_rules()
        prefix_count: dict[str, int] = {}
        for r in rules:
            prefix = r.rule_id.split("-")[0]
            prefix_count[prefix] = prefix_count.get(prefix, 0) + 1

        expected = {
            "CS", "TS", "SEC", "PY", "JV", "GO", "RS", "PHP", "RB",
            "KT", "SW", "CC", "DK", "YC", "RN", "EF", "DF", "SP",
        }
        assert expected.issubset(set(prefix_count.keys())), (
            f"Missing prefixes: {expected - set(prefix_count.keys())}"
        )


# ── C# rule tests ─────────────────────────────────────────────────────────

class TestCSharpRules:
    def test_blocking_async(self):
        code = """\
        var result = task.Result;
        """
        issues = _run_rule("CS-B01", code, Language.CSHARP)
        assert len(issues) >= 1

    def test_raw_sql_concatenation(self):
        code = """\
        var result = _context.Users.FromSqlRaw($"SELECT * FROM Users WHERE Name = '{name}'");
        """
        issues = _run_rule("CS-B02", code, Language.CSHARP)
        assert len(issues) >= 1


# ── TypeScript rule tests ─────────────────────────────────────────────────

class TestTypeScriptRules:
    def test_any_type(self):
        code = """\
        function foo(x: any) { return x; }
        """
        issues = _run_rule("TS-F01", code, Language.TYPESCRIPT)
        assert len(issues) >= 1

    def test_console_log(self):
        code = """\
        console.log("debug");
        """
        issues = _run_rule("TS-F06", code, Language.TYPESCRIPT)
        assert len(issues) >= 1


# ── Security rule tests ───────────────────────────────────────────────────

class TestSecurityRules:
    def test_hardcoded_password(self):
        code = """\
        const password = "SuperSecret123";
        """
        issues = _run_rule("SEC-04", code, Language.TYPESCRIPT)
        assert len(issues) >= 1

    def test_sql_injection_interpolation(self):
        code = 'var query = "SELECT * FROM Users WHERE Id = " + userId;'
        issues = _run_rule("SEC-01", code, Language.CSHARP)
        assert len(issues) >= 1


# ── React / Next.js rule tests ────────────────────────────────────────────

class TestReactNextjsRules:
    def test_dangerously_set_inner_html(self):
        code = '<div dangerouslySetInnerHTML={{ __html: userInput }} />'
        issues = _run_rule("RN-S01", code, Language.TYPESCRIPT, filename="Component.tsx")
        assert len(issues) >= 1

    def test_index_as_key(self):
        code = 'items.map((item, index) => <li key={index}>{item}</li>)'
        issues = _run_rule("RN-Q08", code, Language.TYPESCRIPT, filename="List.tsx")
        assert len(issues) >= 1


# ── ASP.NET / EF Core rule tests ──────────────────────────────────────────

class TestAspnetEfCoreRules:
    def test_raw_sql_injection(self):
        code = """\
        var result = _context.Users.FromSqlRaw($"SELECT * FROM Users WHERE Name = '{name}'");
        """
        issues = _run_rule("EF-S01", code, Language.CSHARP)
        assert len(issues) >= 1

    def test_save_changes_in_loop(self):
        lines = [
            "foreach (var item in items) {",
            "    _context.Items.Add(item);",
            "    await _context.SaveChangesAsync();",
            "}",
        ]
        code = "\n".join(lines)
        ctx = _ctx(code, Language.CSHARP)
        rule = _find_rule("EF-Q03")
        issues = rule.check(ctx)
        assert len(issues) >= 1

    def test_dbcontext_singleton(self):
        code = """\
        services.AddSingleton<AppDbContext>();
        """
        issues = _run_rule("EF-Q10", code, Language.CSHARP)
        assert len(issues) >= 1

    def test_middleware_order(self):
        code = """\
        app.UseAuthorization();
        app.UseAuthentication();
        """
        issues = _run_rule("EF-M03", code, Language.CSHARP, filename="Program.cs")
        assert len(issues) >= 1


# ── Django / Flask rule tests ─────────────────────────────────────────────

class TestDjangoFlaskRules:
    def test_debug_true(self):
        code = """\
        DEBUG = True
        """
        issues = _run_rule("DF-S02", code, Language.PYTHON, filename="settings.py")
        assert len(issues) >= 1

    def test_hardcoded_secret(self):
        code = """\
        SECRET_KEY = "my-super-secret-key-12345678"
        """
        issues = _run_rule("DF-S03", code, Language.PYTHON)
        assert len(issues) >= 1

    def test_flask_debug_mode(self):
        code = """\
        app.run(debug=True, host="0.0.0.0")
        """
        issues = _run_rule("DF-S09", code, Language.PYTHON)
        assert len(issues) >= 1

    def test_csrf_exempt(self):
        code = """\
        @csrf_exempt
        def my_view(request):
            pass
        """
        issues = _run_rule("DF-S05", code, Language.PYTHON)
        assert len(issues) >= 1


# ── Spring rule tests ─────────────────────────────────────────────────────

class TestSpringRules:
    def test_sql_injection_query(self):
        code = '@Query("SELECT u FROM User u WHERE u.name = " + name)'
        issues = _run_rule("SP-S01", code, Language.JAVA)
        assert len(issues) >= 1

    def test_field_injection(self):
        # SP-Q03 uses \n in pattern — must supply two separate lines
        code = "@Autowired\nprivate UserRepository userRepository;"
        issues = _run_rule("SP-Q03", code, Language.JAVA)
        assert len(issues) >= 1

    def test_csrf_disabled(self):
        code = 'http.csrf().disable();'
        issues = _run_rule("SP-S07", code, Language.JAVA)
        assert len(issues) >= 1


# ── Docker rule tests ─────────────────────────────────────────────────────

class TestDockerRules:
    def test_latest_tag(self):
        code = "FROM python:latest\nRUN pip install flask"
        issues = _run_rule("DK-S02", code, Language.DOCKERFILE)
        assert len(issues) >= 1

    def test_run_as_root(self):
        code = 'FROM ubuntu:22.04\nRUN apt-get update\nCMD ["python", "app.py"]'
        issues = _run_rule("DK-S01", code, Language.DOCKERFILE)
        assert len(issues) >= 1


# ── Python rule tests ─────────────────────────────────────────────────────

class TestPythonRules:
    def test_bare_except(self):
        code = """\
        try:
            do_something()
        except:
            pass
        """
        issues = _run_rule("PY-Q01", code, Language.PYTHON)
        assert len(issues) >= 1


# ── Engine integration tests ──────────────────────────────────────────────

class TestEngine:
    def test_analyze_file_returns_issues(self, tmp_path: Path):
        bad_cs = tmp_path / "Bad.cs"
        bad_cs.write_text('var x = task.Result;\nvar y = "SELECT * FROM Users WHERE Id = " + id;')
        rules = get_all_rules()
        issues = analyze_file(bad_cs, Language.CSHARP, rules)
        assert len(issues) > 0

    def test_run_analysis_on_dir(self, tmp_path: Path):
        # Create a small project
        (tmp_path / "app.py").write_text('SECRET_KEY = "hardcoded-key-12345678"\n')
        (tmp_path / "main.ts").write_text('function foo(x: any) { console.log(x); }\n')
        report = run_analysis(target=tmp_path, workers=1)
        assert report.files_analyzed >= 2
        assert report.total_issues > 0

    def test_disabled_rules(self, tmp_path: Path):
        f = tmp_path / "test.ts"
        f.write_text('function foo(x: any) {}\n')
        report_full = run_analysis(target=tmp_path, workers=1)
        report_disabled = run_analysis(target=tmp_path, workers=1, disabled_rules={"TS-F01"})
        ts_f01_full = [i for i in report_full.issues if i.rule_id == "TS-F01"]
        ts_f01_disabled = [i for i in report_disabled.issues if i.rule_id == "TS-F01"]
        assert len(ts_f01_full) > 0
        assert len(ts_f01_disabled) == 0


# ── CLI smoke test ────────────────────────────────────────────────────────

class TestCli:
    def test_list_rules(self):
        from click.testing import CliRunner
        from devhunt_analyzer.cli import main

        runner = CliRunner()
        result = runner.invoke(main, ["list-rules"])
        assert result.exit_code == 0
        assert "CS-B01" in result.output
        assert "SEC-01" in result.output

    def test_analyze_json(self, tmp_path: Path):
        (tmp_path / "test.cs").write_text('var x = task.Result;\n')
        from click.testing import CliRunner
        from devhunt_analyzer.cli import main

        runner = CliRunner()
        result = runner.invoke(main, ["analyze", str(tmp_path), "--json"])
        assert result.exit_code == 0
        assert '"total_issues"' in result.output
