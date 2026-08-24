# OpenArcanum

## Project Goal

Preserve Arcanum: Of Steamworks and Magick Obscura gameplay and content as faithfully as possible while modernizing rendering, assets, UI, compatibility, tooling, and platform support.

This is not a redesign of Arcanum's gameplay.

## Important Paths

Original game data:
D:\OpenArcanum\Source\Steam-Clean

Unity project:
D:\OpenArcanum\Engine\OpenArcanum-Unity

Unity version:
6000.0.71f1

## Original Game Data

Never modify:
D:\OpenArcanum\Source\Steam-Clean

GameData is a local Git-ignored junction providing access to the original Arcanum game data.

Never commit original Arcanum DAT archives, extracted copyrighted assets, or other original proprietary game content.

HDAssets is also local-only and Git-ignored.

## Compatibility Rules

Preserve original game behavior unless explicitly instructed otherwise.

Graphics work must not unintentionally change:

- Gameplay
- Quests
- Dialogue
- Balance
- Scripts
- AI behavior
- World coordinates
- Collision
- Object placement
- Sprite hotspots
- Sprite pivots
- Animation timing
- Map layout

## Graphics Architecture

OpenArcanum supports two graphics modes:

Original
Enhanced

Original mode must remain functional and faithful to the original assets.

Enhanced mode must fall back to the original asset whenever an enhanced replacement does not exist.

Enhanced graphics must support incremental replacement. The entire asset library must not need to be converted at once.

## Code Organization

Prefer OpenArcanum-specific systems under:

Assets/_OpenArcanum/

Avoid modifying upstream project code under:

Assets/_Game/

unless integration requires it.

When upstream code must be changed, keep the modification narrow and preserve the existing behavior for Original graphics mode.

Understand Unity assembly-definition boundaries before adding dependencies between directories.

## Development Practices

Before changing code:

1. Inspect the relevant implementation.
2. Inspect git status.
3. Verify the current branch.
4. Inspect relevant recent history.
5. Understand the existing data flow before redesigning it.

Make small, logically scoped changes.

Compile Unity after meaningful C# changes.

Do not assume code works merely because it compiles.

Use VisualSandbox and existing test scenes where appropriate.

Do not silently discard existing work.

Do not rewrite large files unnecessarily when a narrow change is sufficient.

Do not rewrite Git history.

Do not push branches unless explicitly requested.

## Enhanced Asset Requirements

Higher-resolution replacements must preserve the original asset's:

- World dimensions
- Pivot
- Hotspot
- Placement
- Orientation
- Animation timing
- Frame semantics
- Gameplay behavior

A 4x replacement may contain four times as many pixels in each dimension while occupying exactly the same physical size in the game world.

## HD Assets

Optional local replacement artwork is stored under:

HDAssets/

HDAssets is Git-ignored.

Enhanced mode should look for a replacement there and fall back to the original ART resource when none exists.

Read PROJECT_STATUS.md before beginning work.
