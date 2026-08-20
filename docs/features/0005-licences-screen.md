# 0005 — Licences screen

**Status:** not started
**Depends on:** [0001](done/0001-settings-shell.md)
**Blocks:** nothing

## Why

The app stands on other people's work — Avalonia, the .NET runtime, qpdf and its own
dependencies. Attribution should be visible in the product, not only in a file in the
repository.

## Acceptance criteria

1. Settings → Licences lists every dependency with its name, version, licence
   identifier, and full licence text available without leaving the app.
2. The NuGet portion is **generated at build time** from the restored package graph, so
   it cannot drift out of date as dependencies change.
3. qpdf has a hand-written entry, because it is not a NuGet package. It appears whether
   the resolved installation is bundled or the user's own — the app depends on it either
   way.
4. The app's own licence (MIT) is stated distinctly from its dependencies'.
5. The list is readable with the keyboard and scrolls; long licence texts do not force
   the window wider.
6. A build that cannot generate the licence data fails loudly rather than shipping an
   empty screen.

## Edge cases

- A package with no detectable licence: listed as "licence not declared" with its
  project URL, never silently omitted.
- Transitive dependencies: included. The obligation follows what ships, not what was
  directly referenced.

## Verification

Add a dependency, rebuild, confirm it appears without any manual edit. Confirm the
generated data contains the Apache-2.0 text for qpdf and the MIT text for the app.

## Out of scope

Licence *compliance* checking or allow-listing.
