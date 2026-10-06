# Contributing to OpenArcanum

OpenArcanum aims to preserve the behavior and content of the original game while modernizing its runtime,
presentation, tooling, and compatibility. Contributions should solve an observed compatibility, correctness, or
maintainability problem without redesigning Arcanum's gameplay.

## Reporting a bug

Use the bug-report issue form and provide the smallest reliable reproduction. For campaign defects, include the map,
location, NPC, dialogue, quest, or save-state context and distinguish what the retail game does from what OpenArcanum
does. Console output, a short recording, and whether a non-retail save can be shared are useful when available.

## Proposing a change

Source-fidelity, compatibility, tooling, and presentation proposals are welcome. Describe the original behavior or
other evidence behind the change. Speculative redesigns should not be presented as compatibility fixes.

Before editing code:

1. Inspect the current implementation, data flow, relevant documentation, and recent history.
2. Identify which subsystem owns the authoritative state.
3. Keep presentation and input adapters from becoming gameplay authority.
4. Prefer a small change with an explicit unsupported boundary over a broad approximation.

Code changes should include focused tests where practical and update the relevant implementation or audit document.
Run the directly affected regression groups and confirm Unity compiles. Substantial changes should also pass the
complete EditMode suite and receive proportional physical Play Mode validation. Always run `git diff --check` before
submitting.

## Asset policy

Do not submit original Arcanum assets or material extracted from them. In particular, never commit:

- `GameData/` or retail DAT archives;
- extracted ART, audio, maps, dialogue/data dumps, fonts, portraits, or UI images;
- `HDAssets/`, `UIReference/`, generated retail-data caches, or personal output directories; or
- saves or recordings that contain redistributed retail content.

Tests and documentation may identify source records and expected behavior, but fixtures must remain lawful to
redistribute. Contributors need their own legitimate Arcanum installation for source-backed integration tests.

Small corrections do not need elaborate process. Keep each pull request focused, document meaningful boundaries,
and avoid unrelated formatting or authority changes.
