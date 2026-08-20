# 0002 — Folder password rules

**Status:** done — verified 2026-08-20
**Depends on:** [0001](0001-settings-shell.md)
**Blocks:** nothing

## Why

The whole point of the tool. Statements for one bank live in a folder and share a
password; another bank's folder has a different one. Typing them repeatedly is the
tedium the app exists to remove.

## Domain vocabulary

Uses **Directory Password Rule**, **Password Store**, **Password Override**,
**Default Password** as defined in [`CONTEXT.md`](../../../CONTEXT.md). A rule keys on the
*name of the immediately containing folder*, case-insensitively, so it survives an
enclosing year folder changing.

## Acceptance criteria

1. The store is **off by default**. A single toggle in Settings → Passwords enables it,
   and states plainly that passwords will be saved to the operating system's secret
   store.
2. Rule **metadata** — folder name, the full path it was created from, timestamps —
   lives in plain JSON. Rule **passwords** live in the macOS Keychain or Windows
   Credential Manager, referenced by rule id. A stolen JSON file reveals folder names
   and no secrets.
3. Folder names are recorded as rule *candidates* only while the store is enabled. With
   it off, the app accumulates nothing.
4. Settings → Passwords lists existing rules and suggests candidate folders seen in
   batches, each convertible to a rule in one action. A folder can also be chosen
   manually via a directory picker.
5. Matching is on folder name only. Where two rules would share a name, the app warns
   **at rule-creation time**, naming the path of the existing rule, and asks whether
   this is the same source.
6. A job whose folder matches a rule shows *which* rule supplied its password.
7. Precedence is Override → Rule → Default. Confirmed visible in the detail panel.
8. After a run, one dialog lists folder → password → outcome for the passwords used,
   offering to store those **proven correct** and not already stored identically.
   Failures appear in the list so the user can see what did not work, but cannot be
   selected for storing.
9. A rule whose stored password fails prompts inline; if a new password succeeds, the
   app offers to update that rule. It never updates a stored password silently.
10. Deleting a rule removes both the metadata entry and the secret.

## Edge cases

- Keychain access denied or cancelled by the user: the rule is not created, and the
  failure is reported as a permission problem, not a generic error.
- Store enabled, then disabled: existing rules are retained but unused, and the UI says
  so. Disabling is not deletion; a separate explicit "forget all saved passwords".
- Two rules matching after a rename that creates a collision: most recently created
  wins, and the ambiguity is surfaced on the job row.
- A folder name that is empty or a volume root: rejected, since it would match far too
  broadly.

## Verification

Round-trip a rule through the real OS secret store on macOS. Confirm the JSON contains
no password material by reading it. Confirm a wrong stored password prompts and updates
on success. Confirm disabling the store stops candidate accumulation.

## Out of scope

Password *templates* — deriving a per-file password from a formula. Explicitly rejected
during design: it needs per-institution knowledge and placeholder sources that do not
exist in a filename.

## Outcome

All ten criteria met, verified against the real macOS Keychain rather than a stub:
metadata JSON confirmed to contain folder names and no password material; a password
round-tripped through the keychain; case-insensitive name matching; conflict detection
naming the existing rule's origin path; precedence Override → Rule → Default; a volume
root rejected as too broad; deletion removing both metadata and secret; and rules
surviving a reload from disk.

Passwords reach the keychain over standard input, not as arguments — `security -w` with
no value reads the secret from stdin (twice, since it asks to retype). Verified that the
password does not appear in the process arguments.

Two defects were found by verification rather than by reading the code:

1. **A permissions-only PDF was being treated as proof that a password was correct.**
   Such a file decrypts whatever password is supplied, so its success is no evidence at
   all. It masked a genuine `WrongPassword` failure on a sibling file and suppressed the
   whole folder's row. Decryption now reports whether the password was actually required
   and used, and only such a success counts as proof.
2. The prompt's outcome column read "failed" for a folder whose stored password was
   already correct, because it was derived from "is there anything to save" rather than
   from what happened. Outcome is now stated explicitly: worked, mixed, or failed.

The Windows credential store is implemented but **unverified** — it has never run on
Windows. That is the one part of this feature not to be trusted yet.
