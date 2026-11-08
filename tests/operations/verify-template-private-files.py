#!/usr/bin/env python3
"""Check package and template-source exclusion boundaries using harmless files only.

The disposable fixture copies only package metadata, exclusion configuration, and
the public template icon. No application settings, certificates, or private keys
are copied from the checkout. --revision can demonstrate the failure in an older
commit using that commit's metadata without changing the working tree.
"""

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile


PRIVATE_MARKER = b"KRAFTER_OPERATIONAL_PRIVATE_SENTINEL_ONLY"
PUBLIC_MARKER = b"KRAFTER_OPERATIONAL_PUBLIC_SENTINEL_ONLY"
PRIVATE_NAMES = (
    "local.pfx", "local.p12", "private.pem", "private.key",
    "uppercase.PFX", "uppercase.P12", "uppercase.PEM", "uppercase.KEY",
    "mixed.PfX", "mixed.P12", "mixed.pEm", "mixed.kEy",
    "key-sentinel.xml", "appsettings.Local.json",
    "appsettings.Local.Development.json", "appsettings.Development.Local.json",
    ".env", ".env.Production", "secrets.json",
    "Key-uppercase.XML", "appsettings.local.extra.json", "AppSettings.Other.LOCAL.JsOn",
    ".ENV.Validation", "mixed/SeCrEtS.JsOn",
)
PUBLIC_FILES = (
    "public-examples/example.cer",
    "public-examples/example.crt",
    "public-examples/appsettings.Local.json.example",
)


def run(command, env, cwd=None):
    result = subprocess.run(command, cwd=cwd, env=env, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n{result.stdout[-6000:]}")
    return result.stdout


def fixture_bytes(source, relative, revision):
    if revision:
        return subprocess.check_output(["git", "show", f"{revision}:{relative}"], cwd=source)
    return (source / relative).read_bytes()


def generated_leaks(directory):
    return sorted(str(path.relative_to(directory)) for path in directory.rglob("*")
                  if path.is_file() and PRIVATE_MARKER in path.read_bytes())


def verify(source, dotnet, revision, workspace):
    template = workspace / "template"
    template.mkdir()
    metadata = (
        "AditiKraft.Krafter.Templates.csproj", "Directory.Build.props",
        ".template.config/template.json", ".template.config-single/template.json",
        ".template.config/icon.png",
    )
    for relative in metadata:
        destination = template / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(fixture_bytes(source, relative, revision))
    (template / "README.md").write_text("Harmless operational packaging fixture.\n")
    for solution in ("AditiKraft.Krafter.slnx", "AditiKraft.Krafter.Single.slnx"):
        (template / solution).write_text("<Solution />\n")

    roots = {Path("."), Path("nested"), Path("src/AditiKraft.Krafter.Backend")}
    for config in (".template.config/template.json", ".template.config-single/template.json"):
        data = json.loads((template / config).read_text())
        # This fixture has no application dependencies. Disable its post-actions
        # instead of assuming a custom template defines --no-restore.
        data.pop("postActions", None)
        (template / config).write_text(json.dumps(data, indent=2) + "\n")
        roots.update(Path(item.get("source", ".")) for item in data["sources"])
    for root in sorted(roots):
        directory = template / root
        directory.mkdir(parents=True, exist_ok=True)
        # NuGet omits empty directories. Retain each overlay source after all
        # private sentinels are excluded so the single template remains valid.
        (directory / "fixture-public.crt").write_bytes(PUBLIC_MARKER)
        for name in PRIVATE_NAMES:
            sentinel = directory / name
            sentinel.parent.mkdir(parents=True, exist_ok=True)
            sentinel.write_bytes(PRIVATE_MARKER)
    for relative in PUBLIC_FILES:
        destination = template / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(PUBLIC_MARKER)

    env = dict(os.environ)
    env["DOTNET_CLI_HOME"] = str(workspace / "cli-home")
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env["DOTNET_NOLOGO"] = "1"
    packages = workspace / "packages"
    run([dotnet, "pack", str(template / "AditiKraft.Krafter.Templates.csproj"),
         "-c", "Release", "-o", str(packages),
         "--disable-build-servers", "-m:1",
         "-p:PackageVersion=0.0.1-operational-sentinel"], env)
    package = next(packages.glob("*.nupkg"))
    with zipfile.ZipFile(package) as archive:
        leaks = sorted(name for name in archive.namelist() if PRIVATE_MARKER in archive.read(name))
        missing_public = [name for name in PUBLIC_FILES if "content/" + name not in archive.namelist()]
    summary = {"revision": revision or "working tree", "package_private_files": leaks,
               "package_missing_public_examples": missing_public, "generated": []}

    # Source installation independently exercises template exclusions even when
    # the package boundary has already removed every private sentinel.
    for install_kind, install_path in (("package", package), ("source", template)):
        hive = workspace / f"hive-{install_kind}"
        hive_option = ["--debug:custom-hive", str(hive)]
        run([dotnet, "new", "install", str(install_path), *hive_option], env)
        for short_name, name in (("krafter", "PrivateFilesSplit"), ("krafter-single", "PrivateFilesSingle")):
            output = workspace / f"{install_kind}-{name}"
            run([dotnet, "new", short_name, "-n", name, "-o", str(output),
                 "--no-update-check", *hive_option], env)
            generated = {"install": install_kind, "template": short_name,
                         "private_files": generated_leaks(output),
                         "missing_public_examples": [relative for relative in PUBLIC_FILES
                                                     if not (output / relative).is_file()]}
            summary["generated"].append(generated)
    print(json.dumps(summary, indent=2))
    return int(bool(leaks or missing_public or any(
        result["private_files"] or result["missing_public_examples"] for result in summary["generated"])))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    parser.add_argument("--revision", help="Read only package metadata from this Git revision")
    arguments = parser.parse_args()
    if not arguments.dotnet:
        parser.error("dotnet is not on PATH; supply --dotnet /path/to/dotnet")
    with tempfile.TemporaryDirectory(prefix="krafter-private-files-") as temporary:
        return verify(arguments.source.resolve(), arguments.dotnet, arguments.revision, Path(temporary))


if __name__ == "__main__":
    raise SystemExit(main())
