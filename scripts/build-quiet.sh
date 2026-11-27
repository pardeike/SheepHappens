#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
export PYTHONDONTWRITEBYTECODE=1
mkdir -p .builds
log="$root/.builds/build.log"
exec 3>&1
exec >"$log" 2>&1
step="verify frozen payload"
trap 'status=$?; if (( status != 0 )); then printf "Failed: %s (exit %s). Log: %s\n" "$step" "$status" "$log" >&3; tail -25 "$log" >&3; fi' EXIT
build_pid=""
cancel() {
    if [[ -n "$build_pid" ]]; then
        kill -TERM "$build_pid" 2>/dev/null || true
        wait "$build_pid" 2>/dev/null || true
    fi
    exit "$1"
}
trap 'cancel 130' INT
trap 'cancel 143' TERM

package=false
check=false
args=(build Source/SheepHappens.csproj -v:q --disable-build-servers)
for arg in "$@"; do
    if [[ "$arg" == "--check" ]]; then
        check=true
        package=true
    elif [[ "$arg" == "--package" ]]; then
        package=true
    else
        if [[ "$(printf '%s' "$arg" | tr '[:upper:]' '[:lower:]')" == *rimworld_mod_dir* ]]; then
            printf 'Set RIMWORLD_MOD_DIR in the environment, not as an MSBuild argument.\n' >&2
            exit 2
        fi
        args+=("$arg")
    fi
done
python3 scripts/rimworld_deploy_guard.py verify-legacy
step="build 1.7"
if "$package"; then
    step="build and package 1.7"
    args+=(-t:PackageMod -p:RIMWORLD_MOD_DIR=)
    if ! "$check"; then args+=(-p:BuildBridgeTools=false); fi
elif [[ -n "${RIMWORLD_MOD_DIR:-}" ]]; then
    step="build, deploy and verify package"
fi
python3 - "${args[@]}" <<'PY' &
import os
import signal
import subprocess
import sys

process = subprocess.Popen(["dotnet", *sys.argv[1:]], start_new_session=True)

def cancel(signum, frame):
    try:
        os.killpg(process.pid, signum)
    except ProcessLookupError:
        pass

signal.signal(signal.SIGINT, cancel)
signal.signal(signal.SIGTERM, cancel)
status = process.wait()
sys.exit(status if status >= 0 else 128 - status)
PY
build_pid=$!
wait "$build_pid"
build_pid=""
if "$check"; then
    step="check packaging and isolated deployment"
    python3 -m unittest discover -s scripts -p test_workflow.py
fi
step="verify frozen payload after build"
python3 scripts/rimworld_deploy_guard.py verify-legacy
printf 'ok\n' >&3
