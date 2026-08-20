# 0003 — Bundled qpdf

**Status:** not started
**Depends on:** [0001](done/0001-settings-shell.md)
**Blocks:** [0004](0004-macos-app-bundle.md), [0007](0007-ci-and-releases.md)

## Why

Resolution already prefers a system qpdf, but its last candidate — the bundled copy —
does not exist yet, so a machine without qpdf cannot use the app at all. qpdf publishes
no official macOS binary, so for many Mac users there is no easy route to a system
installation. See [ADR 0001](../adr/0001-qpdf-resolution-strategy.md).

## Scope

Produce static qpdf binaries, ship them inside the app, and expose the resolved
installation in Settings → qpdf.

## Acceptance criteria

1. A separate, manually-triggered `build-qpdf` workflow builds **statically linked**
   qpdf for `osx-arm64` and `osx-x64`, and downloads the official `msvc64` archive for
   `win-x64`. It publishes them as assets on a `qpdf-<version>` tag.
2. Binaries do not enter git history. The application build downloads them by pinned
   version.
3. The bundled binary lands at `qpdf/qpdf` (`qpdf\qpdf.exe`) beside the executable,
   which is where `QpdfResolver` already looks.
4. Each macOS binary is verified to have no non-system dynamic dependencies — a bundle
   that links Homebrew's `libqpdf` works only on the build machine.
5. Bundled binaries are ad-hoc signed, without which Apple Silicon refuses to execute
   them at all.
6. Settings → qpdf shows the resolved installation: ✓/✗, path, version, and which of
   the four mechanisms found it, distinguishing *bundled* from *system* explicitly.
7. **Change…** accepts a path and re-runs resolution against it; a chosen path that is
   too old or not qpdf is rejected with the reason, and the previous resolution stands.
8. Bumping the bundled qpdf version requires no application code change.

## Edge cases

- Bundled binary missing from a build: the app must still start and show the setup
  banner rather than crashing at launch.
- User chooses a path that is a wrapper script rather than a binary: accepted if
  `--version` reports a usable qpdf.
- Rosetta: an `osx-x64` bundle running on Apple Silicon is valid and must not be
  rejected.

## Verification

On a machine with Homebrew's qpdf removed from `PATH`, the app resolves the bundle and
decrypts successfully. `otool -L` on each macOS binary shows only system libraries.
Version reported in Settings matches `qpdf --version` for that binary.

## Out of scope

Automatic installation via `brew` or `winget`. It was only load-bearing while there was
no bundle; with one, it is optional polish.
