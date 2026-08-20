# 0006 — Update notification

**Status:** not started
**Depends on:** [0001](0001-settings-shell.md), [0007](0007-ci-and-releases.md)
**Blocks:** nothing

## Why

Without releases to point at, a user has no way to learn a fix exists. Notification is
the cheapest thing that solves that, and — unlike auto-update — is safe for an unsigned
application.

## Acceptance criteria

1. On launch, the app asks the GitHub Releases API for the latest release, compares it
   with its own version, and shows an unobtrusive "version X available" affordance when
   newer. Opening it launches the browser.
2. The check is **notify-only**. The app never downloads or installs anything.
3. Failure — offline, rate-limited, API changed — is silent. A failed update check must
   never interrupt the user's actual task.
4. The check runs at most once per day, cached, so repeated launches do not hammer the
   API or the user.
5. It can be turned off in Settings → General, and honours that immediately.
6. Pre-releases are ignored unless the running build is itself a pre-release.

## Edge cases

- Version comparison must not treat `v1.10.0` as older than `v1.9.0`. Semantic
  comparison, not string.
- A release with no assets: still reported; the user may want the source.

## Verification

Point the check at a fixture release newer and older than the current version and
confirm both outcomes. Confirm airplane mode produces no dialog and no log noise.

## Out of scope

Silent auto-update. An updater that installs unsigned binaries is an attack vector, and
was rejected during design for that reason.
