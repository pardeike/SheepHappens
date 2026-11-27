# Project workflow

- Run `./scripts/build-quiet.sh -c Release` for the normal build, matching BridgeTools build, guarded install, and verified ZIP.
- If `RIMWORLD_MOD_DIR` is unset or empty, the normal workflow only builds locally.
- Set `RIMWORLD_MOD_DIR` to the existing RimWorld 1.7 game-root Mods directory for deployment.
- Add `--package` for a public package without deployment or companion build.
- Add `--check` to build both DLLs, package, and run isolated workflow tests without using the configured Mods directory.
- Read `scripts/README.md` for the published SDK 2.0.0 dependency and exact interfaces.
- Keep `1.2/`, `1.3/`, `1.4/`, `1.6/`, root media, and existing artwork unchanged. Build current code into `1.7/`.
- The repository's 1.6 DLL matches the published baseline. Stage older payloads from the checksum-pinned published v1.7.0.0 ZIP.
- Never replace existing frozen files during deployment. Reject missing, differing, or additional older payload files.
- Retain the published annotation DLL only under frozen `1.6/Assemblies/`. Do not remove it during companion extraction.
- Keep SDK and annotation assemblies out of the active 1.7 payload. Keep companion DLLs out of the public package.
- The MBA extraction ref supplies source and project intent only. Never cherry-pick its 1.6 binary changes.
- Deployment refuses a running game. The workflow never starts or stops the game.
