#!/bin/sh
set -eu
exec dotnet "$CONTRACTAI_APPLICATION" "$@"
