# Agent Note: Remove the packed-session fixture branch migrator

Status: implemented

English | [中文](2026-07-26-remove-packed-session-fixture-migrator.zh.md)

## Problem

The repository's default writers and snapshot check keep session fixtures in the canonical packed-row layout. `pnpm run migrate:packed-session-fixtures` remained alongside that permanent enforcement only so in-flight branches carrying older fixture edits could merge current `master` and mechanically converge without re-recording model output.

Once every such branch was merged, closed, or already canonical, the write command and its branch-convergence instructions had no continuing owner. Keeping a mutation command after its transition ended would add a second apparent maintenance path beside the permanent read-only snapshot check.

## Decision

Remove the temporary `scripts/migrate-packed-session-fixtures.ts` CLI and the root `migrate:packed-session-fixtures` package command. Remove the transitional command links from the testing policy, the ACP snapshot README, and the implemented packed-row Agent Note; replace the command-specific remediation text in `scripts/session-fixture-layout.snapshot.ts` with command-independent canonical-layout guidance.

Retain `scripts/session-fixture-layout.ts`, its unit tests, and `scripts/session-fixture-layout.snapshot.ts`. They define and enforce the permanent canonical layout; only the branch-facing writer is removed.

## Alternatives considered

**Keep the command indefinitely.** This makes old fixture conversion convenient, but it leaves a repository-wide mutation tool after the only known migration window closes. The read-only gate already supplies the durable behavior and diagnostic.

**Remove the canonicalization module with the CLI.** The module is not transition residue: snapshot CI uses it to discover future fixtures, decode mixed physical records, and compare them with the canonical packed representation. Removing it would also remove enforcement.

**Delete the command immediately when packed rows reach `master`.** Older open branches would then need ad hoc scripts or manual snapshot regeneration after retargeting, increasing conflict risk and making decoded-event preservation harder to review.

## Consequences

The repository carries no branch-migration mutation command and no temporary convergence instructions. Committed fixtures remain permanently governed by the read-only canonicalizer and `scripts/session-fixture-layout.snapshot.ts`. Any future branch introducing non-canonical session JSONL fails the keyless snapshot gate and must author or re-record canonical packed rows.
