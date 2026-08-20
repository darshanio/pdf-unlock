# 0004 — macOS app bundle and Quick Action

**Status:** not started
**Depends on:** [0003](done/0003-bundled-qpdf.md)
**Blocks:** [0007](0007-ci-and-releases.md)

## Why

Right now the app runs only from `dotnet run`. It has no dock icon, no name of its own
in the menu bar, and no way to be invoked from Finder — which is half the point of the
tool.

## Scope

A `.app` bundle, distributed in a `.dmg`, ad-hoc signed, with a Finder context-menu
entry.

## Acceptance criteria

1. `PDF Unlock.app` contains a self-contained build, so a user needs no .NET installed.
2. `Info.plist` sets `CFBundleName` "PDF Unlock", the bundle identifier, the version,
   and `CFBundleIconFile` pointing at `pdf-unlock.icns`. The menu bar shows "PDF Unlock",
   not "Avalonia Application", and the dock shows the real icon.
3. The bundle declares a PDF document type so **Open With → PDF Unlock** appears in
   Finder, and an `NSServices` entry so the app appears under right-click → Services.
   The design accepts that macOS nests this one level; a top-level Finder verb is not
   available to a non-extension app.
4. Files opened this way reach the running app as a batch — including when the app is
   already open, in which case they append rather than opening a second window.
5. The app **and** the bundled qpdf are ad-hoc signed, with the deep flag, so Apple
   Silicon will execute them.
6. A `.dmg` is produced containing the app and an Applications symlink.
7. First launch on another machine is documented: Gatekeeper will warn because the app
   is not notarised. `xattr -dr com.apple.quarantine`, or right-click → Open, is stated
   in the README and release notes.

## Edge cases

- Multiple files selected in Finder: macOS may deliver them as several open events in
  quick succession. They must coalesce into one batch, not one batch each.
- App launched with no files: normal empty state, not an error.
- Quarantine attribute on the bundled qpdf: must be cleared or avoided, or qpdf will be
  killed on first run even though the app itself opened.

## Verification

Install from the `.dmg` on a machine without .NET and without qpdf; decrypt a file.
Confirm dock icon, menu bar name, Services entry, and that selecting four PDFs produces
one window with four jobs. Confirm `codesign -dv` reports an ad-hoc signature.

## Out of scope

Notarisation, and a Finder Sync extension for a top-level context-menu verb.
