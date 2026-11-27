"""Exercise deployment guards only against disposable Mods directories."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from zipfile import ZipFile

import rimworld_deploy_guard as workflow


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(dir=workflow.ROOT / ".builds", prefix="workflow-check-")
        self.addCleanup(self.temporary.cleanup)
        self.mods = Path(self.temporary.name) / "Mods"
        self.mods.mkdir()
        self.destination = self.mods / "SheepHappens"

    def deploy_stopped(self):
        with patch.object(workflow, "ensure_stopped"):
            workflow.deploy(str(self.mods))

    def snapshot(self):
        return workflow.read_tree(self.mods)

    def assert_matches_zip(self):
        with ZipFile(self.mods / "SheepHappens.zip") as archive:
            self.assertEqual({name: archive.read(name) for name in archive.namelist()},
                             workflow.read_tree(self.destination))

    def test_first_install_uses_published_payload_and_verifies_zip(self):
        self.deploy_stopped()
        published = workflow.published_payload()
        for name, data in published.items():
            self.assertEqual((self.destination / name).read_bytes(), data)
        self.assert_matches_zip()
        companion = self.mods.parent / "BridgeTools/SheepHappens/SheepHappens.BridgeTools.dll"
        self.assertEqual(companion.read_bytes(), workflow.COMPANION.read_bytes())
        self.assertEqual(set(workflow.read_tree(companion.parent)), {companion.name})
        with ZipFile(self.mods / "SheepHappens.zip") as archive:
            names = archive.namelist()
            self.assertIn("1.6/Assemblies/RimBridgeServer.Annotations.dll", names)
            self.assertFalse(any("BridgeTools" in n or n.endswith("RimBridgeServer.Sdk.dll") for n in names))
            self.assertEqual({n for n in names if n.startswith("1.7/Assemblies/")},
                             {"1.7/Assemblies/SheepHappens.dll", "1.7/Assemblies/CrossPromotion.dll"})

    def test_update_preserves_frozen_files_and_updates_current_dll(self):
        self.deploy_stopped()
        frozen = {}
        for name in workflow.published_payload():
            path = self.destination / name
            os.utime(path, ns=(1_600_000_000_000_000_000, 1_600_000_000_000_000_000))
            stat = path.stat()
            frozen[name] = path.read_bytes(), stat.st_ino, stat.st_mtime_ns
        (self.destination / "1.7/Assemblies/SheepHappens.dll").write_bytes(b"previous 1.7 build")
        self.deploy_stopped()
        for name, expected in frozen.items():
            path = self.destination / name
            stat = path.stat()
            self.assertEqual((path.read_bytes(), stat.st_ino, stat.st_mtime_ns), expected)
        self.assertEqual((self.destination / "1.7/Assemblies/SheepHappens.dll").read_bytes(),
                         (workflow.ROOT / "1.7/Assemblies/SheepHappens.dll").read_bytes())
        self.assert_matches_zip()

    def test_each_mismatched_frozen_file_refuses_all_payload_changes(self):
        self.deploy_stopped()
        for name in ("1.6/Assemblies/SheepHappens.dll", "1.6/Defs/SheepHappens.xml",
                     "Textures/DisguiseMask/DisguiseMask.png", "Sounds/SheepIgnite.wav", "About/Preview.PNG"):
            with self.subTest(name=name):
                path = self.destination / name
                original = path.read_bytes()
                path.write_bytes(b"different installed older payload")
                before = self.snapshot()
                with self.assertRaisesRegex(ValueError, "frozen 1.6 file differs"):
                    self.deploy_stopped()
                self.assertEqual(self.snapshot(), before)
                path.write_bytes(original)

    def test_missing_frozen_file_is_not_recreated(self):
        self.deploy_stopped()
        (self.destination / "1.6/Defs/SheepHappens.xml").unlink()
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, "missing"):
            self.deploy_stopped()
        self.assertEqual(self.snapshot(), before)

    def test_additional_legacy_resource_is_not_removed(self):
        self.deploy_stopped()
        extra = self.destination / "1.6/Defs/Extra.xml"
        extra.write_bytes(b"older resource")
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, "additional files"):
            self.deploy_stopped()
        self.assertEqual(self.snapshot(), before)

    def test_unknown_versioned_payload_is_not_removed(self):
        self.deploy_stopped()
        extra = self.destination / "1.5/Assemblies/SheepHappens.dll"
        extra.parent.mkdir(parents=True)
        extra.write_bytes(b"older version")
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, "unexpected files"):
            self.deploy_stopped()
        self.assertEqual(self.snapshot(), before)

    def test_symlinked_destination_is_rejected(self):
        elsewhere = self.mods.parent / "untouched"
        elsewhere.mkdir()
        self.destination.symlink_to(elsewhere, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlinked mod destination"):
            self.deploy_stopped()
        self.assertEqual(list(elsewhere.iterdir()), [])

    def test_invalid_root_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "existing Mods directory"):
            workflow.deploy_root(str(self.mods.parent))

    def test_concurrent_deploy_is_rejected(self):
        with workflow.deployment_lock(self.mods):
            with self.assertRaises(OSError):
                with workflow.deployment_lock(self.mods):
                    self.fail("A second installer acquired the same lock.")

    def test_running_game_refuses_install(self):
        with patch.object(workflow, "ensure_stopped", side_effect=ValueError("RimWorld is running")):
            with self.assertRaisesRegex(ValueError, "RimWorld is running"):
                workflow.deploy(str(self.mods))
        self.assertFalse(self.destination.exists())

    def test_public_install_omits_new_companion_but_preserves_old_annotations(self):
        with patch.object(workflow, "ensure_stopped"):
            workflow.deploy(str(self.mods), build_bridge_tools=False)
        self.assertFalse((self.mods.parent / "BridgeTools").exists())
        self.assertEqual((self.destination / "1.6/Assemblies/RimBridgeServer.Annotations.dll").read_bytes(),
                         workflow.published_payload()["1.6/Assemblies/RimBridgeServer.Annotations.dll"])
        self.assert_matches_zip()

    def test_unexpected_companion_sdk_refuses_all_payload_changes(self):
        self.deploy_stopped()
        bundle = self.mods.parent / "BridgeTools/SheepHappens"
        (bundle / "RimBridgeServer.Sdk.dll").write_bytes(b"must be supplied by the host")
        before = self.snapshot(), workflow.read_tree(bundle)
        with self.assertRaisesRegex(ValueError, "Unexpected files"):
            self.deploy_stopped()
        self.assertEqual((self.snapshot(), workflow.read_tree(bundle)), before)

    def test_missing_companion_refuses_install(self):
        with patch.object(workflow, "COMPANION", self.mods.parent / "missing.dll"):
            with self.assertRaisesRegex(ValueError, "matching SheepHappens companion"):
                self.deploy_stopped()
        self.assertFalse(self.destination.exists())

    def test_old_macos_alias_is_rejected(self):
        app = self.mods.parent / "RimWorldMac.app"
        app.mkdir()
        alias = app / "Mods"
        alias.symlink_to(self.mods, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "game-root Mods path"):
            workflow.deploy_root(str(alias))

    def native_build(self, game_running=False, package_only=False):
        # A simulated process list keeps this isolated install independent of a live game.
        bin_dir = self.mods.parent / "bin"
        bin_dir.mkdir()
        ps = bin_dir / "ps"
        process = "123 /games/RimWorld" if game_running else "1 /usr/bin/launchd"
        ps.write_text(f"#!/bin/sh\nprintf '{process}\\n'\n")
        ps.chmod(0o755)
        env = os.environ.copy()
        env["PATH"] = str(bin_dir) + os.pathsep + env["PATH"]
        env["RIMWORLD_MOD_DIR"] = str(self.mods)
        # Avoid the outer wrapper's build.log while testing its MSBuild deployment integration.
        command = ["dotnet", "build", "Source/SheepHappens.csproj", "-c", "Release",
                   "--no-restore", "--disable-build-servers", "-v:q"]
        if package_only:
            command += ["-t:PackageMod", "-p:RIMWORLD_MOD_DIR=", "-p:BuildBridgeTools=false"]
        return subprocess.run(command, cwd=workflow.ROOT, env=env, capture_output=True, text=True)

    @unittest.skipIf(os.name == "nt", "The native shell entry point requires bash.")
    def test_normal_build_installs_and_creates_zip(self):
        result = self.native_build()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertTrue(self.destination.is_dir())
        self.assert_matches_zip()
        workflow.verify_legacy()

    @unittest.skipIf(os.name == "nt", "The process-list fixture requires a POSIX shell.")
    def test_normal_build_reports_running_game_without_installing(self):
        result = self.native_build(game_running=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("RimWorld is running", result.stdout + result.stderr)
        self.assertFalse(self.destination.exists())

    @unittest.skipIf(os.name == "nt", "The process-list fixture requires a POSIX shell.")
    def test_package_build_ignores_inherited_root_and_running_game(self):
        result = self.native_build(game_running=True, package_only=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(self.destination.exists())
        self.assertFalse((self.mods / "SheepHappens.zip").exists())


class ProcessGuardTests(unittest.TestCase):
    @unittest.skipIf(os.name == "nt", "These fixtures use ps output.")
    def test_game_executables_are_detected(self):
        for name in ("RimWorld", "RimWorldLinux", "RimWorldWin64.exe", "RimWorld by Ludeon Studios"):
            with self.subTest(name=name):
                result = subprocess.CompletedProcess([], 0, f"123 /games/{name}\n", "")
                with patch.object(workflow.subprocess, "run", return_value=result):
                    with self.assertRaisesRegex(ValueError, "RimWorld is running"):
                        workflow.ensure_stopped()

    def test_failed_process_inspection_refuses_deployment(self):
        result = subprocess.CompletedProcess([], 1, "", "inspection failed")
        with patch.object(workflow.subprocess, "run", return_value=result):
            with self.assertRaisesRegex(ValueError, "Cannot determine"):
                workflow.ensure_stopped()


if __name__ == "__main__":
    unittest.main()
