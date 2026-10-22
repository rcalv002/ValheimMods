#!/usr/bin/env bash
# Deploy staged server DLLs to this stack's persistent and active BepInEx trees.
set -Eeuo pipefail

if (( $# < 1 || $# > 2 )); then
    echo "Usage: bash $0 SOURCE_DIRECTORY [STACK_DIRECTORY]" >&2
    exit 2
fi
source_dir=$(cd -- "$1" && pwd -P)
stack=$(cd -- "${2:-/dockerdata/valheim}" && pwd -P)
cd -- "$stack"

grep -Eq '^BEPINEX=(true|1)\r?$' .env || {
    echo 'Enable BEPINEX=true in the stack .env before deploying.' >&2; exit 1;
}
test -f data/bepinex/BepInEx/core/BepInEx.dll || {
    echo 'Active BepInEx runtime not found. Start the BepInEx-enabled server once first.' >&2; exit 1;
}
docker compose config --quiet

plugins=(ValhallaMeter/ValhallaMeter.dll ValhallaMeter/ValhallaMeter.Core.dll TargetCall/TargetCall.dll)
roots=(config/bepinex/plugins data/bepinex/BepInEx/plugins)
for plugin in "${plugins[@]}"; do
    test -s "$source_dir/${plugin##*/}" || {
        echo "Missing staged DLL: $source_dir/${plugin##*/}" >&2; exit 1;
    }
done

backup=$(mktemp -d "$stack/config/bepinex/deploy-backup-XXXXXXXX")
echo "Backup: $backup"
targets=()
existed=()
hashes=()
# Snapshot sources before stopping; this also supports staging from an installed tree.
for plugin in "${plugins[@]}"; do
    install -D -m 644 -- "$source_dir/${plugin##*/}" "$backup/source/$plugin"
    hash=$(sha256sum -- "$backup/source/$plugin")
    hashes+=("${hash%% *}")
done
for root in "${roots[@]}"; do
    for plugin in "${plugins[@]}"; do
        target="$root/$plugin"
        # Avoid overwriting through a file symlink or backing up a directory as a DLL.
        if [[ -L "$target" || ( -e "$target" && ! -f "$target" ) ]]; then
            echo "Expected a regular DLL or missing file: $target" >&2; exit 1
        fi
        targets+=("$target")
        if [[ -f "$target" ]]; then
            existed+=(1)
            mkdir -p -- "$backup/previous/$(dirname -- "$target")"
            cp -p -- "$target" "$backup/previous/$target"
        else
            existed+=(0)
        fi
    done
done
printf '%s\n' "${targets[@]}" > "$backup/targets.txt"
printf '%s\n' "${existed[@]}" > "$backup/existed.txt"
was_running=$(docker compose ps --status running -q valheim)
touched=0

rollback() {
    local status=$1 i
    trap - ERR INT TERM
    set +e
    echo "Deployment failed; restoring DLLs from $backup" >&2
    # A failed start or post-start hash check can leave the new server running.
    if (( touched > 0 )); then
        if ! docker compose stop -t 120 valheim; then
            echo 'Could not stop server; restore the backup manually before restarting.' >&2
            exit "$status"
        fi
    fi
    for (( i=0; i<touched; i++ )); do
        if [[ ${existed[i]} == 1 ]]; then
            cp -p -- "$backup/previous/${targets[i]}" "${targets[i]}" || exit "$status"
        else
            rm -f -- "${targets[i]}" || exit "$status"
        fi
    done
    if [[ -n "$was_running" ]]; then docker compose up -d valheim; fi
    exit "$status"
}
trap 'rollback $?' ERR
trap 'rollback 130' INT
trap 'rollback 143' TERM

docker compose stop -t 120 valheim
for i in "${!targets[@]}"; do
    plugin=${plugins[i % ${#plugins[@]}]}
    touched=$((i + 1))
    install -D -m 644 -- "$backup/source/$plugin" "${targets[i]}"
done

verify() {
    local i actual expected
    for i in "${!targets[@]}"; do
        actual=$(sha256sum -- "${targets[i]}") || return 1
        expected=${hashes[i % ${#plugins[@]}]}
        if [[ ${actual%% *} != "$expected" ]]; then
            echo "Installed hash mismatch: ${targets[i]}" >&2
            return 1
        fi
    done
}
verify
docker compose up -d valheim
verify
trap - ERR INT TERM
echo 'Verified server DLLs in persistent config and active data BepInEx paths.'
echo 'Startup is asynchronous. Confirm both plugins loaded in:'
echo "$stack/data/bepinex/BepInEx/LogOutput.log"
