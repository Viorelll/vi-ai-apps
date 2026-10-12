#!/usr/bin/env bash
set -euo pipefail

app_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build_dir="$app_root/.build"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT
mkdir -p "$build_dir"

architecture="$(uname -m)"
case "$architecture" in
  x86_64) go_arch="amd64" ;;
  aarch64|arm64) go_arch="arm64" ;;
  *) echo "Unsupported build architecture: $architecture" >&2; exit 1 ;;
esac

metadata="$tmp_dir/go-releases.json"
curl --fail --silent --show-error --location \
  'https://go.dev/dl/?mode=json&include=all' -o "$metadata"
read -r go_version archive_sha < <(python3 - "$metadata" "$go_arch" <<'PY'
import json
import re
import sys

metadata_path, architecture = sys.argv[1:]
versions = json.load(open(metadata_path, encoding="utf-8"))
candidates = [
    item for item in versions
    if re.fullmatch(r"go1\.25(?:\.\d+)?", item["version"])
]
candidates.sort(key=lambda item: tuple(map(int, item["version"][2:].split("."))), reverse=True)
if not candidates:
    raise SystemExit("No stable Go 1.25 release was listed by go.dev")
release = candidates[0]
filename = f"{release['version']}.linux-{architecture}.tar.gz"
archive = next(file for file in release["files"] if file["filename"] == filename)
print(release["version"], archive["sha256"])
PY
)
archive="$tmp_dir/$go_version.tar.gz"
curl --fail --silent --show-error --location "https://go.dev/dl/$go_version.linux-$go_arch.tar.gz" -o "$archive"
printf '%s  %s\n' "$archive_sha" "$archive" | sha256sum --check --status
mkdir "$tmp_dir/sdk"
tar -xzf "$archive" -C "$tmp_dir/sdk"

GOBIN="$build_dir" GOMODCACHE="$tmp_dir/gomodcache" GOCACHE="$tmp_dir/gocache" GOTOOLCHAIN=local "$tmp_dir/sdk/go/bin/go" install \
  github.com/minio/minio@RELEASE.2025-10-15T17-29-55Z
GOOS=linux GOARCH="$go_arch" CGO_ENABLED=0 GOMODCACHE="$tmp_dir/gomodcache" GOCACHE="$tmp_dir/gocache" GOTOOLCHAIN=local \
  "$tmp_dir/sdk/go/bin/go" build -trimpath -o "$build_dir/minio-healthcheck" \
  "$app_root/deploy/minio-healthcheck.go"
install -m 0644 /etc/ssl/certs/ca-certificates.crt "$build_dir/ca-certificates.crt"
