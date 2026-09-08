# Upstream Gameplay-Parity Audit

- Audit date: 2026-09-07
- Audited branch/commit: `feature/player-navigation` at `174cc23`
- Mode: research and documentation only; no production code or assets changed

## Executive conclusion

OpenArcanum has a credible, tested foundation for reading the original data, rendering a real sector, assigning stable
object identities, preserving in-memory object state, animating portals, and moving a player surrogate on the original
source grid. It is not yet a gameplay-complete engine. Most rules that turn parsed data into an RPG—character creation,
inventory transactions, statistics, combat, magic, AI, quests, economy, save games, and production UI—are absent or
exist only as data fields, pure evaluators, event definitions, or a self-contained test gallery.

The public upstream repository does not contain a hidden implementation of those systems. The current project includes
all 24 commits in upstream `master` and is 31 commits ahead (`master...HEAD` = 0 behind, 31 ahead). The upstream README
describes a fuller private/work-in-progress project and explicitly says code will be published module by module. Its
high-level “what works” list must therefore be treated as an author claim, not evidence that the corresponding code is
available in the public tree.

Selective upstream reuse is still valuable: the DAT/VFS and format readers, object field decoder, dialogue reader and
evaluator, script reader/VM, quest metadata reader, and sound selectors/services remove substantial reverse-engineering
risk. They do not remove the larger integration and gameplay-state workload. Complete campaign parity is **somewhat
easier to plan and de-risk after this audit, but not substantially closer to implementation**.

## Scope and grading

This audit inspected current source and tests, all reachable branches/tags/history and reflogs, the local research clone,
and the current public GitHub repository. It did not alter or run the original game data and did not infer a production
feature from documentation alone.

Grades:

- **A — implemented and production-ready:** connected to the current production world path and previously validated.
- **B — implemented but needs integration:** substantive implementation exists, but no production bootstrap/call path.
- **C — partial/prototype:** meaningful behavior exists, but it is a demo, narrow slice, or deliberately incomplete.
- **D — data parser only:** source data can be read/modelled, but gameplay semantics are not executed.
- **E — exists upstream but not current:** reusable published upstream code is absent locally.
- **F — missing:** no meaningful implementation was found.
- **G — unknown:** evidence was insufficient to grade responsibly.

“Upstream” below means the public `Suvitruf/unity-arcanum` `master` at `2d44a62`, not unpublished work described in its
README. Because the current branch descends from that exact commit, there are no grade-E systems in the audited public
lineage.

## Repository lineage and provenance

| Fact | Evidence | Conclusion |
|---|---|---|
| Current branch | `feature/player-navigation` at `174cc23` | The live branch is authoritative; the older branch text near the top of `PROJECT_STATUS.md` is stale. |
| Current `origin` | `D:\OpenArcanum\Research\Repositories\unity-arcanum` | The working clone uses a local research mirror as its remote. |
| Local research mirror origin | `https://github.com/Suvitruf/unity-arcanum.git` | The mirror tracks the public project named in the request. |
| Common base | Local `master` and research/public `master` are both `2d44a62f3e295640eca08291b485285bc8e0d072` | Proven direct lineage, not merely similar code. |
| Divergence | `git rev-list --left-right --count master...HEAD` returned `0 31` | Current is a strict descendant: nothing published on upstream master is missing locally. |
| Published refs | GitHub shows 1 branch, 0 tags, 24 commits; local mirror has only `origin/master` | No published feature branch or release contains additional gameplay systems. |
| Latest upstream commit | `2d44a62`, “Fixes typo in README: #1”, 2026-08-06 | Matches the local mirror exactly. |
| Other local refs | OpenArcanum feature branches lead to the current line; mirror-only `feature/enhanced-rendering` is an OpenArcanum-era prototype | No alternate local branch contains a campaign/gameplay implementation. |
| Reflog/unreachable scan | Reflogs contain only the documented OpenArcanum milestones; no unreachable commits reported | No discarded local gameplay line was found. |

Primary external source: [public upstream repository](https://github.com/Suvitruf/unity-arcanum), inspected 2026-09-07.
The public page independently displayed the same commit, 24-commit history, one branch, zero tags, and the README claims.

### Local work added after upstream

The 31 local commits add 6,676 lines across 75 changed paths. Gameplay-relevant additions are concentrated in:

- stable `ArcanumObjectId` identity and collision validation;
- a production object-sector loader and owned sprite lifecycle;
- typed in-memory persistent object/portal/movement state;
- a session coordinator and transactional portal animation;
- deterministic eight-direction source-grid navigation and physical click input;
- extensive edit-mode and real-sector validation;
- Original/Enhanced rendering and local HD replacement support.

They do not add inventory, character rules, combat, spell execution, AI, quest orchestration, save serialization, or a
shipping UI shell.

## Upstream claim versus published evidence

The upstream README labels the project “work in progress,” says its high-level list is partial, and says code is being
opened module by module after documentation. It claims world streaming, composite characters, click movement,
doors/containers/inventory/equipment, real-time and turn-based combat, dialogue/quests, and followers.

The published tree at the claimed commit contains parsers, art/map demos, dialogue/script tests, audio services, a
`Weapon` data model, a `Race` enum, event contracts, and a mutable `WorldObject` data carrier. It contains no inventory
controller or transaction service, equipment rules, character/stat engine, `CombatMath`, combat controller, spell
runtime, AI/follower controller, quest journal runtime, save system, or production gameplay UI. The only `ScriptVm` and
`DialogConversation` runtime caller is `Runtime/Demo/DialogScriptGallery`, whose nested harness uses fake `object`
instances, fixed stats/skills, in-memory flags, and logging/no-op methods for combat, spells, item transfer, teleport,
portals, and quests.

Accordingly:

1. README claims are useful discovery leads and design intent.
2. A subsystem is reusable only when a concrete source path and callable implementation exist.
3. No unpublished/README-only system is assigned grade E or counted toward completion.

## Comprehensive parity matrix

Evidence abbreviations used in the table:

- **World runtime:** `Assets/_Game/Scripts/Runtime/World/`
- **Formats:** `Assets/_Game/Scripts/Formats/`
- **Demo:** `Assets/_Game/Scripts/Runtime/Demo/`
- **Script:** `Assets/_Game/Scripts/Script/`
- **Docs:** `documentation/`

| Domain / component | Grade | Local evidence and production call path | Public-upstream comparison | Recommendation |
|---|:---:|---|---|---|
| Foundations: DAT/VFS and MES reading | A | `Formats/Database`, `Formats/Text`; used by real-sector loaders and validated against clean data | Same core is upstream | Reuse unchanged; harden malformed-data diagnostics only as needed. |
| Foundations: ART/BMP decoding and resolvers | A | `Formats/Art`, `World/ArtTextureFactory`; production object/terrain presentation | Same base, with local identity/HD/lifecycle hardening | Keep source semantics authoritative. |
| Foundations: sector/map/area/jump/world-map readers | C | `Formats/World` has readers; sector loading is live, but shared terrain/object transitions and world travel are not | Same parser set upstream | Reuse readers; build a typed map/session layer around them. |
| Foundations: object/prototype field decoding | B | `Formats/Objects` parses extensive critter/item/script/inventory fields; production loader copies many into `WorldObject` | Same broad parser core upstream; local identity fixes add value | Reuse, but translate raw arrays/flags into typed gameplay state. |
| Foundations: stable object identity | A | `ArcanumObjectId`, `PersistentObjectState`, `WorldMapSessionCoordinator`; real-sector validation | Local-only | Make this the authoritative key for all future state and saves. |
| Foundations: production sector object lifecycle | A | `WorldObjectSectorLoader` + `WorldObjectSpriteOwner`; validated load/rebuild/unload/reload | Local-only; upstream relied on demos | Extend narrowly; do not make sprite ownership the gameplay authority. |
| Foundations: portal state/animation | A | Session-owned `PortalTransitionScheduler`; rollback and interrupted-transition tests | Local-only | Reuse as the model for transactional object actions. |
| Foundations: sector-local click navigation | A | Deterministic source-grid A*, input/controller/follower; physical Game-view validation | Public upstream has no published A* implementation despite README claim | Reuse; add cross-sector ownership and dynamic reservations separately. |
| Foundations: cross-sector traversal | F | Explicitly listed as remaining in `player-navigation.md`; no shared terrain/object sector selector | Only data readers upstream | New implementation required. |
| Foundations: production PC creation/binding | F | Navigation validation uses a real-NPC fallback; no character/save selection lifecycle | Upstream has only race/art demo pieces | New authoritative PC aggregate required. |
| Interaction: targeting, use/examine dispatch, range | F | Script numbers are stored on `WorldObject`, but no production dispatcher invokes them | No published upstream implementation | Build after PC binding; route through object identity and navigation. |
| Interaction: doors | C | Portal open/close and lock state exist; no key checks, use scripts, damage, lockpick, or AI policy | README claim is not present as published production code | Extend session action model; integrate script host and skills. |
| Interaction: containers/loot | D | Object/inventory fields and container events exist; no open/loot transaction or UI call path | No published controller | Build with inventory transactions and persistent containment. |
| Interaction: ground items | D | Items can be parsed/rendered and parent relationships represented | No pick-up/drop service upstream | Build with the same transaction layer as containers. |
| Interaction: hover/examine feedback | F | `ExamineScriptNum` is data only; no shipping cursor/tooltip/floating-text flow | No published implementation | New UI plus script dispatch. |
| Inventory: containment model | C | `WorldObject.Inventory`, `ParentIdentity`, `InvLocation`; loader can attach parsed contents | Same mutable carrier upstream, without local stable identities | Preserve parsed meaning, replace ad-hoc mutation with atomic commands. |
| Inventory: pick up/drop/transfer | F | `ItemMove` events exist but have no producers; gallery only logs transfers | No published implementation | New validated transaction service. |
| Equipment: slots/wield state | D | `InvLocation` documents worn slots 1000–1008; script host can query conceptually | No rules/controller upstream | Create typed slots, eligibility, derived-stat invalidation, visuals. |
| Equipment: paper-doll visual composition | C | `CharacterArtGallery` and critter art resolution demonstrate combinations | README claim exceeds published runtime | Extract resolver logic; do not promote gallery state to production. |
| Items: stacks, charges, durability | D | Ammo/gold quantities and spell mana are parsed; no stack/durability semantics | Same fields upstream | Implement from source behavior and content corpus. |
| Items: use, consumables, magic items | D | `ItemSpell`/`SpellMana` parsed; script VM exposes cast action | No runtime execution upstream | Defer until inventory and spell runtime share an action model. |
| Items: theft, ownership, stolen state | D | Item flags can be parsed; no ownership/crime system | No published implementation | New social/crime integration. |
| Character creation: race/gender | D | `Race` enum/body mapping and art resolver exist | Same upstream | New character-creation state/UI and source validation. |
| Character creation: attributes, backgrounds, portrait/name | D | Critter stat arrays parse; data documentation exists; no creation rules | No published implementation | Build a typed character schema from original tables/rules. |
| Character creation: skills, spells, tech degrees | D | Basic/tech/spell-tech arrays parse | No published rule engine | Implement allocation prerequisites and derived effects. |
| Character creation: equipment/start location | F | No creation finalization pipeline | No published implementation | Add after inventory and map-transition foundations. |
| Statistics: base/derived stats | D | Raw stat/resistance arrays on `WorldObject`; gallery returns constants | `Weapon` refers to absent `CombatMath` | New deterministic stat service and dependency graph. |
| Statistics: skills and checks | D | Raw ranks parsed; script VM delegates `GetSkill` | No roll/check implementation | New source-faithful check service shared by dialogue/combat/world actions. |
| Statistics: alignment/reaction/reputation | D | Reaction base parses; dialogue interfaces/globals expose concepts | Demo harness only mutates integers | New persistent social-state services. |
| Statistics: fatigue/HP/status | D | HP damage and resistances parse; VM delegates damage/heal | No authoritative health/status runtime | New state model before combat/spells. |
| Leveling: XP/levels/character points | D | Quest XP metadata and NPC XP-worth parse | README level-up claim has no published controller | New progression service with source tables and cap rules. |
| Leveling: followers and party XP | F | `IsFollower` flag and join/disband interface only | No published party/XP runtime | New party/progression orchestration. |
| Combat: weapon data | D | `Runtime/Combat/Weapon.cs` maps parsed damage/range/ammo/flags | Same upstream; referenced `CombatMath` is absent | Reuse model after verifying every field against source data. |
| Combat: attack resolution/damage/critical tables | F | Events and VM host verbs only; no math/controller | README claim not published | New deterministic rules engine required. |
| Combat: real-time mode | F | No scheduler, initiative, target/action state, or attack producer | No published implementation | Build on common combat state machine after turn-based rules core. |
| Combat: turn-based/AP/initiative | F | No implementation | No published implementation | New implementation; validate AP boundaries and mode transitions. |
| Combat: ranged/LOS/ammunition | D | Weapon/ammo fields parse; no LOS/projectile/ammo consumption | No published implementation | Integrate nav visibility, attack math, inventory transactions. |
| Combat: unarmed/melee/defense | D | `Weapon.Unarmed()` data helper only | No published implementation | New rules and animation-event synchronization. |
| Combat: death/corpses/loot/XP | D | `IsDead`, HP damage, death/attack events exist; no lifecycle producers | No published implementation | Implement atomically with persistent object state. |
| Combat: conditions/critical effects | F | VM explicitly treats many exceptional states as unmodelled/false | No published implementation | New typed effect system; never encode as loose booleans. |
| Magic: spell definitions/known spells | D | Spell-tech/item-spell fields and VM condition/action hooks | No spell database/runtime found | Add source table ingestion and character spellbooks. |
| Magic: casting, mana/fatigue, targeting | F | Gallery logs `SAT_CAST_SPELL`; no runtime | No published implementation | New action/effect pipeline. |
| Magic: maintained effects/dispel/resistance | F | Several script states intentionally return false | No published implementation | New effect lifecycle and save representation. |
| Technology: disciplines/schematics/crafting | D | Tech arrays and written schematic fields parse | No runtime upstream | Build after inventory and stats; validate ingredients and aptitude. |
| Technology: aptitude interactions | D | Map/object metadata and documentation only | No runtime upstream | Centralize aptitude modifiers rather than duplicating in systems. |
| Dialogue: DLG parsing and line graph | B | `Formats/Dialog` reader/conversation/evaluator; extensive edit-mode tests | Reusable upstream core | Reuse with corpus validation; connect to real PC/NPC state. |
| Dialogue: conditions/effects | C | Evaluator supports many tokens, but some tiers/location behavior are approximate or missing | Same upstream | Extend from observed corpus; fail loudly on unsupported semantics. |
| Dialogue: production UI and world pause/focus | F | Only IMGUI `DialogScriptGallery` test bench | No shipping UI upstream | New production presenter/input/state boundary. |
| Script: SCR parsing/database/control flow | B | `ScriptReader`, `ScriptDatabase`, engine-free `ScriptVm`, tests; 2,396 scripts previously load | Strong reusable upstream core | Preserve VM purity; measure opcode coverage across shipped scripts. |
| Script: production world host | F | Only `DialogScriptGallery.Harness` implements `IScriptHost`; it uses constants, logs, and no-ops | README/private project may have had a host, but none is published | Implement against session, stats, inventory, combat, quests, and UI. |
| Script: unsupported conditions/actions | C | 23 named state conditions return false; unknown conditions default true with a warning | Same upstream limitation | Add strict audit mode; never silently accept unknown campaign branches. |
| Script: heartbeat/use/examine lifecycle | D | Script slots and timers stored on `WorldObject`; no production executor | No published runtime caller | Build event scheduling after production host exists. |
| Quests: metadata/text/XP parsing | D | `Formats/Quest/QuestLog`; tests cover states and botched modifier | Reusable upstream reader | Reuse as immutable content metadata. |
| Quests: state transitions/global flags | C | `ScriptGlobals` and VM opcodes work in memory; only gallery/test ownership | Same upstream | Move into versioned campaign state with transition events. |
| Quests: journal UI/notifications | F | No runtime journal/presenter | README claim not published | New implementation driven by `QuestLog` metadata. |
| Quests: campaign sequencing | F | No production script host or persistent campaign state | No published implementation | Validate quest-by-quest against original saves/playthroughs. |
| World map: area discovery/town map flags | D | Readers and VM host verbs exist; gallery logs/no-ops | No production implementation | Put in campaign state; later connect UI. |
| World map: travel/encounters/time | F | No travel UI, route simulation, encounter tables, or game clock | No published implementation | New system after map/session and party state. |
| World: day/night/lighting/weather | D | Map/scheme metadata and docs exist; no production clock/lighting ownership found | README lighting claim is not present as a production system | Build a game-time service, then rendering/audio consumers. |
| AI: perception/hostility/packets | D | NPC flags, faction, AI packet parse; script interface names visibility/hearing/KOS hooks | No AI loop upstream | New perception and decision systems; source tables first. |
| AI: navigation/dynamic avoidance | C | Player pathfinding handles static source blockers; no critter occupancy/reservations | No published AI pathing | Reuse grid graph; add deterministic occupancy policy. |
| AI: combat behavior | F | No controller | No published implementation | Build after combat actions are stable. |
| Followers: recruit/disband/leader | D | Flags and script verbs exist; gallery only logs | README claim not published | New party aggregate and persistence. |
| Followers: formation/orders/equipment | F | No implementation | No published implementation | Build after shared movement and inventory. |
| Followers: dialogue/AI/combat integration | F | No production host or party loop | No published implementation | Campaign-test follower-specific script paths. |
| Economy: gold/worth/barter multipliers | D | Gold, worth, retail multiplier, substitute inventory parse | Barter documentation only; no runtime | New pricing and transaction services. |
| Economy: merchant stock/restock | D | Substitute inventory OID and buy-script number parse | No controller upstream | New scheduled merchant state. |
| Economy: barter UI/haggle | F | Dialogue context names Haggle; no barter flow/UI | No published implementation | Implement after inventory and social checks. |
| Social: reaction, factions, KOS | D | Fields parse; script VM delegates queries | No production social/AI system | New shared relationship service. |
| Social: crime/reputation/rumors | C | Script globals/interfaces support storage concepts; only demo/test state | No published production runtime | Add campaign-state stores and observable consequences. |
| UI: HUD, selection, action modes | F | Test scenes use IMGUI and direct click movement | No shipping UI upstream | New production UI architecture. |
| UI: inventory/character/combat/dialog/journal | F | No shipping presenters; dialogue gallery is a developer bench | No published implementation | Build presenters after domain APIs stabilize. |
| UI: accessibility/options/input rebinding | F | Graphics mode config exists; gameplay options layer absent | No published implementation | Add after the first playable slice, before broad content QA. |
| Audio: formats, SFX selection, WAV loading | B | Sound readers/selectors and `SoundBank`; substantive tests/docs | Reusable upstream core | Reuse and corpus-test. |
| Audio: playback/music/ambient director | B | `AudioService`, `MusicService`, `AudioDirector`; no scene/bootstrap instantiation found | Same upstream | Integrate through a production composition root and real event producers. |
| Audio: gameplay event producers | F | `GameEvents` raise methods have no production callers | No published producers | Emit only after successful authoritative state transitions. |
| Save/load: persistent schema and serialization | F | Session state is explicitly in-memory only | No upstream save system | New versioned, deterministic serialization is a prerequisite for campaign work. |
| Save/load: map objects/inventories/scripts/quests | F | Stable object IDs help, but no serialization or restoration contract | No upstream implementation | Snapshot domain state, not Unity scene objects. |
| Save/load: compatibility/migration/autosave | F | No implementation | No upstream implementation | Add schema migrations and golden-save fixtures early. |
| Campaign validation/tooling | C | Strong parser/edit-mode and real-sector milestone validators; no gameplay replay harness | Similar parser tests upstream; local validation is substantially stronger | Build deterministic scenario fixtures and parity traces per milestone. |

### Grade summary

The exact row count is less important than the distribution: grades A are confined to data/render/world-session/navigation
foundations; B/C cover reusable parser/evaluator/service cores and narrow prototypes; D dominates gameplay-shaped data;
and F dominates authoritative rules, orchestration, UI, persistence, and campaign behavior. Grade E is empty because the
audited branch already contains the entire published upstream history. No row required grade G after source inspection.

## Reuse map and risk

### Green — reuse largely as-is

- DAT archive/VFS, MES, BMP/ART, tile, object, sector/map/area/jump readers.
- Stable `ArcanumObjectId` and local collision checks.
- Immutable dialogue/script/quest/sound data models and locators.
- Deterministic pathfinder core and source-direction mappings.
- HD replacement fallback policy, which is orthogonal to gameplay.

These components are bounded and mostly deterministic. Their main risk is malformed or previously unseen source data,
not architectural ownership.

### Yellow — adapt behind a new production boundary

- `ObjectInstanceReader` and raw `WorldObject` gameplay fields: valuable data, but arrays/flags/mutable lists must feed
  typed domain state rather than become the rules engine.
- `DialogConversation`, `DialogScriptEvaluator`, `ScriptVm`, and `ScriptGlobals`: substantial logic, but the production
  host, persistence, strict unsupported-opcode policy, game clock, focus resolution, and world side effects are absent.
- `QuestLog`: useful metadata, not a quest lifecycle.
- `SoundBank`, audio services/director, and `GameEvents`: useful consumers/contracts, but need a composition root and
  authoritative producers.
- Character/critter art resolvers: reusable presentation logic, but gallery state must not own gameplay equipment.
- Player navigation: solid for static sector traversal, but cross-sector selection, dynamic critters, combat movement,
  and interaction range require explicit layers.

### Red — do not promote or merge as architecture

- `DialogScriptGallery.Harness` as a world implementation. It deliberately uses fake objects, hard-coded statistics,
  and log/no-op side effects; it is an excellent test bench and a dangerous production seed.
- Demo scene ownership (`TileMapDemo`, galleries) as the global gameplay composition root.
- `WorldObject` as the sole campaign save model. It is a Unity presentation component with mutable public fields and
  should project authoritative typed state, not define serialization or rules.
- README-only upstream implementations. There is no code to assess, test, merge, or maintain.
- Permissive script fallbacks in campaign mode. Treating an unknown condition as true can choose the wrong dialogue or
  quest branch; strict telemetry/corpus coverage is required before claiming parity.

## Architectural conflict map

| Boundary | Current pressure | Required resolution |
|---|---|---|
| Presentation vs. authority | `WorldObject` carries render references and gameplay-like mutable fields | Keep session/domain state authoritative; make MonoBehaviours projections and input adapters. |
| Static identity vs. dynamic objects | Static IDs are solved; creation/destruction/inventory ownership are not | Define deterministic dynamic IDs, tombstones, containment, and map ownership before loot/quests. |
| Demo host vs. campaign host | VM interfaces are broad, but only the gallery implements them | Compose a production host from small domain services; avoid one monolithic scene controller. |
| Raw source fields vs. typed rules | Stats, flags, skills, resistances and inventory locations are arrays/integers | Translate once at load boundaries; preserve raw values for diagnostics. |
| Events vs. state changes | Audio/game events exist without producers | Domain commands commit state first, then emit facts; subscribers never decide gameplay. |
| Navigation vs. interaction/combat | Player movement owns a static route through one sector | Add shared sector selection, destination intents, range stopping, occupancy, and action cancellation. |
| In-memory state vs. campaign state | Validated session state has no serialization | Introduce a versioned save schema before the number of mutable systems multiplies. |
| Approximation vs. parity | VM and dialogue include explicit approximations/defaults | Track coverage from original content and fail validation on unknown behavior. |

## Recommended roadmap

Each milestone should be a small, reviewable vertical slice with original-data fixtures and no proprietary data committed.

### M1 — Shared map/sector session and production PC lifecycle

Deliver one authoritative selection boundary for terrain and object sectors, cross-sector route continuation, deterministic
dynamic-PC identity, spawn/bind/unbind, and graphics rebuild survival. Effort: **medium**. Risk: ownership and coordinate
seams. Validate repeated boundary crossings, interrupted routes, reloads, and zero duplicate/leaked owners.

### M2 — Interaction kernel and object commands

Deliver target selection, use/examine intents, range approach/stop, transactional door/container actions, cancellation,
and success/failure results. Effort: **medium**. Risk: prematurely embedding rules in presentation. Validate real doors,
locked states, unreachable targets, concurrent commands, unload rollback, and source animation timing.

### M3 — Inventory, containment, ground items, and equipment

Deliver typed containment and slots, deterministic dynamic IDs, atomic pickup/drop/transfer/equip/unequip, quantities,
capacity, and presentation projection. Effort: **large**. Risk: object ownership and save compatibility. Validate nested
real inventories, worn locations 1000–1008, stack boundaries, failed transfers, unload/reload, and art pivots.

### M4 — Character state, statistics, skills, and progression

Deliver PC/NPC typed attributes, derived stats, HP/fatigue, skills, alignment/reaction inputs, XP, level-up, and creation
allocation rules. Effort: **large**. Risk: undocumented formulas and ordering. Validate golden calculations against the
original/decompilation across races, backgrounds, aptitude, equipment changes, damage/heal, and level thresholds.

### M5 — Production script host, dialogue, and minimal quest vertical slice

Connect real focus objects, globals, local script state, use/examine/dialog attachment points, dialogue UI, quest
transitions, journal entries, and strict opcode telemetry. Effort: **large**. Risk: broad cross-domain side effects and
silent fallback branches. Validate a small start-to-finish original quest with alternate gender/intelligence/skill paths,
save/reload checkpoints, and zero unsupported executed opcodes.

### M6 — Versioned save/load foundation

Serialize maps, PC/party, dynamic/static objects, containment, portal state, script globals/locals, quests, clock, and RNG
state without serializing Unity objects. Effort: **large**. Risk: schema churn and non-determinism. Validate golden saves,
round-trip equality, migrations, cross-sector saves, and corrupted-save diagnostics.

### M7 — Map transitions, town/world map, clock, and travel

Deliver teleport/jump-point handling, area discovery, game time/day-night state, world-map travel, and encounter hooks.
Effort: **large**. Risk: coordinate/map-index semantics and time side effects. Validate scripted and physical transitions,
known-area flags, travel time, save/load mid-route, and return positions.

### M8 — Combat rules core and turn-based vertical slice

Deliver deterministic attack/damage/defense/critical math, AP, initiative, melee/ranged/ammunition, death, loot, and XP
using the character/inventory foundations. Effort: **very large**. Risk: formula/order fidelity. Validate golden combat
traces, seeded RNG, resistances, critical tables, ammo transactions, corpse persistence, and mode entry/exit.

### M9 — Real-time combat, conditions, and combat presentation

Add real-time scheduling, animation/event synchronization, status/critical effects, interruption, feedback, and combat
audio/UI. Effort: **large**. Risk: timing drift between rules and animation. Validate identical seeded outcomes across
frame rates and real-time/turn-based transitions.

### M10 — Magic and technology

Deliver spell/schematic databases, known abilities, targeting, fatigue/mana/charges, effect lifecycle, dispel/resistance,
crafting, and aptitude interactions. Effort: **very large**. Risk: content breadth and persistent effects. Validate each
college/discipline family with table-driven fixtures and save/load during maintained effects.

### M11 — AI, hostility, and followers

Deliver perception, KOS/faction/reaction gates, schedules/heartbeats, dynamic movement reservations, combat decisions,
party formation/orders, follower equipment, and join/leave persistence. Effort: **very large**. Risk: emergent behavior and
script coupling. Validate representative guards, animals, undead, civilians, merchants, and named followers.

### M12 — Economy, social systems, and production UI/audio shell

Deliver barter/haggle, merchant inventory/restock, crime/theft, reputation/rumors, HUD, character/inventory/combat/journal
screens, settings/accessibility, and audio composition/event producers. Effort: **very large**. Risk: many cross-system
edge cases. Validate transactions and reactions before/after save, input rebinding, resolution/UI scaling, and source SFX.

### M13 — Campaign parity program

Execute deterministic map/quest/dialog/combat scenario suites, content-corpus opcode coverage, long-session save migration,
performance/memory checks, and chapter-by-chapter playthrough comparison. Effort: **program-scale**. Risk: long-tail content
and rare script branches. Exit only when every main path, alternate resolution, follower arc, and ending dependency has a
recorded parity result or an explicit accepted exception.

## Immediate next five milestones

1. Shared map/sector session plus production PC lifecycle.
2. Interaction kernel and transactional object commands.
3. Inventory/containment/equipment with dynamic identities.
4. Typed character/stat/skill/progression model.
5. Production script host + dialogue + one complete quest vertical slice.

This ordering intentionally establishes authoritative state and commands before connecting the very broad script-host
surface. It also delays combat until characters, items, persistence boundaries, and deterministic actions are real.

## Completion estimates

These estimates weight executable player-facing behavior and campaign reliability, not source-file count or parser count.
They are planning ranges, not earned-value measurements.

| Target | Estimate | Rationale |
|---|---:|---|
| First playable vertical slice | **30–35%** | World/data/render/navigation foundations are unusually strong, but a slice still needs a real PC, interaction, inventory, character rules, dialogue/quest integration, basic combat, UI, and save/load. |
| Substantial campaign coverage | **10–15%** | Parsers and script/dialog cores reduce discovery work, yet campaign state, broad opcode semantics, AI, followers, economy, magic, transitions, saves, and content validation remain. |
| Complete gameplay parity | **5–8%** | Nearly all systemic RPG rules and long-tail campaign verification are unimplemented; the strongest completed work is foundational rather than feature-complete gameplay. |

The confidence level is **medium**. Source presence and call paths are high-confidence; effort and percentage estimates
necessarily depend on fidelity criteria and undiscovered content edge cases.

## Validation strategy for future parity claims

- Maintain three evidence levels: parser fixture, deterministic domain test, and physical production-scene validation.
- Scan all shipped scripts/dialogue for actually used opcodes/tokens and require zero unclassified executed behavior.
- Use seeded RNG and golden traces for statistics, combat, travel encounters, AI, and loot.
- Create legal fixture descriptors/hashes from local original data; never commit extracted proprietary content.
- Validate Original graphics mode on every gameplay milestone; Enhanced must remain a presentation-only substitution.
- Round-trip save/load at each new mutable domain boundary and test schema migration from every released format.
- Record parity discrepancies as behavioral cases with source evidence, not as general “close enough” notes.
- Treat README/docs as hypotheses until a concrete production call path and validation result exist.

## Final answer to the audit question

OpenArcanum is **well positioned for continued implementation but not close to campaign gameplay parity**. The audited
upstream has no additional published gameplay branch to merge, so upstream archaeology does not unlock a shortcut. The
project should reuse the strong format, dialogue/script, audio, identity, session, rendering, and navigation foundations;
adapt them behind typed state and explicit production boundaries; and implement the missing RPG systems as small,
validated vertical slices. The audit makes the path clearer and reduces avoidable rework, but complete parity remains a
multi-system, campaign-scale program.
