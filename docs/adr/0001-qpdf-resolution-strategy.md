# 1. Prefer a system qpdf, fall back to a bundled one

Date: 2026-08-20

## Status

Accepted

## Context

PDF Unlock does its actual work by driving the `qpdf` command-line program. Where
that program comes from is not obvious, and the repository contains evidence of
both answers — a bundled binary *and* a multi-step search for an installed one —
so the reasoning needs recording.

Three properties were in tension:

1. **qpdf moves faster than PDF Unlock.** qpdf releases regularly, including
   security fixes for its PDF parser. Anything PDF Unlock ships is stale the moment
   it ships, and shipping a new PDF Unlock release to deliver a new qpdf is a poor
   trade for both of us.
2. **qpdf publishes no official macOS binary.** Windows users have two ordinary
   routes to an installed qpdf: `winget install qpdf.qpdf`, or the official
   `msvc64` release archive. On macOS the only ordinary route is a package manager
   — in practice Homebrew — which a non-developer user very likely does not have.
   For those users, "install the dependency yourself" degrades to "install a
   package manager first, then install the dependency", which is not a prompt but
   a wall.
3. **Homebrew refuses to run as root.** An elevated installer therefore cannot
   install the dependency on the user's behalf on macOS, so the classic
   "installer pulls the dependency" escape route is closed.

## Decision

Ship a bundled static qpdf, but treat it as the *last* candidate rather than the
default. Resolution consults, in order: a path the user explicitly chose, then
`PATH`, then the platform's conventional install locations, then the bundle. The
first candidate new enough to be usable wins.

The resolved installation's origin is always displayed to the user, not merely the
fact that resolution succeeded.

qpdf 11 is the minimum accepted version, because `--password-file` — which is how
passwords are kept out of the process argument list — arrived in qpdf 11.

The bundled binaries are built by a separate, manually-triggered workflow and
published as release assets, which the release workflow then downloads by pinned
version. They do not enter git history, and bumping the bundled qpdf does not
require touching application code.

## Consequences

A user who keeps qpdf current gets their own copy, on qpdf's release cadence, and
is never held back to whatever PDF Unlock shipped — property 1 is preserved for
exactly the users who care about it.

A user with no qpdf at all gets a working application on first launch and never
sees a dependency prompt. The "no usable qpdf" state, and the setup and repair UI
that serves it, becomes a rare damaged-install path rather than the ordinary
first-run experience. This is the main reason for the decision: it removes the
largest predictable source of "the app doesn't work" reports.

The costs are accepted deliberately: release artifacts grow by roughly 15MB per
platform; a static qpdf must be built for `osx-arm64` and `osx-x64`, which is
one-time CI work plus a version bump ritual; and the bundle is by construction the
staleest qpdf in play, which is why its use is always disclosed in the UI rather
than silent.

Because a system qpdf may be any version from 11 upward, PDF Unlock must confine
itself to qpdf invocations that are stable across that range. Behaviour can differ
between two machines running the same PDF Unlock build — the displayed origin and
version exist so that difference is diagnosable in one glance.

Automatic dependency installation via `brew` or `winget` is left out of scope. It
was only load-bearing while there was no bundle; with one, it is optional polish.
