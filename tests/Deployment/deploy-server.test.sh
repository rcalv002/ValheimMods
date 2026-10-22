#!/usr/bin/env bash
# Offline integration tests. Docker is replaced with a fixture; no server is touched.
set -euo pipefail
repo=$(cd -- "$(dirname -- "$0")/../.." && pwd -P)
fixture=$(mktemp -d)
trap 'rm -rf -- "$fixture"' EXIT
mkdir -p "$fixture/bin" "$fixture/staged" "$fixture/stack/config/bepinex" \
    "$fixture/stack/data/bepinex/BepInEx/core"
export DEPLOY_TEST_STACK="$fixture/stack"
export DEPLOY_TEST_CALLS="$fixture/docker-calls"
export DEPLOY_TEST_INSTALL
DEPLOY_TEST_INSTALL=$(command -v install)
export PATH="$fixture/bin:$PATH"
cat > "$fixture/bin/docker" <<'MOCK'
#!/usr/bin/env bash
set -eu
echo "$*" >> "$DEPLOY_TEST_CALLS"
case "$*" in
    'compose ps --status running -q valheim') echo test-container ;;
    'compose up -d valheim')
        if [[ -f "$DEPLOY_TEST_STACK/fail-start" ]]; then
            rm "$DEPLOY_TEST_STACK/fail-start"
            exit 1
        fi
        if [[ -f "$DEPLOY_TEST_STACK/change-runtime" ]]; then
            rm "$DEPLOY_TEST_STACK/change-runtime"
            echo changed > "$DEPLOY_TEST_STACK/data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll"
        fi
        ;;
esac
MOCK
chmod +x "$fixture/bin/docker"
cat > "$fixture/bin/install" <<'MOCK'
#!/usr/bin/env bash
set -eu
if [[ ${!#} == 'data/bepinex/BepInEx/plugins/ValhallaMeter/ValhallaMeter.Core.dll' &&
      -f "$DEPLOY_TEST_STACK/fail-install" ]]; then
    rm "$DEPLOY_TEST_STACK/fail-install"
    exit 1
fi
exec "$DEPLOY_TEST_INSTALL" "$@"
MOCK
chmod +x "$fixture/bin/install"
printf 'BEPINEX=true\n' > "$fixture/stack/.env"
touch "$fixture/stack/data/bepinex/BepInEx/core/BepInEx.dll"
plugins=(ValhallaMeter/ValhallaMeter.dll ValhallaMeter/ValhallaMeter.Core.dll TargetCall/TargetCall.dll)
roots=(config/bepinex/plugins data/bepinex/BepInEx/plugins)
for plugin in "${plugins[@]}"; do
    echo "release $plugin" > "$fixture/staged/${plugin##*/}"
done

# Missing input must fail before downtime or destination creation.
mv "$fixture/staged/TargetCall.dll" "$fixture/staged/TargetCall.saved"
if bash "$repo/scripts/deploy-server.sh" "$fixture/staged" "$fixture/stack"; then exit 1; fi
! grep -q 'compose stop' "$DEPLOY_TEST_CALLS"
mv "$fixture/staged/TargetCall.saved" "$fixture/staged/TargetCall.dll"

# Reproduce the incident: persistent DLL exists, active plugin directory does not.
mkdir -p "$fixture/stack/config/bepinex/plugins/TargetCall"
echo old > "$fixture/stack/config/bepinex/plugins/TargetCall/TargetCall.dll"
bash "$repo/scripts/deploy-server.sh" "$fixture/staged" "$fixture/stack"
for root in "${roots[@]}"; do
    for plugin in "${plugins[@]}"; do
        cmp "$fixture/staged/${plugin##*/}" "$fixture/stack/$root/$plugin"
    done
done

# Repeated deployments should preserve matching DLLs in both locations.
bash "$repo/scripts/deploy-server.sh" "$fixture/staged" "$fixture/stack"

# Failed restart restores old files and removes DLLs absent before deployment.
rm "$fixture/stack/data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll"
echo previous > "$fixture/stack/config/bepinex/plugins/TargetCall/TargetCall.dll"
touch "$fixture/stack/fail-start"
if bash "$repo/scripts/deploy-server.sh" "$fixture/staged" "$fixture/stack"; then exit 1; fi
grep -qx previous "$fixture/stack/config/bepinex/plugins/TargetCall/TargetCall.dll"
test ! -e "$fixture/stack/data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll"

# A partial copy failure restores destinations already written in this run.
echo old-meter > "$fixture/stack/config/bepinex/plugins/ValhallaMeter/ValhallaMeter.dll"
touch "$fixture/stack/fail-install"
if bash "$repo/scripts/deploy-server.sh" "$fixture/staged" "$fixture/stack"; then exit 1; fi
grep -qx old-meter "$fixture/stack/config/bepinex/plugins/ValhallaMeter/ValhallaMeter.dll"
grep -qx previous "$fixture/stack/config/bepinex/plugins/TargetCall/TargetCall.dll"
test ! -e "$fixture/stack/data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll"

# A container rewriting the runtime copy is detected and rolled back too.
touch "$fixture/stack/change-runtime"
if bash "$repo/scripts/deploy-server.sh" "$fixture/staged" "$fixture/stack"; then exit 1; fi
grep -qx previous "$fixture/stack/config/bepinex/plugins/TargetCall/TargetCall.dll"
test ! -e "$fixture/stack/data/bepinex/BepInEx/plugins/TargetCall/TargetCall.dll"
echo 'PASS: preflight, dual-path installation, repeat deployment, copy/start/hash failure rollback'
