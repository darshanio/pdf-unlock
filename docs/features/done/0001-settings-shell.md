# 0001 — Settings shell

**Status:** done — verified 2026-08-20
**Depends on:** nothing
**Blocks:** [0002](../0002-folder-password-rules.md), [0003](../0003-bundled-qpdf.md),
[0006](../0006-update-notification.md)

## Why

Four unrelated things need somewhere to live: the qpdf status block, the folder-rule
list, dependency licences, and the context-menu behaviour toggle. Building them each
their own window would produce four inconsistent surfaces. This feature is the frame
they hang in, and nothing more.

## Scope

A settings window with a left navigation rail and four sections: **General**,
**Passwords**, **qpdf**, **Licences**. Sections may push a sub-page (Passwords does),
which needs a back affordance.

## Acceptance criteria

1. Settings opens from the main window and is modal to it — a batch cannot be edited
   underneath a half-changed setting.
2. The rail lists the four sections; selecting one swaps the pane. The selected section
   is visually unambiguous.
3. A section can push a detail page over itself, with a back arrow returning to the
   section root. The rail selection does not change while a sub-page is open.
4. **General** holds the context-menu behaviour choice: *Preload and wait* (default) or
   *Run immediately*. The Run-immediately option states in words what it will do, since
   it writes files without further confirmation.
5. **qpdf** holds the read-only status block from [0003](../0003-bundled-qpdf.md): a clear
   ✓/✗ mark, the resolved path, the detected version, which mechanism found it, and
   **Change…** / **Re-detect** buttons.
6. **Licences** holds the list from [0005](../0005-licences-screen.md).
7. Settings persist to a JSON file in the platform's per-user application data
   directory, written atomically — a crash mid-write must not produce a file that fails
   to parse on next launch. Secrets never go in this file.
8. A settings file that is missing, empty, or corrupt yields defaults rather than an
   error dialog. A corrupt one is renamed aside, not deleted.
9. Settings render correctly in both light and dark system themes.

## Edge cases

- Settings opened while a batch is running: allowed, but changing the qpdf path takes
  effect on the next run, not the current one. Say so in the UI rather than blocking.
- Two instances of the app cannot occur (single-instance), so concurrent writes to the
  settings file are out of scope.

## Verification

Settings written, app restarted, values still in effect. A deliberately corrupted file
loads defaults and leaves a `.corrupt` copy behind. Both themes captured.

## Out of scope

The contents of the Passwords, qpdf and Licences sections — those are 0002, 0003, 0005.
This feature delivers the frame and General only.

## Outcome

All nine criteria met. Settings live at
`~/Library/Application Support/PDF Unlock/settings.json` on macOS and `%APPDATA%\PDF
Unlock\settings.json` on Windows, written via write-temp-then-move. A corrupt file was
confirmed to yield defaults and leave a `.corrupt` copy; an empty file yields defaults.
The pushed detail page keeps its rail selection, verified directly.

Placeholders remain in Passwords (rule list), qpdf (nothing — the status block is real
and shows the resolved installation and its origin) and Licences, each naming the
feature that fills it.
