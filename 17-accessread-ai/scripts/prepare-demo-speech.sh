#!/usr/bin/env bash
# Fetch through the host proxy; APT verifies signed Release metadata and package hashes.
set -euo pipefail
cd "$(dirname "$0")/.."
cache=/workspace/.cache/accessread-apt
packages=/workspace/.cache/accessread-debs
mkdir -p "$cache/lists/partial" "$cache/archives/partial" "$cache/config" "$packages" artifacts/speech
cat > "$cache/sources.list" <<'SOURCES'
deb [signed-by=/usr/share/keyrings/debian-archive-keyring.gpg] https://deb.debian.org/debian trixie main
SOURCES
apt_options=(-o Dir::Etc::main=/dev/null -o "Dir::Etc::parts=$cache/config" -o "Dir::Etc::sourcelist=$cache/sources.list" -o Dir::Etc::sourceparts=- -o "Dir::State::lists=$cache/lists" -o "Dir::Cache::archives=$cache/archives" -o APT::Sandbox::User="$(id -un)")
apt-get "${apt_options[@]}" -o DPkg::Post-Invoke::= -o APT::Update::Post-Invoke-Success::= update
(
 cd "$packages"
 apt-get "${apt_options[@]}" -o DPkg::Post-Invoke::= download espeak-ng=1.52.0+dfsg-5 espeak-ng-data=1.52.0+dfsg-5 libespeak-ng1=1.52.0+dfsg-5 libpcaudio0 libsonic0 libasound2t64 libpulse0=17.0+dfsg1-2+b1 libasyncns0 libdbus-1-3 libsndfile1 libflac14 libogg0 libvorbis0a libvorbisenc2 libvorbisfile3 libopus0 libmpg123-0t64 libmp3lame0 libx11-6 libx11-xcb1 libxcb1 libxau6 libxdmcp6 libsystemd0=257.13-1~deb13u1
)
for package in "$packages"/*.deb; do dpkg-deb -x "$package" artifacts/speech; done
chmod -R a+rX artifacts/speech
mkdir -p artifacts/certs
cp /etc/ssl/certs/ca-certificates.crt artifacts/certs/ca-certificates.crt
