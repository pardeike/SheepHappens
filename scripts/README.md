# Build, package, and install

Run `./scripts/build-quiet.sh -c Release` for the normal workflow.
With `RIMWORLD_MOD_DIR` unset or empty, it builds the main DLL and matching companion locally.
With `RIMWORLD_MOD_DIR` set, it also installs the pair and creates a verified public ZIP.
The supplied directory must already exist and must logically and physically be named Mods.

```sh
RIMWORLD_MOD_DIR="/path/to/RimWorld/Mods" ./scripts/build-quiet.sh -c Release
```

The paired destinations derive from that single root:

- `Mods/SheepHappens/1.7/Assemblies/SheepHappens.dll`
- `BridgeTools/SheepHappens/SheepHappens.BridgeTools.dll`, beside the selected Mods directory
- `Mods/SheepHappens.zip`, verified against the complete installed public mod

On macOS, use the RimWorld 1.7 game-root Mods directory.
The guard rejects the old app-bundle alias and an ambiguous UserData alias that would separate the companion from its host.
It rejects symlinked destinations and symlinks inside installed payloads.
A global operating-system lock uses the physical Mods path and mod name to serialize deployment across checkouts.
The guard checks that RimWorld is stopped while holding the lock.
This workflow never starts or stops the game.

Both projects target .NET 10 and AnyCPU with SDK 10.0.400.
The main project automatically builds the companion after the main DLL exists.
Local outputs are `1.7/Assemblies/` and `artifacts/BridgeTools/SheepHappens/`.
An ordinary `dotnet build Source/SheepHappens.csproj -c Release` uses the same targets.
Pass `RIMWORLD_MOD_DIR` through the environment when using the quiet wrapper.

## Published SDK dependency

The companion restores the published `RimBridgeServer.Sdk` 2.0.0 package from NuGet.org.
The companion references the SDK with `PrivateAssets="all"` and `ExcludeAssets="runtime"`.
RimBridgeServer supplies the runtime SDK assembly.
The companion output and deployed bundle must contain only `SheepHappens.BridgeTools.dll`.

The compatible dependencies checked on 2026-10-31 are Krafs 1.7.4990-unstable, Harmony references 3.0.0-rc.4, CrossPromotion 1.1.2, TaskPubliciser 1.0.3, and the published SDK 2.0.0.

## Local package and checks

Run `./scripts/build-quiet.sh -c Release --package` for a public package without deployment.
This mode clears inherited deployment settings and sets `BuildBridgeTools=false`.
It skips the companion build and does not inspect or change game state.
Outputs are `.builds/package/SheepHappens/` and `.builds/package/SheepHappens.zip`.
The public ZIP contains no new companion or SDK DLL.
The direct MSBuild equivalent is:

```sh
dotnet build Source/SheepHappens.csproj -c Release -t:PackageMod -p:RIMWORLD_MOD_DIR= -p:BuildBridgeTools=false
```

Run `./scripts/build-quiet.sh -c Release --check` to build both DLLs, package, and run isolated workflow tests.
This mode clears the configured deployment root.
Tests use disposable Mods and sibling BridgeTools directories under `.builds/`.
The native integration tests simulate process lists and cover running-game refusal separately.
They do not launch, stop, or connect to the game.

All quiet commands print `ok` only after every requested step and required verification succeeds.
Full output goes to `.builds/build.log`.
Failures identify the failed step, diagnostic, and log path.
Cancellation stops the active build process and its children.

## Frozen published payload

The published Steam payload matches the GitHub v1.7.0.0 release ZIP byte-for-byte.
The repository's 1.6 main DLL matches that baseline. Its generated README differs.
The repository also retains older version folders that the current public package does not contain.
Never publish a raw copy of the checkout.

Packaging uses the published archive with SHA-256 `963ea6e02e4eebfa77d37ba9ff19501b855bd260d366f792f38a35cf2be57573`.
The first package operation downloads it into `.builds/published-v1.7.0.0.zip`.
An unavailable or mismatched archive stops packaging.
Only About metadata, load folders, and README are replaced with current metadata or documentation.
All other published files remain byte-for-byte unchanged in staging, installation, and ZIP.
`scripts/frozen-payload.json` guards the original repository's older DLLs, defs, media, symbols, and artwork before and after builds.
Neither packaging nor installation writes published bytes over those repository files.

For a fresh installation, the complete staged public payload is installed through one directory rename.
For an existing installation, every frozen published file must match before any installed payload changes.
The installer rejects missing, differing, or additional older files.
It never rewrites, replaces, or deletes matching frozen files.
Changed current files and the ZIP use atomic replacement.
Verification compares installed bytes, staging, ZIP, and the matching companion.

The published `1.6/Assemblies/RimBridgeServer.Annotations.dll` remains frozen.
Removing it would change the published 1.6 payload.
The 1.7 DLL has no bridge dependency, and its development tools live only in the separate companion.
RimWorld 1.7 loads the unchanged root media and `1.7/`; it does not load `1.6/`.

The MBA ref `refs/mba/branches/rimbridge-extraction` supplied the source move, SDK namespace change, and public Constants class.
Its 1.6 binary changes were not applied.
Live startup, new-game validation, and companion discovery remain untested until the runtime owner checks them.
