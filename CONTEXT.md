# Context

Domain glossary for the PDF decryption GUI tool. Glossary only — no implementation
detail, no specs.

## Job

One input PDF paired with the password to be used for it and its current outcome.
The unit of work the user watches succeed or fail. A Job is created when a file
enters the [Batch](#batch) and is discarded only when the user removes it.

A Job's **effective password** is its [Password Override](#password-override) if it
has one, otherwise the Batch's [Default Password](#default-password).

Job states:

- `Pending` — not yet attempted, or attempted and then reset
- `Running` — qpdf is executing for this Job
- `Decrypted` — an unencrypted equivalent PDF was written to the [Output Path](#output-path)
- `Failed` — carries a reason: `WrongPassword`, `NotEncrypted`, `Collision`,
  `QpdfMissing`, `IOError`

## Batch

The full list of Jobs currently loaded in the window, plus the Default Password
shared across them. There is exactly one Batch at a time: files added later —
whether by the file picker or by a [Shell Invocation](#shell-invocation) — append
to it rather than starting a new one.

## Default Password

The Batch-level password applied to every Job that has no Password Override.
Editing it retroactively changes the effective password of all non-overridden Jobs.

## Password Override

A password attached to a single Job, taking precedence over the Default Password.
Set when one file in the Batch differs from the rest.

## Open Password

The password a PDF requires before a reader will display it at all. Called the
*user password* in the PDF specification. Without it the file cannot be read.

## Permissions Password

The password guarding what a reader may do with an already-openable PDF —
printing, copying, extracting. Called the *owner password* in the PDF
specification. A PDF protected only by a Permissions Password can be decrypted
with no password supplied at all.

## QPDF Installation

The copy of the qpdf command-line program the tool drives. There may be more than
one available; the tool locates one, checks it is new enough, and runs it.

An installation is either a **System Installation** — one the user or their package
manager put on the machine, which updates on qpdf's own release cadence rather than
PDF Unlock's — or the [Bundled Installation](#bundled-installation) shipped inside
PDF Unlock itself. System Installations are preferred, so that a user who keeps
qpdf current is never held back to the version PDF Unlock happened to ship.

## Bundled Installation

The copy of qpdf shipped inside PDF Unlock, used only when no System Installation
can be found. It exists so the tool works on a machine that has never heard of
qpdf — notably macOS, where qpdf publishes no official binary and the only ordinary
route to a System Installation is a package manager the user may not have.

Being a fallback rather than the default, it is the version most likely to be stale;
the tool therefore always tells the user which installation it is using.

## Resolution

The act of finding a usable QPDF Installation: consulting the user's explicitly
chosen path, then the system search path, then the platform's conventional install
locations, then the Bundled Installation. The first candidate new enough to be
usable wins, so an explicit choice beats a System Installation, which beats the
bundle. Resolution happens
once per application launch, and yields either a usable installation or the
[Unresolved](#unresolved) state.

Resolution records *how* it succeeded, not merely that it did: the user is shown
which mechanism found the installation, because that is the fact that explains a
later breakage.

## Unresolved

The state in which no usable QPDF Installation could be found. Because the Bundled
Installation is always present, this is an unusual state, reached only when the
bundle is missing or damaged — not the ordinary condition of a fresh install. Decryption is
impossible until it is fixed. The tool never fails silently here: it explains what
is missing and offers to install it, or accepts a path the user supplies.

## Permissions-Only PDF

A PDF that has a Permissions Password but no Open Password. It opens freely, and
its encryption can be stripped without the user knowing any password. Detecting
this case is what lets a Shell Invocation sometimes complete with no prompting.

## Output Path

Where a Job writes its result: the input's own directory, the input's filename
with `_decrypted` appended before the extension. `report.pdf` becomes
`report_decrypted.pdf`.

## Collision

The condition where a Job's Output Path is already occupied before the Job runs.
Resolved by a [Collision Resolution](#collision-resolution), never silently.

## Collision Resolution

The user's decision for a colliding Job: overwrite the existing file, skip the
Job, or write to a distinct name instead. Unresolved, a Job in Collision does not
run.

## Shell Invocation

The tool being launched by the operating system's file context menu with one or
more PDFs as arguments, rather than by the user opening the application directly.
Contrast with a picker-driven Batch: the files are chosen before the tool exists.

## Remaining

The Jobs a re-run acts on: every Job not in `Decrypted`. Includes `Pending` and
all `Failed` reasons. Always surfaced as a count so the user knows the size of
what they are about to re-attempt.

## Directory Password Rule

A stored association between a **folder name** and the password used by the PDFs
kept in folders of that name, so a Job whose input sits directly inside such a
folder receives its password without the user typing it.

The rule keys on the *name of the immediately containing folder*, not on a full
path. A rule for `a bank` therefore covers `/documents/2025/a bank/x.pdf` and
`/documents/2026/a bank/y.pdf` alike: the enclosing year changes, the folder name
does not. Rules live in the [Password Store](#password-store).

## Password Store

The persisted, encrypted-at-rest collection of Directory Password Rules. Distinct
from the Default Password and Password Overrides, which are per-Batch and never
persisted. Disabled by default; the user opts in.

## Abandonment

Stopping a run because continuing cannot succeed, as distinct from a Job failing
on its own merits. A missing QPDF Installation discovered mid-run abandons the
rest of the Batch: attempting the remaining Jobs would produce identical failures
and bury the single real cause.
