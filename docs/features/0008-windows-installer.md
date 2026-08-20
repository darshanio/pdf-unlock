# 0008 — Windows installer and context menu

**Status:** not started
**Depends on:** [0003](done/0003-bundled-qpdf.md)
**Blocks:** [0007](0007-ci-and-releases.md)

## Why

Windows shell integration cannot be done from a portable zip: the context-menu verb is
registry state that something must create, and remove again on uninstall.

## Acceptance criteria

1. An Inno Setup installer installs a self-contained build, so no .NET is required.
2. It writes a **Decrypt with PDF Unlock** verb under
   `HKCU\Software\Classes\SystemFileAssociations\.pdf\shell`, per-user so the installer
   does not require administrator rights.
3. Uninstall removes the verb, the application, and its own registry keys. It leaves the
   user's settings and saved passwords alone unless the user asks for those too.
4. The verb passes the selected files to the app as arguments. Selecting several PDFs
   must produce one window with several jobs, not several windows.
5. The installer sets the application icon and Add/Remove Programs metadata: publisher,
   version, and an uninstall entry with the real icon.
6. Upgrading over an existing installation preserves settings and saved passwords.
7. The unsigned installer's SmartScreen warning is documented in the README and release
   notes rather than left to surprise the user.

## Edge cases

- Path with spaces or non-ASCII characters: the registry command must quote correctly,
  which is the classic failure here.
- Very large selection: Windows caps command line length. Above a threshold the verb
  must hand over a temporary file list rather than truncating silently.
- App already running: the new files append to the existing batch.

## Verification

Install on a clean Windows machine without .NET or qpdf; right-click three PDFs from a
path containing a space and confirm one window with three jobs. Uninstall and confirm
the registry keys are gone.

## Out of scope

Per-machine installation, MSI/WiX packaging, and an `IExplorerCommand` shell extension
for a modern Windows 11 context-menu entry.
