# PDF Unlock

A small desktop app for macOS and Windows that removes password protection from PDFs
you own. Point it at a file — or a hundred — and it writes an unencrypted copy next to
each one, named `statement.pdf` → `statement_decrypted.pdf`.

It does the actual work by driving [qpdf](https://qpdf.sourceforge.io/), which is the
part of this that has been battle-tested for twenty years.

> **Status: early development.** The core works and is tested, but there are no releases
> yet, no installer, and no context-menu integration. See
> [Current state](#current-state) for exactly what does and does not work today.

## Why

If you keep bank statements, payslips, or insurance documents as password-protected
PDFs, you know the tax: every single time you want to read one, you type the password.
Search doesn't work across them. Previews don't render. Your own documents behave like
someone else's.

`qpdf --decrypt` fixes that in one command, but running it by hand over a folder of
forty statements, each with its own password, is its own kind of tedious. This is the
part in between: pick the files, deal with the passwords once, walk away.

## What it does

- **Batch decryption.** Select any number of PDFs. One shared password covers the
  common case; any individual file can override it when it differs.
- **Writes beside the original.** No output folder to choose, no upload, nothing leaves
  your machine. The original is never modified.
- **Tells you what happened, per file.** Decrypted, wrong password, not actually
  encrypted, or output already exists — each one distinct and readable at a glance.
- **Handles permissions-only PDFs.** A PDF that opens freely but blocks printing or
  copying needs no password at all to unlock. The app detects this and doesn't ask.
- **Never destroys existing work.** If a `_decrypted` copy already exists, the run stops
  and asks before the run starts, not halfway through it.
- **Re-run what's left.** "Decrypt 3 remaining" retries only what hasn't succeeded, so
  fixing one wrong password doesn't mean redoing the batch.

### On passwords

Passwords are handed to qpdf over standard input, never as command-line arguments —
process arguments are readable by any other process on both macOS and Windows, and
your bank statement password has no business showing up in `ps` output.

Nothing is written to disk today. When the planned password store lands, secrets will
live in the macOS Keychain or Windows Credential Manager, never in a plain config file.

## Requirements

**qpdf 11 or newer.** Version 11 is the floor because it introduced `--password-file`,
which is what keeps passwords out of the argument list.

PDF Unlock looks for qpdf in this order, and uses the first copy new enough to work:

1. a location you chose explicitly in settings
2. anything named `qpdf` on your `PATH`
3. standard install locations — `/opt/homebrew/bin`, `/usr/local/bin`,
   `%ProgramFiles%\qpdf*\bin`
4. the copy bundled inside the app

Because of (4) you should not need to install anything — but if you already keep qpdf
current, yours wins, and you get its fixes on qpdf's release schedule rather than this
app's. Whichever copy is in use is named in the window, so two machines behaving
differently is a question you can answer at a glance.

Reasoning behind that arrangement:
[ADR 0001](docs/adr/0001-qpdf-resolution-strategy.md).

If you'd rather install qpdf yourself:

```sh
brew install qpdf            # macOS
winget install qpdf.qpdf     # Windows
```

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
git clone https://github.com/<you>/pdf-unlock.git
cd pdf-unlock
dotnet run --project src/PdfUnlock
```

Files given on the command line are preloaded, which is also how the context menu will
hand work over once that ships:

```sh
dotnet run --project src/PdfUnlock -- ~/Documents/statements/*.pdf
```

<details>
<summary>If the built executable won't start</summary>

A Homebrew-installed .NET lives somewhere the app host doesn't search, giving you
*"You must install .NET to run this application"* despite .NET being installed. Either
use `dotnet run`, or point it at the runtime:

```sh
export DOTNET_ROOT=$(brew --prefix dotnet)/libexec
```

Published self-contained builds are unaffected.
</details>

## Current state

Working and verified against real encrypted PDFs:

- [x] qpdf resolution across all four candidate locations, with a version floor
- [x] Batch list, shared password, per-file override
- [x] Decryption, including permissions-only PDFs
- [x] Per-file outcomes: decrypted, wrong password, not encrypted, output exists,
      qpdf unavailable
- [x] Collision detection before the run, with overwrite or skip per file
- [x] Sequential run with progress, and a cancel that cleans up partial output
- [x] Drag-and-drop, and file arguments on launch
- [x] Correct rendering in both light and dark system themes

- [x] Settings, persisted, with the qpdf status panel

Not built yet — one spec each in [`docs/features/`](docs/features):

- [ ] Saved per-folder passwords, in the OS keychain
- [ ] Dependency licences screen
- [ ] Bundled qpdf binaries
- [ ] Installers, and context-menu integration on both platforms
- [ ] Update notification
- [ ] CI, releases, changelog

## Design notes

- [`CONTEXT.md`](CONTEXT.md) — the project's vocabulary. What a *Job* is, what a *Batch*
  is, the difference between an open password and a permissions password, and what
  *remaining* means. Worth reading before the code.
- [`docs/adr/`](docs/adr) — decisions that would otherwise look arbitrary later.
- [`docs/features/`](docs/features) — a specification per unbuilt feature, with the
  build order and what "done" means for each.

## Licence

MIT, for this application.

qpdf is a separate work, distributed under the Apache License 2.0. PDF Unlock invokes
it; it is not a derivative of it. Full notices for every dependency are listed in the
app's own Licences screen — none of this would exist without that work.
