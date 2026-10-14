# OpenArcanum Gap Matrix

Audit date: 2026-10-11
Current-branch contributor reconciliation: 2026-10-13 against `bad3431`.

This matrix contains **60 confirmed gaps**. The first 50 are ranked by player impact, dependency leverage, and breadth of source content blocked. Rank is not implementation permission; each item still requires a single-issue source trace and regression proof.

Evidence abbreviations: `Retail` = pristine retail data; `CE` = `arcanum-ce`; `Manual` = official manual; `Prima` = Prima guide; `OA` = current OpenArcanum source/tests/audits.

## 2026-10-12 reconciliation findings

- `bad3431` completed the typed source contributor framework and migrated race, sex, supported Background attributes/skills/resistances/Speed/aptitude, Persuasion follower-cap training, and maintained attribute spells. Remaining gaps are missing producer/effect families, not missing shared modifier infrastructure.

- `d074033` completed source Background visibility, eligibility, numeric ordering, wrapping, and race/sex rebuilding. The remaining gap is effect execution and fail-closed finalization for unsupported effect families.
- All twelve pregenerated characters, their source-prototype inventories/direct-start path, and ordinary auto-level target families are implemented. The prior broad pregen/auto-level gap is replaced by the exact scheme-76 tokenization mismatch.
- Character Creation Magic/Technology rows, technology expertise projection, the starting Human Shopkeeper, and world corpse inspection are implemented. Remaining UI gaps are explicitly limited to post-creation Magic/Technology/Schematics and other runtime barter/container modes.
- World and Inventory inspection exist. The remaining item-inspection gap is identified/unidentified property projection, not absence of inspection.
- Targeted source/test checks found no later implementation for the other listed gaps. The production tree has not changed since `d074033`; `5926bb4` added only the original reference documents.

## Compatibility catalog

This is the canonical catalog view using the requested schema. The detailed ranked table below adds exact current behavior, likely files, and scope.

| SYSTEM | FEATURE | MANUAL/PRIMA EVIDENCE | SOURCE/DATA EVIDENCE | OPENARCANUM STATUS | SEVERITY | PLAYER IMPACT | RECOMMENDED SINGLE-ISSUE TASK |
|---|---|---|---|---|---|---|---|
| Magic | 80-spell production catalog | Manual spell appendix; Prima pp. 62–74 | CE spell handlers; retail MES | `PARTIAL` (4/80) | Critical | Most magic unavailable | Add one source spell family with tests (Large) |
| Dialogue/Scripting | Campaign opcode corpus | Manual dialogue/quest flow | CE handlers; retail scripts | `PARTIAL` | Critical | Campaign branches stop | Audit and implement one unsupported opcode (Large) |
| Travel | Random encounters | Manual world travel | CE worldmap; retail encounter data | `MISSING` | Critical | Travel loop incomplete | Implement source encounter selection (Large) |
| Maps | Local map | Manual map controls | CE UI/data; retail art | `MISSING` | High | Player lacks local navigation map | Build source local-map authority and UI (Large) |
| Attributes | Poison runtime | Manual poison/recovery/cures | CE poison ticks; retail effects | `PARTIAL` | High | Poison items/spells/backgrounds wrong | Add authoritative poison state/ticks (Large) |
| Attributes | Contributor framework and remaining producers | Manual derived-stat rules | CE `stat_level_get`; retail effects | `COMPLETE` framework / `PARTIAL` coverage | High | The shared typed registry, source operation order, ownership, removal, and reconstruction are complete; equipment, status, sector, time, environment, and many content-specific producers remain absent | Add one missing producer family (Medium/Large) |
| Time/Sleep | Bed authority | Manual wait/sleep | CE `SAP_USE`; retail scripts | `MISSING` | High | Beds and recovery differ | Implement bed classification/use rules (Medium) |
| Creation | Unsupported Background effects | Manual/Prima Background descriptions | CE effects; retail effect MES | `PARTIAL` | High | Eligibility/order are correct; selecting an unsupported effect remains visible and finalization fails closed | Implement one typed Background effect family (Medium) |
| Followers | Selected-follower commands | Manual party HUD/keys | CE party UI/commands | `PARTIAL` | High | Core party control missing | Add one source follower command path (Large) |
| Logbook | Six non-quest domains | Manual permanent screens | CE/retail journal data | `PARTIAL` | High | Major journal information absent | Implement one journal domain end-to-end (Large) |
| Skills | Prowling | Manual training rules | CE skill/detection code | `MISSING` | High | Stealth build unusable | Implement Prowling action/detection (Large) |
| Skills | Pick Pocket | Manual training rules | CE action/crime code | `MISSING` | High | Thief interaction unavailable | Implement pickpocket transaction (Large) |
| Skills | Disarm Traps | Manual training rules | CE trap code | `MISSING` | High | Trap skill unusable | Implement disarm outcome/component path (Large) |
| Skills | Repair | Manual training rules | CE repair/item condition | `MISSING` | High | Repair build unusable | Implement condition plus Repair action (Large) |
| Technology | Product runtime effects | Prima schematic catalog | CE item use; retail prototypes | `PARTIAL` | High | Most crafted items inert | Implement one product family (Large) |
| Technology | Technical Manuals | Manual expertise rule | CE expertise; retail manuals | `MISSING` | Medium | Expertise totals wrong | Add inventory manual contribution (Medium) |
| Combat | Firearms parity | Manual Firearms training | CE combat; retail weapons | `PARTIAL` | High | Gun builds behave incorrectly | Certify one firearm/ammo/training slice (Large) |
| Combat | Thrown weapons | Manual Throwing rules | CE thrown combat; retail weapons | `MISSING/PARTIAL` | High | Throwing build incomplete | Implement thrown attack family (Large) |
| AI | Spellcasting | Manual spell rules | CE AI/spells | `MISSING` | High | Mage enemies/followers wrong | Add source spell choice/cast AI (Large) |
| AI | Technology/item use | Prima item uses | CE AI/items | `MISSING` | High | Tech enemies/followers wrong | Add one item-use decision family (Large) |
| Magic/Party | Summons/familiar | Manual/Prima spells | CE summon ownership | `MISSING` | High | Multiple colleges unusable | Add summoned-actor lifecycle (Large) |
| Magic | Corpse/resurrection spells | Manual spell appendix | CE corpses/spells | `MISSING` | High | Necromancy incomplete | Implement one corpse spell path (Large) |
| Magic/Social | Charm/control/fear | Manual spells | CE effects/AI | `MISSING` | High | Mental/Nature spells absent | Add temporary allegiance effect (Large) |
| Magic/Travel | Teleport/spatial spells | Manual Conveyance | CE spell/world code | `MISSING` | High | Conveyance unavailable | Implement one spatial targeting primitive (Large) |
| Combat/Magic | Spatial areas/walls/clouds | Manual spell/explosive descriptions | CE spatial effects; retail data | `MISSING` | High | AOE and explosives absent | Add persistent spatial-effect authority (Large) |
| Magic | Anti-magic/dispel/reflection | Manual Meta college | CE magic effect graph | `MISSING` | High | Meta college unavailable | Add query/remove/suppress effect API (Large) |
| Status | Timed conditions/transformations | Manual spells/items | CE effects; retail prototypes | `PARTIAL` | High | Many spells/items cannot work | Add one typed timed condition (Large) |
| Equipment | Critter appearance | Manual equipment feedback | CE art selection; retail ART | `MISSING` | Medium | Equipped character looks wrong | Resolve equipment-driven critter art (Large) |
| Items | Condition/durability | Manual Repair/item rules | CE item state; retail prototypes | `MISSING` | High | Repair/breakage unavailable | Add condition lifecycle (Large) |
| Inventory | Inspection/identification | Manual inspection/help | CE/retail item fields | `PARTIAL` | Medium | Item facts hidden/wrong | Implement identified property projection (Medium) |
| Skills | Spot Trap | Manual training rules | CE detection/traps | `MISSING/PARTIAL` | High | Traps not detected faithfully | Add passive spot checks (Medium) |
| Skills/Economy | Gambling inventory effects | Manual training rules | CE merchant filtering | `MISSING` | Medium | Training has no benefit | Implement inventory reveal rules (Medium) |
| Skills | Heal action | Manual training rules | CE heal/injuries | `PARTIAL` | Medium | Healing build incomplete | Implement bandage/critical outcome (Medium) |
| Skills/Economy | Haggle training effects | Manual training rules | CE merchant rules | `PARTIAL` | Medium | Mastery rewards absent | Add one Haggle training effect (Medium) |
| Skills/Party | Persuasion training effects | Manual training rules | CE party/reaction | `PARTIAL` | Medium | Follower limits/join rules differ | Complete training modifiers (Medium) |
| Social | Crime/theft consequences | Manual social play | CE witnesses/crime; retail scripts | `PARTIAL` | High | Theft lacks source consequences | Implement witness-to-consequence slice (Large) |
| Followers | Lifecycle semantics | Manual follower rules | CE dialogues/scripts | `PARTIAL` | High | Join/leave/death cases differ | Certify one follower lifecycle (Large) |
| Followers/UI | Portrait/status HUD | Manual party HUD | CE UI; retail art | `PARTIAL` | Medium | Party condition unclear | Add source portrait/status projection (Medium) |
| Logbook | Rumors/Notes | Manual logbook | CE/retail journal records | `PARTIAL` | Medium | Story notes missing | Implement rumors/notes model and page (Medium) |
| Social/Logbook | Reputations | Manual reputation rules | CE/retail reputation data | `PARTIAL` | High | Social state/effects incomplete | Implement source reputation corpus slice (Large) |
| Effects/Logbook | Blessings/Curses | Manual logbook | CE/retail effects | `MISSING` | High | Persistent modifiers absent | Implement typed blessing/curse record (Large) |
| Combat/Logbook | Kills/Injuries | Manual logbook | CE statistics/injuries | `MISSING` | Medium | Combat history page absent | Add statistics projection and page (Medium) |
| Inventory/Logbook | Keys/keyring | Manual keyring | CE/retail key data | `MISSING` | Medium | Key handling/page absent | Add key ownership and projection (Medium) |
| Creation/Logbook | Background prose/page | Manual Backgrounds | Retail long text; CE UI | `PARTIAL` | Low | Biography page incomplete | Add source text pagination (Small) |
| World Map | Presentation/special routes | Manual world map | CE route/UI; retail art | `PARTIAL` | High | Routes/UI differ | Fix one source route/presentation rule (Large) |
| Travel/Time | Source travel duration | Manual time/travel | CE movement time | `MISSING` | Medium | Calendar advances incorrectly | Implement terrain/distance time cost (Medium) |
| Time/Environment | Gameplay coupling | Manual day/night rules | CE stat/spell/background code | `PARTIAL` | High | Time-dependent rules wrong | Connect one environment consumer (Large) |
| Audio/UI | Startup movies/audio | Manual/startup flow | Retail media; CE startup | `MISSING` | Medium | Source opening absent | Integrate ordered startup media (Medium) |
| Multiplayer | Multiplayer/chat | Manual multiplayer | CE/retail networking surfaces | `MISSING` | High | Whole advertised mode unavailable | Define/implement first networking boundary (Large) |
| Creation | Auto-level rule tokenization | Manual auto-level; Prima pregens | CE `auto_level_process_rule`; retail scheme 76 | `INCORRECT` | Medium | Scheme 76 stops when OpenArcanum rejects `dx 20 pickpocket 20`; CE applies DX 20 and consumes the tail | Match source tokenization for malformed source segments (Small) |
| Technology | Found-schematic acquisition | Prima schematics | Retail items/scripts; CE learning | `PARTIAL` | Medium | Recipes not campaign-reachable | Implement one acquisition/learn path (Large) |
| UI | Runtime Magic/Technology/Schematics screens | Manual permanent-screen interface | CE UI; retail art | `PARTIAL` | Medium | Character Creation rows are corrected; post-creation advancement/crafting screens remain generic or bounded | Reconstruct one runtime source panel (Medium) |
| Controls | Key remapping/command set | Manual quick keys | CE input; retail settings | `PARTIAL` | Medium | Some commands unavailable | Audit and add one command family (Medium) |
| Save/UI | Save-slot preview and source metadata | Manual permanent screens | CE/retail save UI | `PARTIAL` | Low | Functional generic list has no source slot thumbnail and displays UTC metadata rather than the source composition | Add source slot thumbnail/metadata projection (Medium) |
| Inventory/UI | Runtime barter/corpse/special-container interfaces | Manual inventory/barter | CE UI; retail art | `PARTIAL` | Medium | Starting Shopkeeper and world corpse inspection are corrected; other runtime modes remain generic | Rebuild one remaining container mode (Medium) |
| Combat | Critical effect corpus | Manual critical rules | CE tables; retail data | `PARTIAL` | High | Outcomes incomplete | Port one critical table family (Large) |
| Combat | Called-shot injuries | Manual called shots | CE hit-location effects | `PARTIAL` | High | Limb/head outcomes incomplete | Add one location consequence (Medium) |
| Items/Combat | Equipment special properties | Manual item/combat rules | CE/retail prototypes | `PARTIAL` | High | Stats/damage omit properties | Implement one property family (Large) |
| Combat/AI | Hostility/fleeing/combat exit | Manual combat behavior | CE AI/combat; retail scripts | `PARTIAL` | High | Encounters end incorrectly | Implement one retreat/exit rule (Large) |
| World/Rendering | Weather/light/roof/shadow coupling | Manual visual cues | CE renderer; retail assets | `PARTIAL` | Medium | Visibility and presentation differ | Implement one source compositor rule (Large) |

## Ranked top 50

| Rank | Classification | Exact defect | Evidence | Current behavior | Likely implementation surface | Scope |
|---:|---|---|---|---|---|---|
| 1 | `PARTIAL` | Production magic catalog contains only 4 of 80 source spells. | Retail spell MES; CE; Manual/Prima; `PhaseOneSpellCatalog` | Strength of Earth, Harm, Minor Healing, and Flash work; 76 spells are unavailable. | `Runtime/Magic/MagicRuntime.cs`, combat/world/party services | XL |
| 2 | `PARTIAL` | Retail script/campaign opcode coverage is not complete. | Retail script corpus; CE script handlers; OA VM/host | Unsupported operations fail closed, blocking unported quest/dialogue/world behavior. | `Scripts/Script/*`, `Runtime/Campaign/*`, dialogue/world host adapters | XL |
| 3 | `MISSING` | World travel has no source random-encounter resolution. | Retail encounter data; CE worldmap; Manual | Route execution cannot produce the retail encounter loop. | `Runtime/World/WorldMapTravel.cs`, campaign/combat loaders | L |
| 4 | `MISSING` | Local map presentation, reveal, and interactions are absent. | Retail UI/data; Manual; OA UI audit | Only map-transition infrastructure exists; player cannot use the source local map. | `Runtime/UI/*`, world discovery/presentation | L |
| 5 | `PARTIAL` | Poison progression, recovery ticks, display, and cures are not an authoritative runtime domain. | CE stat/poison code; Manual; Background/item/spell data | Poison resistance can be stored, but source poison gameplay is not complete. | character vitality/status, UI HUD, items, magic, save | L |
| 6 | `PARTIAL` | The contributor framework is complete, but equipment, status, sector, time, environment, and many content-specific producers are absent. | Retail effects; CE `effect_adjust_func`/`stat_level_get`; `CharacterContributorService` | Typed ownership, deterministic source operation order, exact removal, no stacking, and reconstruction work; missing domains still cannot contribute until their source producers exist. | equipment, status, magic, world time/environment | L per producer family |
| 7 | `MISSING` | Bed sleeping lacks occupied/rented/free authority and source `SAP_USE` semantics. | CE use/sleep path; Manual; OA sleep audit | Waiting works; beds do not reproduce source sleep access and time rules. | `Runtime/World/SleepLocationSource.cs`, use scripts, HUD | M |
| 8 | `PARTIAL` | Background eligibility/order and supported attribute, skill, resistance, Speed, and aptitude effects are complete, but reaction, critical, follower, XP, alignment, expertise, time, and environment effect families are not executable. | Retail Background/effect MES; CE; `CharacterCreationRuntime.Validate` | All source-legal records remain visible in source order; supported effects use one replaceable Background contributor owner, while unsupported effect records fail closed at finalization. | `Runtime/Creation/CharacterCreationRuntime.cs`, social/combat/world | M |
| 9 | `PARTIAL` | Selected-follower source HUD commands are incomplete. | Retail UI; Manual; OA party/UI audit | Party membership works, but Wait, Inventory, passive sheet, and source command interaction do not. | `Runtime/Party/*`, `RetailGameplayHud.cs` | L |
| 10 | `PARTIAL` | Six non-quest logbook domains are incomplete. | Retail UI/data; Manual; OA logbook audit | Quests work; Rumors/Notes, Reputations, Blessings/Curses, Kills/Injuries, Background, and Keys do not. | `Runtime/Campaign/JournalProjectionService.cs`, `RetailLogbook.cs`, save | XL |
| 11 | `MISSING` | Prowling player action and detection state are absent. | CE skill logic; Manual training table; OA HUD audit | Rank/training can be stored, but the action cannot be used faithfully. | skill runtime, player input, AI perception, HUD | L |
| 12 | `MISSING` | Pick Pocket action and detection/consequence loop are absent. | CE; Manual; retail scripts | Rank exists without source item-size, detection, planting, crime, and training behavior. | inventory/interaction/social/AI/HUD | L |
| 13 | `MISSING` | Disarm Traps player action and component reward loop are absent. | CE; Manual; retail trap data | Rank exists without a complete target/action/outcome implementation. | world interactions, skill runtime, inventory, HUD | L |
| 14 | `MISSING` | Repair action cannot operate on source item condition/durability. | CE; Manual; item prototypes | Rank exists, but repair loss/breakage/training semantics cannot run. | item state, inventory action, skill runtime, HUD | L |
| 15 | `PARTIAL` | Crafted products are mostly behaviorally inert or uncertified. | Retail schematics/prototypes; Prima; OA crafting tests | Generic recipes consume/create correctly; most 118 outputs lack their source effect. | `Runtime/Crafting/CraftingRuntime.cs`, items/combat/status/AI | XL |
| 16 | `MISSING` | Technical Manuals do not add Intelligence to discipline expertise. | Manual; CE technology code | Inventory ownership has no source expertise contribution. | technology runtime, inventory/equipment, derived stats | M |
| 17 | `PARTIAL` | Firearms lack complete source weapon/ammunition/training parity. | Retail prototypes; CE combat; Manual | General ranged kernels exist, but all firearm properties and training effects are not certified. | `Runtime/Combat/*`, item properties, skills | L |
| 18 | `MISSING` | Thrown-weapon combat and Throwing training are incomplete. | CE combat; Manual | Stored Throwing skill does not provide the complete action/range/training loop. | combat weapon/action, inventory projectiles, skills | L |
| 19 | `MISSING` | Spellcasting AI is absent. | CE AI; source spell catalog | Hostile AI chooses weapons/approach but does not evaluate/cast source spells. | `CombatAiController.cs`, magic targeting/costs | L |
| 20 | `MISSING` | Technology/item-use AI is absent. | CE AI; source item data | AI does not select salves, grenades, traps, devices, or therapeutic items by source rules. | AI driver, inventory, crafting products | L |
| 21 | `MISSING` | Summons, elementals, undead, decoys, and familiar ownership are absent. | Retail spells/items; CE; Manual/Prima | No general summoned-actor lifetime/control/persistence domain exists. | magic/crafting, party, AI, world spawn, save | XL |
| 22 | `MISSING` | Resurrection and corpse-animation magic are absent. | Retail spells; CE; Manual | Death/corpses exist, but Resurrect/Create Undead/Conjure Spirit cannot alter them. | magic, death/corpse, party/dialogue, save | L |
| 23 | `MISSING` | Charm, fear, control, and domination effects are absent. | Retail spells/social AI; CE | Reaction/hostility/party control cannot be temporarily overridden by source magic. | magic, social runtime, AI, party | L |
| 24 | `MISSING` | Teleportation and spatial spell placement are absent. | Conveyance spells; CE; Manual | World/local travel services are not connected to spell targeting and legality. | magic, world map, navigation, transitions | L |
| 25 | `MISSING` | Ground areas, walls, clouds, and explosive fields are absent. | Fire/Air/Water/Force spells; explosive schematics | Combat has no general persistent spatial-effect authority. | combat/world spatial effects, magic, crafting | XL |
| 26 | `MISSING` | Anti-magic, dispel, bonds, and reflection are absent. | Meta college; CE; Manual | Effects cannot be generically queried, suppressed, removed, blocked, or reflected. | magic effect registry/targeting | L |
| 27 | `PARTIAL` | Timed conditions and transformations are incomplete. | Spell/tech/critical data; CE | Blindness is a narrow example; stun, paralysis, petrify, invisibility, polymorph, forms, and timed drug states are absent. | character status/effect runtime, combat/UI/save | XL |
| 28 | `MISSING` | Equipment-driven critter appearance is not source-faithful. | Retail ART/prototypes; OA graphics audits | Equipping armor/weapons does not always select the correct source critter art. | world critter renderer, equipment presentation | L |
| 29 | `MISSING` | Item condition, durability damage, breakage, and repair are incomplete. | Retail prototypes; CE; Manual | Item ownership exists without the complete condition lifecycle. | inventory item state, combat, repair, save/UI | L |
| 30 | `PARTIAL` | Identified/unidentified item-property projection is incomplete. | Retail item data/UI; Divination; Manual | World-target hover and ordinary Inventory inspection are implemented; they do not yet expose the full source identified/unidentified property set. | inventory UI, prototype resolver, magic | M |
| 31 | `MISSING` | Spot Trap passive detection and training behavior are incomplete. | CE; Manual | Trap visibility is not governed by a complete recurring source skill check. | world trap state, perception, HUD | M |
| 32 | `MISSING` | Gambling skill does not alter merchant inventory by training rank. | Manual; CE commerce | Merchant kernel ignores the source Apprentice/Expert/Master reveal rules. | `EconomyRuntime.cs`, skills, merchant UI | M |
| 33 | `PARTIAL` | Heal skill lacks the complete bandage, critical, and crippling-injury loop. | Manual; CE | Vitality can change, but source action costs/outcomes/training are incomplete. | vitality, inventory, skill action, injury state | M |
| 34 | `PARTIAL` | Haggle training effects are incomplete. | Manual; CE merchant code | Pricing exists; markup reporting, buy-anything, and Master NFS sale are not all implemented. | economy, merchant UI, skills | M |
| 35 | `PARTIAL` | Persuasion training does not fully control wait time and alignment bypass. | Manual; CE party/dialogue | The Expert-or-Master follower-cap contribution is source-correct and reconstructed through the shared graph; the remaining training behaviors are not certified. | party/social/dialogue/skills | M |
| 36 | `PARTIAL` | Crime, theft detection, witness, and consequence integration are incomplete. | Retail scripts/AI; CE | Item transfer and hostility exist at boundaries, but the complete crime loop is absent. | social runtime, AI, inventory, campaign | XL |
| 37 | `PARTIAL` | Follower join/leave/death/resurrection semantics are not fully campaign-certified. | Retail dialogue/scripts; CE | Core party state works; all source dialogue and lifecycle conditions do not. | party, dialogue scripts, death, save | L |
| 38 | `PARTIAL` | Party HUD lacks source portrait identity and poison/status projection. | Retail UI; OA party audit | Followers appear in generic state without the complete source indicators. | `RetailGameplayHud.cs`, party/vitality/status | M |
| 39 | `PARTIAL` | Rumors/Notes lack source prose, timestamps, quelled state, ordering, and persistence. | Retail journal data; OA logbook audit | Tab fails closed or shows incomplete projection. | journal projection, scripts, save, logbook UI | M |
| 40 | `PARTIAL` | Reputations lack the complete source corpus/effects/presentation. | Retail reputation MES/scripts; Manual | Some social state exists, but logbook and all effects are not complete. | social/campaign state, journal, dialogue, save | L |
| 41 | `MISSING` | Blessings and Curses logbook/runtime domain is absent. | Retail effect/journal data; Manual | No complete acquisition, effect, display, removal, or persistence path. | effect runtime, campaign, logbook, save | L |
| 42 | `MISSING` | Kills and Injuries journal domain is absent. | Retail UI/data; Manual | Combat results do not project the source records into the logbook. | combat statistics/injury state, journal, save | M |
| 43 | `MISSING` | Key/keyring journal and key ownership semantics are absent. | Retail item/UI data; Manual | Keys behave as ordinary/incomplete items and the logbook page is unavailable. | inventory, locks, journal, save | M |
| 44 | `PARTIAL` | Character Background logbook prose/pagination is incomplete. | Retail Background long text; OA logbook audit | Creation selection exists, but full in-game Background page behavior does not. | creation state, journal projection, logbook UI | S |
| 45 | `PARTIAL` | World-map source presentation and special-route behavior are incomplete. | Retail map UI/data; CE; OA world-map audits | Generic destinations/routes work; source art, notes, and some route cases do not. | world-map presenter, route planner, UI assets | L |
| 46 | `MISSING` | Travel duration is not source-derived. | CE world-map time code; Manual | Route execution does not advance time by retail movement/terrain rules. | `WorldMapTravel.cs`, `SourceTimeService.cs` | M |
| 47 | `PARTIAL` | Day/night and environment do not feed every gameplay rule. | CE stat/spell/background code; retail data | Time service exists, but Background, visibility, spell, schedule, and lighting consumers are incomplete. | time service, stats, AI, rendering, magic | XL |
| 48 | `MISSING` | Startup movies are not integrated into the player flow. | Retail media/menu flow; OA certification audit | Game begins without the source intro sequence. | main menu/bootstrap, media/audio presentation | M |
| 49 | `MISSING` | Multiplayer and chat are not implemented. | Retail menu/manual | Entry is deferred/unavailable. | networking, session, UI, save/campaign | XL |
| 50 | `INCORRECT` | Auto-level parsing does not match CE tokenization for malformed source segments. | Retail `gamelevel.mes`; CE `auto_level_process_rule` | The twelve pregenerated prototypes and normal target families are implemented, but scheme 76's `dx 20 pickpocket 20` segment is rejected wholesale; CE applies DX 20 and consumes the trailing text. | `Runtime/Creation/CharacterCreationRuntime.cs` | S |

## Additional confirmed gaps

| ID | Classification | Exact defect | Evidence | Current behavior | Likely implementation surface | Scope |
|---:|---|---|---|---|---|---|
| 51 | `PARTIAL` | Found-schematic acquisition and campaign reachability are not complete. | Retail items/scripts; Prima | Catalog can describe recipes; every schematic pickup/learn path is not campaign-certified. | crafting, item use scripts, campaign/save | L |
| 52 | `PARTIAL` | Post-creation Magic, Technology, and Schematics screens are not source-complete. | Retail UI; Manual; OA UI audit | Character Creation's source rows and expertise projection are corrected; runtime advancement/crafting screens remain generic or bounded. | `RetailCharacterSheet.cs`, magic/technology/crafting presenters | M |
| 53 | `PARTIAL` | Options and key remapping do not cover the complete retail command set. | Retail key list; Manual quick keys; OA UI audit | Keyboard control exists, but all commands/remapping/persistence are not certified. | `ProductionKeyboardController.cs`, options/save | M |
| 54 | `PARTIAL` | Save/load slots lack the source preview composition and source-facing metadata. | Retail UI; OA save audits | Persistence works, but `ProductionSaveLoadPresenter` draws a generic list without slot thumbnails and formats host UTC timestamps. | save presenters/panel, screenshots/metadata | M |
| 55 | `PARTIAL` | Remaining runtime barter, corpse-inventory, steal, and special-container modes are not all source-faithful. | Retail UI; Manual; OA UI audit | The starting Human Shopkeeper and world corpse inspection are corrected; other runtime inventory modes still use generic/bounded presentation. | inventory/economy UI presenters | M |
| 56 | `PARTIAL` | Critical success/failure effect corpus is incomplete. | Retail critical tables; CE; Manual | Core critical resolution exists, but every weapon/location/result consequence is not certified. | combat resolution, injury/item state | L |
| 57 | `PARTIAL` | Called-shot arm/leg/head consequences are incomplete. | CE combat; Manual | Targeting/penalties exist at a boundary; full injury/status effects do not. | combat called locations, statuses, UI | M |
| 58 | `PARTIAL` | Armor, ammunition, and equipment special properties are not fully integrated into combat. | Retail prototypes; CE | Core equipment/resistance paths exist, but the complete property corpus is not active. | item property loader, equipment, combat | XL |
| 59 | `PARTIAL` | Hostility, surrender, retreat, fleeing, and combat-exit rules are incomplete. | CE AI/combat; retail scripts | Basic hostile combat runs; full disposition and exit transitions do not. | combat state, AI, social runtime | L |
| 60 | `PARTIAL` | Source weather/light/roof/shadow presentation and gameplay coupling are incomplete. | Retail runtime/assets; OA rendering audits | World renders, but source roof fading, placed lighting, shadows, and visibility coupling are incomplete. | world rendering, time/weather, perception | XL |

## Best Single-Issue Candidates

These are the smallest high-value production gaps after reconciliation. They are candidates only; this documentation task does not authorize implementation.

| Priority | Exact defect | Likely subsystem | Size | Dependency / blocker |
|---:|---|---|---|---|
| 1 | Scheme 76's `dx 20 pickpocket 20` segment is rejected instead of matching CE's tokenization. | `CharacterCreationRuntime.ApplyAutoLevelRule` plus focused M12B tests | SMALL | None; controlling parser behavior is known. |
| 2 | Background reaction modifiers remain unsupported even though the shared contributor framework is complete. | Background effect parser; social/reaction contributor producer | MEDIUM | Requires the source reaction target/rule; must reuse Background owner replacement rather than add a parallel modifier store. |
| 3 | Technical Manuals in inventory do not add the holder's Intelligence to one discipline's expertise. | `TechnologyRuntime`, inventory/prototype classification | MEDIUM | Needs authoritative manual-to-discipline mapping; no crafting rewrite. |
| 4 | Haggle Apprentice does not report the merchant's markup. | `EconomyRuntime`, merchant feedback/presenter | SMALL | Needs a source message/feedback route; pricing authority already exists. |
| 5 | The Background logbook tab does not paginate the finalized character's authored long text. | `JournalProjectionService`, `RetailLogbook` | SMALL | Finalized Background ID/text already exist; no effect implementation required. |
| 6 | Party HUD slots do not project follower portrait, health, and fatigue from existing party/vitality authority. | `RetailGameplayHud`, `PartyStateService`, vitality projection | MEDIUM | Keep poison/status out of this slice until the poison runtime exists. |
| 7 | World-map travel completes without source-derived elapsed time. | `WorldMapTravel`, `SourceTimeService` | MEDIUM | Requires source terrain/distance cost; keep random encounters separate. |
| 8 | Gambling training does not reveal the source merchant inventory tiers. | `EconomyRuntime`, skill training, merchant projection | MEDIUM | Requires reliable wielded/Not-for-Sale inventory classification. |
| 9 | Keys lack keyring ownership/projection and the Keys logbook page. | inventory item classification, locks, `RetailLogbook` | MEDIUM | Requires authoritative retail key identity mapping. |
| 10 | Save/load slots have no source preview thumbnail and show host UTC metadata in a generic list. | save slot metadata and `ProductionSaveLoadPresenter` | MEDIUM | Source-trace thumbnail capture/display and game-calendar text; do not broaden authoritative Save V1 gameplay data. |

## Source ambiguities excluded from the confirmed-gap count

| Topic | Ambiguity |
|---|---|
| Healing Salve prototype | Schematic output 10059 conflicts with the currently supported usable path 10079. |
| Trap Springer identity | Source naming/aliases do not yet prove one canonical runtime identity. |
| Troll Offspring effect | Retail effect text lacks a separator between CN +2 and DX +1. |
| Corpse interaction wording | Evidence proves inventory access, not every original verb/presentation detail. |
| Retail audio parity | Available physical retail run cannot certify audio because of the host/audio condition. |

## Ranking interpretation

The highest-ranked gaps unlock broad content or block core campaign play. Lower-ranked gaps can still be severe for fidelity. Before implementation, each row should be narrowed into one issue with exact source records, source-runtime/retail proof where possible, the smallest shared rule, focused regression, 800×600 physical verification when player-visible, and a local commit.
