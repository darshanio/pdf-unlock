# Feature specifications

One document per unbuilt feature: what it is for, what "done" means, the edge cases that
are easy to miss, and how it gets verified. Written before the code so that scope is a
decision rather than an accident.

Each spec carries a **Status** line. Keep it current — a spec that says "not started"
about finished work is worse than no spec.

Finished specs move to [`done/`](done), so this directory is the list of work
outstanding rather than a pile to be sifted.

## Build order

Dependencies, not preference, determine the order:

| # | Feature | Depends on | Why here |
|---|---------|-----------|----------|
| [0005](0005-licences-screen.md) | Licences screen | 0001 | Small, and an obligation rather than a feature |
| [0004](0004-macos-app-bundle.md) | macOS bundle and Quick Action | 0003 | Needs something to bundle |
| [0008](0008-windows-installer.md) | Windows installer and context menu | 0003 | Same |
| [0007](0007-ci-and-releases.md) | CI and releases | 0003, 0004, 0008 | Needs artifacts to publish |
| [0006](0006-update-notification.md) | Update notification | 0001, 0007 | Needs releases to point at |

## Done

- [0001 — Settings shell](done/0001-settings-shell.md)
- [0002 — Folder password rules](done/0002-folder-password-rules.md)
- [0003 — Bundled qpdf](done/0003-bundled-qpdf.md) — app side done; the CI build has not run yet

Already built and verified: qpdf resolution, the batch and job model, decryption,
collision handling, cancellation, drag-and-drop and launch arguments. See the README.

## Related

- [`CONTEXT.md`](../../CONTEXT.md) — the domain vocabulary these specs use
- [`../adr/`](../adr) — decisions that constrain them
