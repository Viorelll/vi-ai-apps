#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
folder=artifacts/native
mkdir -p "$folder"
for library in libgssapi_krb5.so.2 libkrb5.so.3 libk5crypto.so.3 libcom_err.so.2 libkrb5support.so.0 libkeyutils.so.1; do
  source="/lib/x86_64-linux-gnu/$library"
  if [ ! -e "$source" ]; then source="/usr/lib/x86_64-linux-gnu/$library"; fi
  if [ ! -e "$source" ]; then printf 'Required runtime library %s is unavailable on the host.\n' "$library" >&2; exit 1; fi
  cp -L "$source" "$folder/$library"
done
chmod 755 "$folder"/*
