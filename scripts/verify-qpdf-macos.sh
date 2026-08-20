#!/usr/bin/env bash
# Fails if a macOS qpdf binary links anything outside the system libraries.
#
# A qpdf built the ordinary way links Homebrew's libqpdf, libjpeg and zlib, and therefore
# runs only on the machine that built it. This check is the difference between a bundle
# that works everywhere and one that works here.
set -euo pipefail

BINARY="${1:?usage: verify-qpdf-macos.sh <path-to-qpdf>}"

echo "== $BINARY"
"$BINARY" --version

BAD=$(otool -L "$BINARY" | tail -n +2 | awk '{print $1}' \
      | grep -Ev '^(/usr/lib/|/System/Library/)' || true)

if [[ -n "$BAD" ]]; then
  echo "FAIL: links non-system libraries:" >&2
  echo "$BAD" >&2
  exit 1
fi

# Apple Silicon refuses to execute an unsigned binary outright, so a signature — even an
# ad-hoc one — is mandatory, not cosmetic.
if ! codesign -dv "$BINARY" 2>&1 | grep -q 'Signature'; then
  echo "FAIL: no code signature; Apple Silicon will kill this on launch." >&2
  exit 1
fi

echo "OK: system libraries only, and signed."
