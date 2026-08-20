#!/usr/bin/env bash
# Fetches the bundled qpdf binary for one target into a build output directory.
#
# Binaries are not kept in git. They are built once per qpdf version by the build-qpdf
# workflow and published as assets on a `qpdf-<version>` tag; this script downloads them
# by pinned version. See docs/features/done/0003-bundled-qpdf.md.
set -euo pipefail

RID="${1:?usage: fetch-qpdf.sh <rid> <destination-dir> [version]}"
DEST="${2:?usage: fetch-qpdf.sh <rid> <destination-dir> [version]}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${3:-$(tr -d '[:space:]' < "$ROOT/qpdf-version.txt")}"
REPO="${QPDF_ASSET_REPO:-}"
CACHE="${QPDF_CACHE:-$ROOT/.qpdf-cache}"

case "$RID" in
  osx-arm64|osx-x64) BINARY="qpdf" ;;
  win-x64)           BINARY="qpdf.exe" ;;
  *) echo "fetch-qpdf: unsupported target '$RID'" >&2; exit 2 ;;
esac

ASSET="qpdf-$VERSION-$RID.tar.gz"
mkdir -p "$CACHE" "$DEST"

if [[ ! -f "$CACHE/$ASSET" ]]; then
  if [[ -z "$REPO" ]]; then
    # A missing bundle is not a build failure: the app resolves a system qpdf and, failing
    # that, shows its setup banner. Refusing to build would make every local build depend
    # on network access.
    echo "fetch-qpdf: no cached $ASSET and QPDF_ASSET_REPO is unset — skipping bundle." >&2
    exit 0
  fi
  echo "fetch-qpdf: downloading $ASSET from $REPO"
  gh release download "qpdf-$VERSION" --repo "$REPO" --pattern "$ASSET" --dir "$CACHE"
fi

tar -xzf "$CACHE/$ASSET" -C "$DEST"
chmod +x "$DEST/$BINARY" 2>/dev/null || true
echo "fetch-qpdf: placed $BINARY in $DEST"
