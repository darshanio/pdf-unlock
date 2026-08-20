# 0007 — CI and releases

**Status:** not started
**Depends on:** [0003](done/0003-bundled-qpdf.md), [0004](0004-macos-app-bundle.md),
[0008](0008-windows-installer.md)
**Blocks:** [0006](0006-update-notification.md)

## Why

Building and packaging by hand on one machine is how release artifacts end up
unreproducible and platform-specific bugs go unnoticed.

## Acceptance criteria

1. `ci.yml` runs on pull requests and pushes to `main`: restore, build, and test on both
   macOS and Windows runners. It does not publish anything.
2. `release.yml` runs on a pushed tag matching `v*`. Merges to `main` do **not** publish
   a release — a typo fix should not consume a version number.
3. The release workflow downloads the pinned qpdf assets from
   [0003](done/0003-bundled-qpdf.md) rather than building qpdf, keeping releases fast.
4. Artifacts: a `.dmg` for macOS (ad-hoc signed) and an Inno Setup installer for
   Windows, both attached to the GitHub release.
5. Release notes are auto-generated from merged pull request titles, grouped by label
   via `.github/release.yml`.
6. A release whose version does not match the tag fails the build rather than shipping
   mislabelled artifacts.
7. Any coverage the pipeline deliberately skips is logged in the job summary, so a
   truncated run cannot read as a complete one.

## Edge cases

- Tag pushed on a branch that is not `main`: rejected, with the reason.
- Re-running a release workflow for an existing tag: must not create a duplicate
  release or silently overwrite assets.
- macOS runner architecture: the `osx-arm64` and `osx-x64` builds must both be produced
  regardless of which runner architecture GitHub provides.

## Verification

Tag a `v0.0.1-test` pre-release and confirm both artifacts appear, install, and run on
a clean machine. Confirm a push to `main` produces no release.

## Out of scope

Code signing certificates and notarisation, both of which cost money and were declined
during design. Windows signing can be added later without any code change.
