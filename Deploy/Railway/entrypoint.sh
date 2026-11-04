#!/bin/sh
set -eu
join_code="${PWA_JOIN_CODE:-}"
if [ "${#join_code}" -lt 12 ]; then
    echo 'Set PWA_JOIN_CODE to a private code with at least 12 characters.' >&2
    exit 1
fi
if [ "$(id -u)" = 0 ]; then
    mkdir -p /data
    chown game:game /data
    exec gosu game "$0" "$@"
fi
# Both processes must stay alive. Forward termination and restart on either failure.
unity_pid=''
proxy_pid=''
cleanup() {
    trap - TERM INT EXIT
    [ -z "$unity_pid" ] || kill -TERM "$unity_pid" 2>/dev/null || true
    [ -z "$proxy_pid" ] || kill -TERM "$proxy_pid" 2>/dev/null || true
    [ -z "$unity_pid" ] || wait "$unity_pid" 2>/dev/null || true
    [ -z "$proxy_pid" ] || wait "$proxy_pid" 2>/dev/null || true
}
trap cleanup TERM INT EXIT
/app/Server/PwaServer -batchmode -nographics -logFile - -pwa-server &
unity_pid=$!
caddy run --config /app/Caddyfile --adapter caddyfile &
proxy_pid=$!
while kill -0 "$unity_pid" 2>/dev/null && kill -0 "$proxy_pid" 2>/dev/null; do sleep 1; done
exit 1
