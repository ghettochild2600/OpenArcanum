# OpenArcanum Gap Matrix

Audit date: 2026-10-11
Audited commit: `d0740332afeef46637d298063693371861f6cb07`

This matrix contains **60 confirmed gaps**. The first 50 are ranked by player impact, dependency leverage, and breadth of source content blocked. Rank is not implementation permission; each item still requires a single-issue source trace and regression proof.

Evidence abbreviations: `Retail` = pristine retail data; `CE` = `arcanum-ce`; `Manual` = official manual; `Prima` = Prima guide; `OA` = current OpenArcanum source/tests/audits.

## Compatibility catalog

This is the canonical catalog view using the requested schema. The detailed ranked table below adds exact current behavior, likely files, and scope.

| SYSTEM | FEATURE | MANUAL/PRIMA EVIDENCE | SOURCE/DATA EVIDENCE | OPENARCANUM STATUS | SEVERITY | PLAYER IMPACT | RECOMMENDED SINGLE-ISSUE TASK |
|---|---|---|---|---|---|---|---|
| Magic | 80-spell production catalog | Manual spell appendix; Prima pp. 62–74 | CE spell handlers; retail MES | `PARTIAL` (4/80) | Critical | Most magic unavailable | Add one source spell family with tests (Large) |
| Dialogue/Scripting | Campaign opcode corpus | Manual dialogue/quest flow | CE handlers; retail scripts | `PARTIAL` | Critical | Campaign branches stop | Audit and implement one unsupported opcode (Large) |
| Travel | Random encounters | Manual world travel | CE worldmap; retail encounter data | `MISSING` | Critical | Travel loop incomplete | Implement source encounter selection (Large) |
| Maps | Local map | Manual map controls | CE UI/data; retail art | `MISSING` | High | Player lacks local navigation map | Build source local-map authority and UI (Large) |
| Attributes | Poison runtime | Manual poison/recovery/cures | CE poison ticks; retail effects | `PARTIAL` | High | Poison items/spells/backgrounds wrong | Add authoritative poison state/ticks (Large) |
| Creation | Unsupported Background effects | Manual/Prima Background descriptions | CE effects; retail effect MES | `PARTIAL` | High | Legal characters are blocked | Implement one typed Background effect family (Large) |
| Attributes | Complete contributor graph | Manual derived-stat rules | CE `stat_level_get`; retail effects | `PARTIAL` | Critical | Many values omit legal modifiers | Add typed source contributor registry (Large) |
| Time/Sleep | Bed authority | Manual wait/sleep | CE `SAP_USE`; retail scripts | `MISSING` | High | Beds and recovery differ | Implement bed classification/use rules (Medium) |
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
| Creation | Pregens/auto-level schemes | Prima pregens; Manual auto-level | CE schemes; retail prototypes | `PARTIAL` | Medium | Pregens/auto-spending differ | Certify one pregen and scheme (Large) |
| Technology | Found-schematic acquisition | Prima schematics | Retail items/scripts; CE learning | `PARTIAL` | Medium | Recipes not campaign-reachable | Implement one acquisition/learn path (Large) |
| UI | Magic/technology sheets | Manual character interface | CE UI; retail art | `PARTIAL` | Medium | Advancement interface differs | Reconstruct one source panel (Medium) |
| Controls | Key remapping/command set | Manual quick keys | CE input; retail settings | `PARTIAL` | Medium | Some commands unavailable | Audit and add one command family (Medium) |
| Save/UI | Save/load metadata/presentation | Manual permanent screens | CE/retail UI | `PARTIAL` | Low | Functional but non-source UI | Add one source metadata behavior (Medium) |
| Inventory/UI | Specialized interfaces | Manual inventory/barter | CE UI; retail art | `PARTIAL` | Medium | Contexts look/behave generically | Rebuild one container mode (Medium) |
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
| 6 | `PARTIAL` | Legal Backgrounds with unsupported effect tokens cannot complete character creation. | Retail Background/effect MES; CE; `CharacterCreationRuntime.Validate` | Safe validation blocks reaction, critical, Speed, follower, XP, alignment, and expertise Backgrounds. | `Runtime/Creation/CharacterCreationRuntime.cs`, stats/social/combat | L |
| 7 | `PARTIAL` | The source effect/contributor graph is incomplete across stats. | Retail effects; CE `stat_level_get`; OA derived services | Core formulas work, but equipment, status, sector, time, and environment contributors are absent or fragmented. | `Runtime/Character/*`, equipment, magic, world time | XL |
| 8 | `MISSING` | Bed sleeping lacks occupied/rented/free authority and source `SAP_USE` semantics. | CE use/sleep path; Manual; OA sleep audit | Waiting works; beds do not reproduce source sleep access and time rules. | `Runtime/World/SleepLocationSource.cs`, use scripts, HUD | M |
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
| 30 | `PARTIAL` | Item inspection/identification does not expose all source properties. | Retail item data/UI; Divination; Manual | Inventory works, but complete identified/unidentified descriptions and properties are not certified. | inventory UI, prototype resolver, magic | M |
| 31 | `MISSING` | Spot Trap passive detection and training behavior are incomplete. | CE; Manual | Trap visibility is not governed by a complete recurring source skill check. | world trap state, perception, HUD | M |
| 32 | `MISSING` | Gambling skill does not alter merchant inventory by training rank. | Manual; CE commerce | Merchant kernel ignores the source Apprentice/Expert/Master reveal rules. | `EconomyRuntime.cs`, skills, merchant UI | M |
| 33 | `PARTIAL` | Heal skill lacks the complete bandage, critical, and crippling-injury loop. | Manual; CE | Vitality can change, but source action costs/outcomes/training are incomplete. | vitality, inventory, skill action, injury state | M |
| 34 | `PARTIAL` | Haggle training effects are incomplete. | Manual; CE merchant code | Pricing exists; markup reporting, buy-anything, and Master NFS sale are not all implemented. | economy, merchant UI, skills | M |
| 35 | `PARTIAL` | Persuasion training does not fully control wait time, follower cap, and alignment bypass. | Manual; CE party/dialogue | Some follower-cap integration exists; complete training behavior is not certified. | party/social/dialogue/skills | M |
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
| 50 | `PARTIAL` | Pregenerated characters and auto-level schemes are not fully source-certified. | Retail prototypes/schemes; CE; Prima | UI offers bounded pregens/creation, but full stats, inventories, biographies, and automatic spending behavior are not established. | creation UI/runtime, progression, source loaders | L |

## Additional confirmed gaps

| ID | Classification | Exact defect | Evidence | Current behavior | Likely implementation surface | Scope |
|---:|---|---|---|---|---|---|
| 51 | `PARTIAL` | Found-schematic acquisition and campaign reachability are not complete. | Retail items/scripts; Prima | Catalog can describe recipes; every schematic pickup/learn path is not campaign-certified. | crafting, item use scripts, campaign/save | L |
| 52 | `PARTIAL` | Magic/technology character-sheet interfaces are not source-complete. | Retail UI; Manual; OA UI audit | Generic/bounded views exist without full source navigation/help/feedback. | `RetailCharacterSheet.cs`, magic/technology presenters | M |
| 53 | `PARTIAL` | Options and key remapping do not cover the complete retail command set. | Retail key list; Manual quick keys; OA UI audit | Keyboard control exists, but all commands/remapping/persistence are not certified. | `ProductionKeyboardController.cs`, options/save | M |
| 54 | `PARTIAL` | Save/load screen presentation and metadata are not source-faithful. | Retail UI; OA save audits | Slot persistence works with a bounded presentation. | save presenters/panel, screenshots/metadata | M |
| 55 | `PARTIAL` | Merchant, barter, corpse, and special-container source skins/interactions are incomplete. | Retail UI; Manual; OA UI audit | Generic inventory panels provide function without every source mode. | inventory/economy UI presenters | M |
| 56 | `PARTIAL` | Critical success/failure effect corpus is incomplete. | Retail critical tables; CE; Manual | Core critical resolution exists, but every weapon/location/result consequence is not certified. | combat resolution, injury/item state | L |
| 57 | `PARTIAL` | Called-shot arm/leg/head consequences are incomplete. | CE combat; Manual | Targeting/penalties exist at a boundary; full injury/status effects do not. | combat called locations, statuses, UI | M |
| 58 | `PARTIAL` | Armor, ammunition, and equipment special properties are not fully integrated into combat. | Retail prototypes; CE | Core equipment/resistance paths exist, but the complete property corpus is not active. | item property loader, equipment, combat | XL |
| 59 | `PARTIAL` | Hostility, surrender, retreat, fleeing, and combat-exit rules are incomplete. | CE AI/combat; retail scripts | Basic hostile combat runs; full disposition and exit transitions do not. | combat state, AI, social runtime | L |
| 60 | `PARTIAL` | Source weather/light/roof/shadow presentation and gameplay coupling are incomplete. | Retail runtime/assets; OA rendering audits | World renders, but source roof fading, placed lighting, shadows, and visibility coupling are incomplete. | world rendering, time/weather, perception | XL |

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
