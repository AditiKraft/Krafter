#!/usr/bin/env python3
"""Check devcontainer setup with a stub dotnet; never restore or install a tool."""

import json
import os
from pathlib import Path
import shlex
import subprocess
import tempfile
import unittest


SOURCE = Path(__file__).resolve().parents[2]
DOTNET_STUB = """#!/bin/sh
printf '%s\\n' "$*" >> "$STARTUP_PROBE_CALLS"
case "$1" in
  restore) exit "$STARTUP_PROBE_RESTORE_EXIT" ;;
  tool)
    case "$2" in
      list)
        if [ "$STARTUP_PROBE_LIST_EXIT" != 0 ]; then exit "$STARTUP_PROBE_LIST_EXIT"; fi
        if [ "$STARTUP_PROBE_INSTALLED" = yes ]; then
          printf 'dotnet-ef   10.0.0   dotnet-ef\\n'
        else
          printf 'dotnet-format   10.0.0   dotnet-format\\n'
        fi ;;
      install) exit "$STARTUP_PROBE_INSTALL_EXIT" ;;
      *) exit 99 ;;
    esac ;;
  *) exit 99 ;;
esac
"""


class DevcontainerStartupTests(unittest.TestCase):
    def test_solution_selection_and_failure_propagation(self):
        scenarios = (
            ("missing tool", False, 0, 0, 0, 0, 3),
            ("installed tool", True, 0, 0, 0, 0, 2),
            ("restore failure", True, 23, 0, 0, 23, 1),
            ("tool listing failure", False, 0, 29, 0, 29, 2),
            ("tool installation failure", False, 0, 0, 31, 31, 3),
        )
        for project, source_checkout in (("AditiKraft.Krafter", True), ("SplitProbe", False), ("SingleProbe", False)):
            for name, installed, restore_exit, list_exit, install_exit, expected_exit, call_count in scenarios:
                with self.subTest(project=project, scenario=name), tempfile.TemporaryDirectory(prefix="krafter-startup-") as directory:
                    root = Path(directory)
                    (root / ".devcontainer").mkdir()
                    script = (SOURCE / ".devcontainer/post-create.sh").read_text().replace("AditiKraft.Krafter", project)
                    (root / ".devcontainer/post-create.sh").write_text(script)
                    (root / f"{project}.slnx").touch()
                    if source_checkout:
                        (root / f"{project}.Dev.slnx").touch()
                    binary_dir = root / "bin"
                    binary_dir.mkdir()
                    stub = binary_dir / "dotnet"
                    stub.write_text(DOTNET_STUB)
                    stub.chmod(0o755)
                    calls = root / "calls.txt"
                    environment = os.environ.copy()
                    environment.update(
                        PATH=f"{binary_dir}{os.pathsep}{environment['PATH']}",
                        STARTUP_PROBE_CALLS=str(calls),
                        STARTUP_PROBE_INSTALLED="yes" if installed else "no",
                        STARTUP_PROBE_RESTORE_EXIT=str(restore_exit),
                        STARTUP_PROBE_LIST_EXIT=str(list_exit),
                        STARTUP_PROBE_INSTALL_EXIT=str(install_exit),
                    )
                    configuration = json.loads((SOURCE / ".devcontainer/devcontainer.json").read_text())
                    result = subprocess.run(shlex.split(configuration["postCreateCommand"]), cwd=root,
                                            env=environment, text=True, capture_output=True)
                    self.assertEqual(expected_exit, result.returncode, result.stderr)
                    actual_calls = calls.read_text().splitlines()
                    suffix = ".Dev" if source_checkout else ""
                    self.assertEqual(f"restore {project}{suffix}.slnx", actual_calls[0])
                    self.assertEqual(call_count, len(actual_calls), actual_calls)
                    if call_count == 3:
                        self.assertEqual("tool install --global dotnet-ef --version 10.*", actual_calls[-1])


if __name__ == "__main__":
    unittest.main()
