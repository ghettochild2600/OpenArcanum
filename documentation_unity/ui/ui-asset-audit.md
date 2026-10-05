# Retail UI Asset and Reconstruction Audit

## Scope and conclusion

This is a source-reconstruction audit, not a production-UI implementation. The current M12C controllers, view models,
command routing, input precedence, modal state, and gameplay-authority boundaries remain the correct functional layer.
`ProductionGameUiPresenter` is a disposable placeholder presentation over that layer. No production UI, gameplay
system, or Enhanced artwork was changed by this audit.

The retail interface is an authored 800 x 600 composition. Most modal and character-management screens occupy the
800 x 400 strip between a 41-pixel top region and a 159-pixel bottom HUD region. Retail ART identity, palette,
rotation, frame, dimensions, transparency, and source IDs can be preserved deterministically. Original and Enhanced
presentation can therefore share one logical layout and one controller hierarchy while resolving different pixels.

## Evidence order and limits

Evidence was evaluated in this order:

1. Mounted retail `GameData`, including the module and patch/base DAT archives.
2. The retail `Manual.pdf` for photographed screen composition and interaction descriptions.
3. `art/interface/interface.mes` and its referenced ART resources.
4. Existing OpenArcanum ART/MES/VFS decoders.
5. The source-authentic UI implementation in the local `arcanum-ce` research checkout.
6. Existing OpenArcanum M12C controllers and presenter.
7. Current OpenArcanum screens, only as a before-state baseline.

The retail source evidence establishes 800 x 600 as the authentic logical coordinate surface. No alternate retail
resolution set was proven, so none is invented here. The community source contains later accommodation code; it is
useful for control identity and geometry, but it is not treated as proof that the original release shipped a modern
responsive layout.

## Inventory result

The authoritative interface table contains **846 entries**. The exporter decoded **799** and recorded **47** as
explicit errors because the named target was absent from the mounted retail archives. The missing entries include
world-map pieces, portrait/race names, a few skill/weapon targets, and other table records. They remain catalogued
with their retail identity and must not be replaced with guessed art.

The committed manifest is `ui-asset-manifest.csv`. It contains one row per interface-table entry and records:

- retail source ID and source path;
- ART format, decoded reference identity, native dimensions, and alpha presence;
- screen/role/state classification where source evidence supports it;
- logical dimensions and coordinates where provable, otherwise the literal value `unknown`;
- source call-site evidence, related identities, reuse, Enhanced path, and slice suitability;
- an `EXPORT ERROR` note for every unresolved target.

The screen classifier maps 503 entries to a screen or common family and leaves 343 explicitly `unknown`. Unknown is
preferred to a filename-only guess. Direct source call sites take priority over conservative name classification.

### Source formats

- **ART**: palette-indexed images with embedded palette, one or more frames/rotations, offsets/hotspots, and palette
  variants. Palette index 0 is transparent for exported interface references.
- **MES**: the authoritative numeric-ID-to-resource-path table and related interface strings/metadata.
- **C source geometry**: screen rectangles, button coordinates, hit rectangles, modal rules, and coordinate
  transforms from the research checkout.
- **Retail manual PDF**: visual composition and player-facing behavior evidence.
- **Bitmap font ART**: glyph collections, normally 225 frames per font identity.

## Deterministic lossless reference export

`RetailUiReferenceExporter` adds `OpenArcanum > UI Audit > Export Retail UI References` in the Unity Editor. It mounts
the retail module and patch/base archives in first-mount-wins order, reads the interface MES, decodes every available
ART palette/rotation/frame, and writes local-only RGBA PNG references to:

`D:/OpenArcanum/UIReference/Original/`

Identity is never based on a manual rename:

`art/interface/<source-id>-<source-stem>/p<palette>-r<rotation>-f<frame>.png`

The external folder also receives the retail interface table, `ui-source-index.csv`, and a README carrying counts and
the non-redistribution warning. Native frame dimensions and alpha are retained. Retail PNGs are outside the Git
workspace and must never be committed or redistributed. The committed CSV contains deterministic paths to those
local references, not the pixels.

Validation on the mounted installation successfully decoded representative full-screen, full-panel, control,
indicator, icon, scrollbar, and dialogue assets. The rerun produced the same 846/799/47 result.

## Original coordinate and composition findings

### Shared surface and gameplay frame

- Logical base: **800 x 600**.
- Gameplay top interface region: `(0,0,800,41)`.
- Gameplay world viewport: `(0,41,800,400)`.
- Gameplay bottom interface region: `(0,441,800,159)`.
- Normal player lens: `(311,96,178,178)`; full-screen lens: `(311,196,178,178)`.
- Health vial: `(14,472,28,88)`; fatigue vial: `(754,473,28,88)`.
- Ten quick slots begin at y=445, x=`198,237,276,315,354,418,456,495,534,573`.

The retail `intrface.art` is an 800 x 600 authored frame with a transparent world aperture and integrated bottom
controls. It is not equivalent to the current generic button strip.

### Main Menu

- Full composition: `(0,0,800,600)` using source ID 329 `mainmenuback`.
- Source also defines a partial `(0,41,800,400)` region for related presentation.
- Primary buttons are at x=410 and y=143/193/243/293 for Single Player, Options, Credits, and Quit.
- The current production launch/New Game/Load command path can be retained. The later presenter must supply the
  authentic art, states, layout, cursor, version presentation, and source menu flow.

### Character Creation

The source interface is not a giant scrolling form. Initial identity selection uses a composed screen with portrait
`(95,28,64,64)`, name `(46,183,228,18)`, gender `(66,243,186,19)`, race `(66,293,186,19)`, and background
`(66,343,186,19)`. The principal editor uses source ID 22 `char_maint`, an 800 x 400 panel, with fixed subpanels for
skills (ID 29, 235 x 325), technology disciplines (ID 30), and spells (ID 31). Buying equipment subsequently reuses
the barter composition.

M12B/M12C creation authority remains reusable. A later source presenter should replace the ScrollRect-heavy visual
composition, keep the major editor visible without horizontal scrolling at all target resolutions, and allow only
genuinely long source lists to scroll.

### Gameplay and combat HUD

Primary top buttons include Character `(4,2)`, Logbook `(41,2)`, Map `(78,2)`, Inventory `(115,2)`, Fate `(157,9)`,
and Sleep `(605,9)`. Bottom controls include combat at `(86,457)`, Spells `(649,494)`, Skills `(693,456)`, and
Schematics `(693,539)`, plus the ten quick slots above. Health/fatigue, AP/readiness, active item/weapon, message and
targeting regions are integrated into the authored frame.

The existing UI and M8I combat controllers already provide the authority and command boundaries. A later presenter
must bind those values to authentic regions; it must not reimplement combat or item authority.

### Inventory, equipment, barter, loot

The inventory composition joins source ID 221 `inventor` (442 x 400) with the paper-doll/equipment region. Proven
equipment hit rectangles are:

- helmet `(151,107,64,64)`;
- rings `(247,107,32,32)` and `(279,107,32,32)`;
- medallion `(247,139,64,32)`;
- weapon `(23,170,96,128)`;
- shield `(247,171,96,128)`;
- armor `(119,171,128,160)`;
- gauntlets `(55,107,64,64)`;
- boots `(150,331,64,64)`.

Inventory, barter, steal, and corpse/loot presentations share source geometry and components. Existing M3/M8E/M12C
inventory/equipment/transfer commands remain authoritative. The source presenter owns only art, layout, selection,
drag/right-click affordances, stack labels, weight/gold text, and feedback.

### Maps and other fixed panels

The World Map uses `(0,41,800,400)`, a map canvas `(150,52,501,365)`, a PC lens `(25,65,89,89)`, and authored
compass/right-side controls. Local and world maps are distinct presentations. Inventory, world map, and main-menu
source code convert mouse input through a centered 800 x 600 coordinate model. Journal uses a book spread and tabs;
Schematics uses a discipline-tab book; Sleep and Fate use modal overlays; Dialogue has both a thin framed window and
an ornate box family.

## Typography

The retail interface table exposes bitmap font/glyph ART identities including Roller, Arial-like, Morph, Pork,
Casablanca, Cloister, Comic, Elga, Flare, Bookman-like, Clarendon-like, Courier-like, Garamond-like, Georgia-like,
Zurich-like, Latin, Swiss, Pepper, Times-like, and icon-font families. Typical resources contain 225 frames. Source
code supplies the context, position, colors, and alignment; the manual supplies additional visual evidence.

For Original mode, local runtime decoding may reproduce retail glyph atlases without adding them to Git. For
Enhanced mode, prefer licensed/dynamic Unity font assets with source-matched scale, color, spacing, outline/shadow,
and alignment. Keep baked text only when it is inseparable from authored illustration; otherwise separate labels
from pixels. Dynamic text is required for localization, accessibility, and resolution-independent rendering. Do not
redistribute retail font resources.

## Scalable component findings

- ID 354 `dialoguewindow` is the strongest full 9-slice candidate: preserve an 8-pixel perimeter and treat 16 x 16
  as the provisional minimum. It still requires edge-pattern visual QA before production import.
- ID 822 `dialoguebox` is horizontally extensible only: preserve 16-pixel end caps; provisional minimum 32 x 136.
- Scrollbar IDs 238/787/240 form an authored tiled assembly: fixed 11-pixel width, 5-pixel top, 1-pixel repeating
  middle, 7-pixel bottom, minimum height 12. They are not conventional four-direction 9-slices.
- Full-screen/frame art, paper dolls, ornamental books, icon borders, and decorated panels remain fixed-size by
  default. No asset is sliced merely because Unity can slice it.

The manifest records these exceptions; all other entries are `unverified; fixed-size by default` or equivalent.

## Current OpenArcanum Placeholder UI Baseline

M12C is functionally complete, but its generic dark full-screen panels, gradient buttons, generic close controls,
bottom button matrix, translucent Inventory/World Map overlays, scroll-heavy Character Creation form, and generic
spacing are **functional placeholder presentation**. They are not evidence for retail layout and are not the visual
hierarchy to preserve.

Preserve M12C controller logic, projections, command routing, input behavior, and useful screen/modal state. Replace
the visual skin, panel composition, HUD arrangement, and source-screen layouts in later UI phases. Current screenshots
need not be committed; this textual baseline is sufficient.

## Existing authority and known presentation gaps

`GameUiController` already projects and routes Main Menu, Character Creation, Inventory, Character, Skills, Magic,
Technology, Crafting, Options, Journal, Map, Party, Merchant, Save/Load, Dialogue, and Corpse flows. Combat remains
under `CombatUiController`/M8I. These controllers should survive presentation replacement intact.

Presentation-specific gaps include local-map reveal art, authentic paper-doll animation/gestures, recent-action slot
visuals, rich tooltips/inspection, and source Sleep/Fate flows. Missing gameplay authority must fail closed or be
implemented in its own bounded milestone; the UI must not invent it. Intro/cinematic presentation also remains
outside this audit.

## Implementation phase plan

| Phase | Bounded result | Existing authority | Focused proof and commit boundary |
|---|---|---|---|
| UI-A | Common source resolver, Original/Enhanced mapping, 800 x 600 logical foundation, common controls/fonts/cursors | Graphics mode is presentation selection only | Resolver identity/fallback/dimension/alpha tests; 1080p/1440p/4K harness; one tooling/runtime commit and audit update |
| UI-B | Retail Main Menu reconstruction | Production launch, New Game, Load, Options/Quit routes | Original source asset/layout proof; one Enhanced replacement and missing-replacement fallback; screen commit |
| UI-C | Authentic gameplay/combat HUD | M12C, M8I, inventory/magic/technology/journal/map commands | World-aperture expansion, quick slots, vials, combat state at all targets; Original/Enhanced parity commit |
| UI-D | Retail Character Creation | M12B creation service and M12C commands | No horizontal scrolling; fixed editor/subpanels; validation at all targets; screen commit |
| UI-E | Inventory, equipment, Character, Skills | M3/M4 and M12C projections/commands | Paper-doll hit regions, transfers/equip, modal/input parity; grouped screen commit |
| UI-F | Dialogue, Journal, Party | M10/M9/M12C authorities | Modal precedence, reply/quest/party commands, source book/dialogue geometry; grouped commit |
| UI-G | Magic, Technology, Crafting | M5/M11/M12C authorities | Source tabs/icons/readiness/feedback; no effect authority in presenter; grouped commit |
| UI-H | Local Map and World Map | M7/M12C map/travel authority | Distinct map surfaces, marker/route controls, widescreen containment; grouped commit |
| UI-I | Merchant, Save/Load, Options, secondary screens | M3/M6/M12C where available | Barter/save/options parity; Sleep/Fate only after runtime closure; grouped commit |

Every phase consumes only the documented source assets, reuses the named controller, adds focused presentation tests,
proves Original mode and at least one Enhanced/fallback path, validates 1920 x 1080, 2560 x 1440, and 3840 x 2160,
and ends at its own reviewable commit boundary.

## Future validation standard

For every reconstructed screen, Original mode must resolve the expected source identities, match evidenced logical
geometry and interactions, retain all functional controls, show no placeholder controls, and pass all three target
resolutions. Enhanced mode must use the exact same logical RectTransforms and hit regions, resolve valid replacements,
fall back per missing asset/frame to Original, and preserve gameplay, modal, input, and controller behavior.

Automated tests should cover resolver identity, dimensions, fallback, hit regions, modal precedence, and command
routing. VisualSandbox/reference scenes should capture deterministic screenshots at all targets for manual comparison.
Pixel identity is not required where widescreen necessarily exposes more world, but source proportions, fixed artwork,
control placement, and interaction semantics are required.

## Recommended next milestone

Begin with **UI-A — Common UI Runtime / Skin Resolver**. It is the narrow proof that source identity, local retail
decoding, Enhanced 4x replacement, fallback, logical sizing, and graphics-mode rebuild can coexist without duplicating
controllers. Do not begin a screen reconstruction until that shared contract is proven.
