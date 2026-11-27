#!/usr/bin/env python3
"""Build a local package without rewriting the frozen root payload."""

import hashlib
import json
import contextlib
import csv
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile


ROOT = Path(__file__).resolve().parent.parent
PUBLISHED_URL = "https://github.com/pardeike/SheepHappens/releases/download/v1.7.0.0/SheepHappens.zip"
PUBLISHED_SHA256 = "963ea6e02e4eebfa77d37ba9ff19501b855bd260d366f792f38a35cf2be57573"
FROZEN_ROOT = json.loads((ROOT / "scripts/frozen-payload.json").read_text())
MUTABLE = {"About/About.xml", "About/Manifest.xml", "LoadFolders.xml", "README.md"}
COMPANION = ROOT / "artifacts/BridgeTools/SheepHappens/SheepHappens.BridgeTools.dll"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def files_under(folder):
    return {
        p.relative_to(ROOT).as_posix(): p.read_bytes()
        for p in (ROOT / folder).rglob("*")
        if p.is_file() and p.name != ".DS_Store" and not p.name.startswith("._")
    }


def verify_legacy():
    actual = {}
    for folder in ("1.2", "1.3", "1.4", "1.6", "Sounds", "Textures"):
        actual.update(files_under(folder))
    actual["About/Preview.PNG"] = (ROOT / "About/Preview.PNG").read_bytes()
    require(set(actual) == set(FROZEN_ROOT), "Frozen source payload has missing or additional files.")
    for name, expected in FROZEN_ROOT.items():
        require(hashlib.sha256(actual[name]).hexdigest() == expected, f"Frozen source file changed: {name}")


def published_payload():
    archive = ROOT / ".builds/published-v1.7.0.0.zip"
    if not archive.exists():
        archive.parent.mkdir(parents=True, exist_ok=True)
        with urllib.request.urlopen(PUBLISHED_URL, timeout=60) as response:
            data = response.read()
        require(hashlib.sha256(data).hexdigest() == PUBLISHED_SHA256, "Published archive checksum mismatch.")
        archive.write_bytes(data)
    require(hashlib.sha256(archive.read_bytes()).hexdigest() == PUBLISHED_SHA256,
            f"Published archive checksum mismatch. Remove {archive} and retry.")
    with ZipFile(archive) as release:
        return {name.removeprefix("SheepHappens/"): release.read(name) for name in release.namelist()
                if not name.endswith("/") and name.removeprefix("SheepHappens/") not in MUTABLE}


def package():
    verify_legacy()
    published = published_payload()
    current = files_under("1.7")
    dlls = {name for name in current if name.endswith(".dll")}
    require(dlls == {"1.7/Assemblies/SheepHappens.dll", "1.7/Assemblies/CrossPromotion.dll"},
            "Build 1.7 first. Its only runtime DLLs must be SheepHappens.dll and CrossPromotion.dll.")
    current = {name: data for name, data in current.items()
               if name in dlls or name.startswith("1.7/Defs/") and name.endswith(".xml")}
    require("1.7/Defs/SheepHappens.xml" in current, "Missing 1.7 defs.")

    version = ET.parse(ROOT / "Directory.Build.props").findtext(".//ModVersion")
    about = ET.parse(ROOT / "About/About.xml")
    manifest = ET.parse(ROOT / "About/Manifest.xml")
    require(about.findtext("packageId") == "brrainz.sheephappens", "Unexpected package ID.")
    require(about.findtext("modVersion") == manifest.findtext("version") == version,
            "About.xml and Manifest.xml must match ModVersion.")
    require([e.text for e in about.findall("supportedVersions/li")] == ["1.6", "1.7"],
            "About.xml must advertise only RimWorld 1.6 and 1.7.")
    require([e.text for e in manifest.findall("targetVersions/li")] == ["1.6.0", "1.7.0"],
            "Manifest.xml must target RimWorld 1.6 and 1.7.")
    load_folders = ET.parse(ROOT / "LoadFolders.xml")
    require([e.text for e in load_folders.findall("v1.6/li")] == ["/", "1.6"], "1.6 must retain its original load folders.")
    require([e.text for e in load_folders.findall("v1.7/li")] == ["/", "1.7"], "1.7 must load shared media and 1.7/ only.")

    payload = published | current
    for name in MUTABLE:
        payload[name] = (ROOT / name).read_bytes()
    destination = ROOT / ".builds/package"
    stage = destination / "SheepHappens"
    destination.mkdir(parents=True, exist_ok=True)
    if stage.exists():
        shutil.rmtree(stage)
    for name, data in payload.items():
        path = stage / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    output = destination / "SheepHappens.zip"
    with ZipFile(output, "w", ZIP_DEFLATED) as archive:
        for name in sorted(payload):
            archive.write(stage / name, name)
    with ZipFile(output) as archive:
        actual = {name: archive.read(name) for name in archive.namelist()}
    require(actual == payload, "ZIP contents differ from the staged payload.")
    require(all(actual[name] == data for name, data in published.items()), "Packaged 1.6 payload changed.")
    verify_legacy()
    return payload, published, output


def read_tree(root):
    files = {}
    for path in root.rglob("*"):
        require(not path.is_symlink(), f"Refusing symlink in installed payload: {path}")
        if path.is_file() and path.name != ".DS_Store" and not path.name.startswith("._"):
            files[path.relative_to(root).as_posix()] = path.read_bytes()
    return files


def deploy_root(value):
    logical = Path(os.path.abspath(os.path.expanduser(value)))
    require(logical.is_dir(), f"Mods directory does not exist: {logical}")
    physical = logical.resolve(strict=True)
    require(logical.name.casefold() == physical.name.casefold() == "mods",
            "RIMWORLD_MOD_DIR must logically and physically name an existing Mods directory.")
    destination = physical / "SheepHappens"
    require(not destination.is_symlink(), f"Refusing symlinked mod destination: {destination}")
    require(not destination.exists() or destination.is_dir(), f"Not a mod directory: {destination}")
    require(destination != ROOT and destination not in ROOT.parents, "Deployment would overwrite the source checkout.")
    require(not (physical / "SheepHappens.zip").is_symlink(), "Refusing symlinked ZIP destination.")
    if logical.parent.suffix.casefold() == ".app":
        outer = logical.parent.parent / "Mods"
        require(not outer.is_dir() or outer.resolve() != physical,
                f"RimWorld 1.7 uses the game-root Mods path. Set RIMWORLD_MOD_DIR={outer}")
    if logical.parent.name.endswith("-UserData"):
        app_mods = logical.parent.parent / (logical.parent.name.removesuffix("-UserData") + ".app") / "Mods"
        require(not app_mods.is_dir() or app_mods.resolve() != physical,
                "Refusing ambiguous UserData Mods alias. Use the game-root Mods path.")
    return logical, destination


def bridge_destination(mods):
    root = mods.parent / "BridgeTools"
    destination = root / "SheepHappens"
    for path in (root, destination):
        require(not path.is_symlink(), f"Refusing symlinked BridgeTools path: {path}")
        require(not path.exists() or path.is_dir(), f"Not a BridgeTools directory: {path}")
    files = read_tree(destination) if destination.exists() else {}
    require(set(files) <= {"SheepHappens.BridgeTools.dll"}, "Unexpected files in the SheepHappens companion bundle.")
    return destination


def ensure_stopped():
    command = ["tasklist", "/FO", "CSV", "/NH"] if os.name == "nt" else ["ps", "-A", "-o", "pid=,comm="]
    result = subprocess.run(command, capture_output=True, text=True)
    require(result.returncode == 0, "Cannot determine whether RimWorld is running. Deployment refused.")
    if os.name == "nt":
        names = [row[0] for row in csv.reader(result.stdout.splitlines()) if row]
    else:
        names = [line.strip().split(None, 1)[1] for line in result.stdout.splitlines() if len(line.strip().split(None, 1)) == 2]
    pattern = r"RimWorld(?:Win64|Linux)?(?:\.exe)?|RimWorld by Ludeon Studios"
    require(not any(re.fullmatch(pattern, Path(name).name, re.IGNORECASE) for name in names),
            "RimWorld is running. Deployment refused. This workflow never stops the game.")


def verify_installed_legacy(destination, published):
    if not destination.exists():
        return
    installed = read_tree(destination)
    legacy = {name: data for name, data in installed.items()
              if name in published or name.startswith(("1.2/", "1.3/", "1.4/", "1.6/", "Sounds/", "Textures/"))}
    for name, expected in published.items():
        require(legacy.get(name) == expected,
                f"Installed frozen 1.6 file differs or is missing: {destination / name}. No files were changed.")
    require(set(legacy) == set(published), "Installed frozen 1.6 payload has additional files. No files were changed.")


@contextlib.contextmanager
def deployment_lock(mods):
    digest = hashlib.sha256(f"{mods.resolve()}\nSheepHappens\n".encode()).hexdigest()
    path = Path(tempfile.gettempdir()) / f"steam-mods-deploy-{digest}.lock"
    require(not path.is_symlink(), f"Refusing symlinked deployment lock: {path}")
    with path.open("a+b") as handle:
        if os.name == "nt":
            import msvcrt
            if handle.tell() == 0:
                handle.write(b"\0")
                handle.flush()
            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        # Closing the handle releases the lock, including after cancellation.
        yield


def check_deploy(value, build_bridge_tools=True):
    mods, destination = deploy_root(value)
    ensure_stopped()
    verify_installed_legacy(destination, published_payload())
    if build_bridge_tools:
        bridge_destination(mods)


def atomic_write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(data)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def deploy(value, build_bridge_tools=True):
    mods, destination = deploy_root(value)
    with deployment_lock(mods):
        ensure_stopped()
        verify_installed_legacy(destination, published_payload())
        bridge = bridge_destination(mods) if build_bridge_tools else None
        if bridge is not None:
            require(COMPANION.is_file(), "Build the matching SheepHappens companion before deploying.")
            require(set(read_tree(COMPANION.parent)) == {COMPANION.name}, "Companion output contains extra files or dependency DLLs.")
        payload, published, archive = package()
        installed = read_tree(destination) if destination.exists() else {}
        unexpected = set(installed) - set(payload)
        require(not unexpected, f"Installed payload has unexpected files: {', '.join(sorted(unexpected))}. No files were changed.")
        ensure_stopped()
        if not destination.exists():
            # Install the complete initial payload in one directory rename.
            with tempfile.TemporaryDirectory(prefix=".SheepHappens-", dir=mods) as temporary:
                stage = Path(temporary) / "SheepHappens"
                for name, data in payload.items():
                    path = stage / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(data)
                stage.rename(destination)
        else:
            for name, data in payload.items():
                if name not in published and installed.get(name) != data:
                    atomic_write(destination / name, data)
        require(read_tree(destination) == payload, "Installed files differ from the verified package.")
        verify_installed_legacy(destination, published)
        if bridge is not None:
            companion_bytes = COMPANION.read_bytes()
            installed_companion = bridge / COMPANION.name
            if not installed_companion.is_file() or installed_companion.read_bytes() != companion_bytes:
                atomic_write(installed_companion, companion_bytes)
            require(read_tree(bridge) == {COMPANION.name: companion_bytes}, "Installed companion differs from this build.")
        atomic_write(mods / "SheepHappens.zip", archive.read_bytes())
        with ZipFile(mods / "SheepHappens.zip") as deployed_zip:
            require({name: deployed_zip.read(name) for name in deployed_zip.namelist()} == read_tree(destination),
                    "Deployment ZIP differs from installed files.")
        verify_legacy()


if __name__ == "__main__":
    try:
        action = sys.argv[1:]
        if action == ["verify-legacy"]:
            verify_legacy()
        elif action == ["package"]:
            package()
        elif len(action) == 4 and action[0] in {"check-deploy", "deploy"}:
            logical_mods = Path(os.path.abspath(os.path.expanduser(action[1])))
            supplied_bridge_root = Path(os.path.abspath(action[3]))
            require(supplied_bridge_root == logical_mods.parent / "BridgeTools",
                    "The companion root must be the selected Mods directory's sibling BridgeTools directory.")
            require(action[2] in {"true", "false"}, "BuildBridgeTools must be true or false.")
            command = check_deploy if action[0] == "check-deploy" else deploy
            command(action[1], action[2] == "true")
        else:
            raise ValueError("Usage: rimworld_deploy_guard.py verify-legacy|package|check-deploy MODS true|false BRIDGE_ROOT|deploy MODS true|false BRIDGE_ROOT")
    except (OSError, ValueError) as error:
        print(f"error: SheepHappens: {error}", file=sys.stderr)
        sys.exit(1)
