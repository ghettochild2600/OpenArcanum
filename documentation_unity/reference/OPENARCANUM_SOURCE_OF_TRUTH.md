# OpenArcanum Source-of-Truth Audit

Audit date: 2026-10-11
Current-branch reconciliation: 2026-10-12 against `5926bb42ff0f1fe261ba8deb71716d149733c9d3`; the latest production commit remains `d0740332afeef46637d298063693371861f6cb07` (`feature/session-save-load`).

This is a source-first implementation audit, not a feature roadmap. It records what Arcanum requires, what OpenArcanum currently owns, and where evidence is incomplete. A completed infrastructure milestone is not evidence that all retail content which depends on that infrastructure is implemented.

## Evidence order

For gameplay/runtime truth, use this order and record disagreement instead of silently choosing:

1. `arcanum-ce` controlling source.
2. Pristine retail data, scripts, MES, and prototypes in `D:\OpenArcanum\Source\Steam-Clean`.
3. The 800×600 `arcanum-ce` source-runtime reference.
4. Official manual.
5. Prima guide.

For player-facing terminology, explanations, progression tables, items, quests, and authored content, prefer official manual → Prima guide → retail data → source/runtime. Existing OpenArcanum documentation, tests, and implementation are evidence only of current status, never authority over the source. `arcanum-ce` observations are `SOURCE-RUNTIME-PHYSICALLY-CERTIFIED`, never `RETAIL-PHYSICALLY-CERTIFIED`.

## Classification

| Classification | Meaning |
|---|---|
| `COMPLETE` | The audited retail rule and its required content are implemented and covered at the audited boundary. |
| `PARTIAL` | A functioning kernel exists, but content, integration, presentation, or edge rules are absent. |
| `MISSING` | No usable implementation of the audited rule exists. |
| `INCORRECT` | Implemented behavior conflicts with source evidence. |
| `SOURCE AMBIGUITY` | Primary evidence disagrees or does not establish one result. |
| `NOT YET AUDITED` | Evidence was not sufficient to classify the area. |

## Audit overview

| Area | Classification | Source truth and current boundary |
|---|---|---|
| Character creation | `PARTIAL` | Race, sex, attributes, portraits, name validation, source-ordered Background selection, twelve pregenerated characters, auto-level execution, and the Human Shopkeeper route exist. The selector exposes every source-legal Background; separate effect execution remains incomplete and unsupported effect families fail closed at finalization. |
| Backgrounds | `PARTIAL` | All 79 source records and numeric order are catalogued. Eligibility now follows source identity masks, but reaction, critical, speed, follower, experience, alignment, expertise, time, and environment effects are incomplete. |
| Statistics and derived values | `PARTIAL` | Primary attributes, maxima, vitality, carry, damage, AC, heal rate, poison recovery, reaction, follower cap, and aptitude kernels exist. Equipment, spell, status, sector, and environment contributions are not complete end-to-end. |
| Skills and training | `PARTIAL` | Ownership, point spending, caps, ranks, training, and UI exist. Several player actions and many Apprentice/Expert/Master effects are absent. |
| Technology and schematics | `PARTIAL` | Eight disciplines, seven degrees, 118-recipe catalog, ingredients, and generic crafting exist. Most crafted-item runtime effects and Technical Manual expertise are missing. |
| Magic | `PARTIAL` | College/rank ownership and a reusable spell kernel exist. Only 4 of the source 80 spells are production-catalogued. |
| Combat | `PARTIAL` | Turn-based/real-time scheduling, core attacks, bows, called shots, criticals, death/corpses, and bounded training effects exist. Weapon families, injuries, conditions, equipment effects, fleeing, and spell/technology integration remain incomplete. |
| Items, inventory, equipment | `PARTIAL` | Containers, transfer, equip, stack, use, barter, corpse, persistence, source data loading, world hover, and ordinary Inventory inspection exist. Condition/durability, broad item effects, identified/unidentified property projection, and remaining runtime special interfaces are incomplete. |
| Dialogue and scripting | `PARTIAL` | Dialogue presentation, selection, state, and a bounded opcode set exist. Full retail opcode/campaign coverage is not established and unsupported operations fail closed. |
| Quests, reputations, blessings/curses | `PARTIAL` | Quest journal/state is usable. Rumors/notes, reputations, blessings/curses, kills/injuries, Background text, and keys are incomplete or missing. |
| Followers and party | `PARTIAL` | Membership, following, combat participation, dialogue bridge, dismissal, and persistence exist. Source HUD portraits/status and selected-follower commands are incomplete. |
| AI | `PARTIAL` | Hostile target selection, weapon selection, approach, and basic combat are present. Spell, technology, item, tactical, retreat, and broader social AI are incomplete. |
| World map, local map, travel | `PARTIAL` | Known areas, destination selection, route construction, and travel execution exist. Local map, travel duration, random encounters, special-route handling, and source presentation are incomplete. |
| Economy | `PARTIAL` | Merchant stock, pricing, money transfer, restock, and barter kernel exist. Gambling-linked commerce, complete Not-for-Sale semantics, service interactions, and source UI remain incomplete. |
| Save/load | `PARTIAL` | Versioned persistence covers the implemented domains. Unimplemented retail domains cannot be persisted, and some transient normalization is intentionally bounded. |
| UI, controls, audio | `PARTIAL` | Main flow and major gameplay panels are usable at 800×600. Local map, most logbook pages, several HUD actions, source skins, startup movies, remapping completeness, and retail audio certification remain open. |

## Character creation

### Playable race and sex combinations

| Race | Male | Female | Primary modifiers | Exceptional maximum |
|---|---:|---:|---|---|
| Human | yes | yes | none | none |
| Dwarf | yes | no | ST +1, CN +1, CH -1, DX -1 | ST 21, CN 21 |
| Elf | yes | yes | WP +1, DX +1, BE +1, CN -2, ST -1 | DX 21, BE 21, WP 21 |
| Half-Elf | yes | yes | DX +1, BE +1, CN -1 | DX 21 |
| Gnome | yes | no | WP +2, CN -2 | WP 22 |
| Halfling | yes | no | DX +2, ST -3 | DX 22 |
| Half-Orc | yes | yes | ST +1, CN +1, BE -2, CH -2 | ST 21, CN 21 |
| Half-Ogre | yes | no | ST +4, BE -1, IN -4 | ST 24 |

Female adds ST -1 and CN +1; male adds no generic modifier. Characters begin with eight in every primary attribute before race/sex/Background effects and receive five unspent character points. A normal primary attribute ranges from 1 to 20, except for the race maxima above.

The creation screen must rebuild the valid Background list when race or sex changes, retain a legal selection where possible, otherwise select the first legal numeric source record, and apply exactly one Background effect. The source enumerates Background IDs numerically; it does not alphabetize them. See [character-background-matrix.md](character-background-matrix.md).

Current OpenArcanum's Background selector is `COMPLETE` at this boundary: it exposes every source-legal race/sex record in numeric order, including records whose effects are not yet executable. Effect support is a separate `PARTIAL` boundary. Finalization fails closed with `UnsupportedBackgroundEffect` rather than silently hiding the record or applying only part of its effect.

Pregenerated characters are separate NPC-conditioned Background records backed by the twelve source prototypes. Selecting a pregen is not equivalent to assembling a custom character with its displayed race and Background. Prototype identity, authored inventory, direct-start behavior, initial/derived projection, and Save V1 continuity are implemented and physically covered. Auto-level execution supports attributes, skills, spell colleges, technology disciplines, and vitality targets; one exact parser mismatch remains for malformed source segments such as scheme 76's `dx 20 pickpocket 20`, which CE treats as `dx 20` while OpenArcanum rejects the four-token segment.

## Statistics and progression

The eight primary attributes are Strength, Constitution, Dexterity, Beauty, Intelligence, Perception, Willpower, and Charisma. Source-derived values audited here are:

- Carry weight: `500 × ST` source weight units.
- Damage bonus: `ST - 10`; negative values use integer division by two; ST 20+ doubles the positive result; clamp -50..50.
- Armor Class adjustment: `DX - 10`, clamp -9..95.
- Speed: base DX, plus 5 at DX 20+, clamp 1..100, before other effects.
- Heal rate: `(CN + 1) / 3`, clamp 0..6.
- Poison recovery: CN, clamp 1..20.
- Beauty reaction adjustment for BE 1..20: `-65,-52,-42,-33,-25,-18,-12,-7,-3,0,3,7,12,18,25,33,42,52,65,75`; above 20 use `2 × (5 × BE - 50)`.
- Maximum followers: `CH / 4`, plus one at Expert Persuasion and another at Master, clamp 1..7.
- Aptitude: `(50 × magic points - 55 × technology points) / 10`, plus source sector/effect terms, clamp -100..100.
- Maximum hit points: `4 × HP points + HP adjustment + WP + 2 × (ST + level) + 4`.
- Maximum fatigue: `4 × fatigue points + fatigue adjustment + 2 × (level + CN) + WP + 4`.
- Constitution adds `max(0, 5 × (CN - 4))` poison resistance; ordinary resistances clamp 0..95.

Core ownership and formulas are implemented. The principal gap is not the arithmetic kernel but the incomplete contributor graph: many Background tokens, equipment properties, spell conditions, poisons, injuries, sector aptitude, time, and environment effects never reach the authoritative value.

## Skills and training

Each skill point is four internal units. The governing-attribute cap, for attribute values 1 through 20, is:

`3,3,3,3,3,7,7,7,11,11,11,15,15,15,19,19,19,20,20,20`.

Training is sequential: None, Apprentice, Expert, Master, with minimum effective ranks 0, 1, 9, and 18 internal units. Ownership, spending, caps, and training state are implemented. Skill use is not complete merely because the rank is stored. See [skill-matrix.md](skill-matrix.md) for all sixteen skills and their source training effects.

## Technology and schematics

Source discipline order is Herbology, Chemistry, Electrical, Explosives, Gun Smithy, Mechanical, Smithy, Therapeutics. Each discipline has seven learned degrees after Layman. Degrees are sequential, cost one character point, add one technology point, and require:

| Degree | Intelligence | Level |
|---|---:|---:|
| Novice | 5 | 10 |
| Assistant | 8 | 20 |
| Associate | 11 | 35 |
| Technician | 13 | 50 |
| Engineer | 15 | 65 |
| Professor | 17 | 80 |
| Doctorate | 19 | 100 |

If Intelligence falls, effective degree walks down while learned ownership remains. Built-in schematic IDs follow `1990 + 200 × discipline + 10 × degree`. Found schematics are separately learned content. Technical Manuals contribute the holder's Intelligence as expertise in their discipline; this is not currently implemented. See [technology-matrix.md](technology-matrix.md).

OpenArcanum has a broad recipe catalog and generic crafting transaction. Runtime behavior for the resulting objects is the limiting boundary: only a small set has product-specific behavior, so successful crafting is not proof of a complete discipline.

## Magic

There are sixteen colleges in source order and five sequential spells in each. Rank prerequisites are:

| Rank | Willpower | Level |
|---|---:|---:|
| 1 | 6 | 1 |
| 2 | 9 | 1 |
| 3 | 12 | 5 |
| 4 | 15 | 10 |
| 5 | 18 | 15 |

Intelligence must be at least 5. Each learned spell costs one character point and adds one magic point. Maintained-spell capacity is `INT / 4`. Fatigue is paid on cast and by upkeep where specified.

The runtime framework supports instantaneous damage/healing, maintained attribute changes, finite blindness, resistance, time/end processing, and persistence. The production catalog includes only Strength of Earth (15), Harm (55), Minor Healing (60), and Flash (66). The other 76 source spells are not production-available. See [magic-matrix.md](magic-matrix.md).

## Combat

The retail rules combine character Speed, weapon speed, action-point costs, range, line of sight, cover, called-shot penalties, armor, damage types, resistances, critical success/failure tables, injuries, fatigue, morale/hostility, training, and real-time or turn-based scheduling.

OpenArcanum owns a coherent bounded combat loop: combat state, target selection, turn ordering, attacks, bows and ammunition, called locations, cover, critical resolution, death, corpses, and real-time scheduling. Remaining gaps are integration-heavy: full firearms/thrown parity, injury/condition consequences, equipment and ammunition properties, broad spell/technology combat, tactical AI, fleeing/combat exit, and the complete critical-effect corpus.

| Combat rule | Source expectation | OpenArcanum |
|---|---|---|
| Real-time / turn-based | Both modes share authoritative actors, attacks, damage, and hostility; scheduling differs. | `PARTIAL`: both schedulers exist; full source parity is not certified. |
| Ready/unready | Weapon readiness and animations gate attacks and transitions. | `PARTIAL`: bounded combat presentation exists; complete weapon-state semantics are not audited. |
| AP / Speed / initiative | Speed drives action capacity/order; actions consume source costs modified by weapon, training, and state. | `PARTIAL`: bounded AP/order exists; every modifier and transition is not complete. |
| Attack families | Unarmed, melee, bow, firearm, and thrown attacks use their source skill, range, ammo, and properties. | `PARTIAL`: melee/bow strongest; firearms and thrown parity remain open. |
| Criticals | Source success/failure tables produce damage, drops, injuries, knockdown, breakage, and other typed results. | `PARTIAL`: core resolution exists; corpus incomplete. |
| Damage/armor/resistance | Damage types resolve through armor, resistance, conditions, effects, and item properties. | `PARTIAL`: core arithmetic exists; full prototype/effect integration does not. |
| Called targets | Location selection changes chance and can produce location-specific consequences. | `PARTIAL`: selection/penalty exists; injury consequences incomplete. |
| Death/unconsciousness | Hit points/fatigue, knockout, death, corpse, loot, scripts, followers, and resurrection remain coherent. | `PARTIAL`: death/corpse exists; unconsciousness, injuries, scripts, followers, and resurrection are incomplete. |
| Hostility/aggro/fleeing/exit | Social/script state starts combat; actors may retreat/flee; source conditions end it. | `PARTIAL`: basic hostile loop exists; complete transitions do not. |

## Items, inventory, equipment, and commerce

The source distinguishes prototypes, instances, inventory/container ownership, wearable slots, stackable quantities, charges/ammunition, condition, worth, flags, scripts, magic/technology properties, identified state, and use/equip/drop/transfer behavior.

OpenArcanum implements the main ownership and transfer transactions, equipment slots, stack handling, item use boundaries, merchant/corpse containers, barter, persistence, world-target hover, and ordinary Inventory inspection. It does not yet establish full source behavior for condition/durability, every item property/effect, identified/unidentified property disclosure, ammunition families, key/keyring behavior, in-game service special cases, gambling commerce, or all remaining source interfaces.

| Item rule | Source expectation | OpenArcanum |
|---|---|---|
| Equipment slots | Helmet, rings, medallion, weapon, shield, armor, gauntlets, and boots enforce object/body rules. | `PARTIAL/COMPLETE` at bounded slot ownership; full body/property restrictions need corpus proof. |
| Footprints | Source inventory dimensions and location form a two-dimensional packing grid. | `COMPLETE` at the audited inventory projection boundary. |
| Stacks/resources | Stack-compatible objects merge/split; charges and ammunition retain source type/count. | `PARTIAL`: generic stacks exist; every resource family is not certified. |
| Encumbrance | Carried weight is compared with Strength-derived capacity and affects movement/state by source thresholds. | `PARTIAL`: carry formula/capacity exist; all penalties and UI feedback are not established. |
| Weapon/armor properties | Prototype damage, speed, range, ammo, armor, resistances, requirements, flags, magic/tech effects, and aptitude apply. | `PARTIAL`: base paths exist; complete property corpus does not. |
| Condition | Use/combat can damage/break items; Repair restores with training-dependent maximum loss. | `MISSING`. |
| Inspection | Identified state controls which source fields/help text are visible. | `PARTIAL`. |
| Containers/corpses | Ownership, locks, scripts, capacity, transfer, and source mode determine access. | `PARTIAL`: transfer works; special modes/scripts are incomplete. |
| Merchants | Inventory, reaction, Haggle/Gambling, markup, buy/sell restrictions, money, restock, and services apply. | `PARTIAL`: stock/pricing/restock work; training, services, restrictions, and source UI remain incomplete. |

## Dialogue, scripts, quests, and world state

Dialogue text, choices, conditions, effects, scripts, quest/reputation state, reactions, party state, and object/world mutations form one source system. OpenArcanum has a usable dialogue runner and a bounded set of script operations. Unsupported operations fail closed, which prevents false success but also prevents full campaign execution.

Quest state and the Quests logbook page are implemented. The remainder of the logbook is not equivalent to that milestone: Rumors/Notes, Reputations, Blessings/Curses, Kills/Injuries, Background, and Keys each require separate data, ordering, persistence, and presentation.

Full retail campaign certification requires an opcode-and-script corpus audit, representative quest chains, conversation side effects, timers, object mutation, followers, reputations, travel, combat handoff, save/load, and failure paths. That certification has not been reached.

| Dialogue/script rule | Source expectation | OpenArcanum |
|---|---|---|
| Responses | Source conditions determine visible responses; selection applies ordered effects and jumps. | `PARTIAL`: bounded parser/session works; full vocabulary/corpus does not. |
| Skill/reaction checks | Dialogue may test skills, training, attributes, reaction, alignment, race, sex, quests, and state. | `PARTIAL`. |
| Mutation | Effects can change quests, flags, variables, reaction, inventory, money, followers, teleportation, combat, and objects. | `PARTIAL`; unsupported operations fail closed. |
| Barter | Authored dialogue can enter/exit merchant interaction without losing conversation/campaign state. | `PARTIAL`: bounded bridge exists. |
| Voice/forced dialogue | Source voice assets and script/world triggers can force a conversation. | `PARTIAL`: presentation supports voice boundaries; corpus and forced triggers are incomplete. |
| Opening Virgil trigger | First-map entry must execute exactly once and survive save/load. | `COMPLETE` at the audited opening compatibility slice. |

| Logbook domain | Required authority | OpenArcanum |
|---|---|---|
| Quests | state progression, text, timestamps, sorting, updates, persistence | `COMPLETE` at the current quest-journal boundary; campaign corpus remains partial |
| Rumors / Notes | authored prose, source order, timestamp, quelled state, persistence | `PARTIAL` |
| Reputations | acquisition/removal, reaction/gameplay effects, text, persistence | `PARTIAL` |
| Blessings / Curses | typed effect, source text, removal, persistence | `MISSING` |
| Kills / Injuries | combat statistics and injury records | `MISSING` |
| Background | full authored text and pagination | `PARTIAL` |
| Keys | key ownership/keyring projection and lock integration | `MISSING` |

## Followers, party, and AI

Party membership, following, combat participation, dialogue bridge, dismissal, and save/load are present. Missing source-facing pieces include portrait identity, poison/status projection, selected-follower wait/inventory/command interactions, and complete join/leave/death/resurrection dialogue semantics.

AI currently covers the bounded hostile loop: choose a target, choose a usable weapon, approach, and attack. Retail parity additionally needs spell/technology/item selection, ammunition and weapon switching, distance and cover tactics, healing, retreat/fleeing, surrender, social/campaign schedules, and follower command response.

| Party/social rule | Source expectation | OpenArcanum |
|---|---|---|
| Reaction | Beauty, race, alignment, reputation, actions, dialogue, Background, and temporary effects contribute. | `PARTIAL`: core social value exists; full contributor/event graph does not. |
| Follower cap | CH/4 plus Persuasion Expert/Master, bounded 1..7. | `PARTIAL/COMPLETE` formula; all recruitment exceptions not certified. |
| Recruitment | Dialogue, reaction, alignment, level, quests, party space, and NPC rules gate joining. | `PARTIAL`. |
| Commands | global hotkeys and selected-follower wait/inventory/behavior commands affect AI. | `PARTIAL`: core party state exists; command UI/AI modes incomplete. |
| Dismissal/death/resurrection | authored dialogue and lifecycle state preserve the correct world actor and inventory. | `PARTIAL`. |
| Party HUD | portrait, health, fatigue, poison/status, selection, and command feedback are source-projected. | `PARTIAL`. |

## Maps, travel, time, and encounters

OpenArcanum can discover known areas, select world destinations, build routes, and execute bounded travel. It lacks a source local map, complete world-map presentation, source travel duration, random encounters, and some special-route rules. Time-of-day and environment are gameplay inputs to visibility, schedules, Backgrounds, and spells, not only display concerns.

Waiting is not sleeping. Wilderness/town waiting exists, while occupied/rented/free bed authority and source `SAP_USE` sleep semantics remain missing.

| Map/time rule | Source expectation | OpenArcanum |
|---|---|---|
| Town/local map | discovered geometry, markers, notes, centering, and interaction use the local-map screen. | `MISSING`. |
| World map | known locations, marker states, route legality, terrain, notes, and source presentation. | `PARTIAL`: destinations/routes work. |
| Encounters | travel checks source encounter tables and transfers into encounter maps. | `MISSING`. |
| Travel restrictions | map/quest/script/special-route rules may reject or alter a route. | `PARTIAL`. |
| Travel time | distance/terrain advances calendar by source rules. | `MISSING`. |
| Wait | source choices include bounded durations and until morning/evening/healed where legal. | `PARTIAL`: general wait exists; every choice/restriction is not complete. |
| Sleep | wilderness/town/bed access, rental/ownership, healing, time, and maintained effects resolve together. | `PARTIAL/MISSING`: bed authority is absent. |
| Maintained effects | time advance charges upkeep, expires finite effects, and cancels illegal effects. | `PARTIAL`: magic kernel supports this for its four catalogued spells. |

## UI, controls, and audio

The 800×600 player flow covers main menu, character creation, inventory/equipment, character/skills, dialogue, combat, world map, merchants, and save/load at useful bounded depth. Major source UI gaps are local map, most logbook pages, selected-follower HUD, several skill/Fate actions, technology/magic source interfaces, special container/merchant skins, options/remapping completeness, and source-faithful save/load/options presentation.

Startup movies are not integrated. Runtime audio has a functional OpenArcanum implementation, but physical comparison against the retail executable remains blocked by the retail host/audio condition and therefore is not retail-certified.

| Presentation/control rule | Source expectation | OpenArcanum |
|---|---|---|
| Music/ambient/positional | source sound tables, schemes, distance, maps, transitions, and persistence control playback. | `COMPLETE/PARTIAL` at bounded M12D runtime; retail physical certification open. |
| Voice/UI/combat sounds | dialogue and actions dispatch the source asset at source timing/position. | `PARTIAL`: service exists; complete event corpus not certified. |
| Startup media | ordered movies and audio precede/enter the menu. | `MISSING`. |
| Keyboard | manual quick keys and configurable commands route through the correct context. | `PARTIAL`: not every command/remap is certified. |
| Mouse/cursors | move, attack, use, talk, loot, inspect, drag/drop, scroll, and invalid states select source cursors/behavior. | `PARTIAL`. |
| HUD buttons | every permanent button performs its source action and provides contextual help. | `PARTIAL`: several skill/Fate/party actions are absent. |
| Permanent screens | inventory, character, skills, magic, technology, logbook, maps, options, save/load, and help retain source routes/state. | `PARTIAL`: local map and most logbook domains are the largest gaps. |
| Contextual help | hover/right-click help uses source terminology and follows the focused control/item/stat. | `PARTIAL`: major reconstructed screens support it; complete corpus/routes do not. |

## Recorded source disagreements and ambiguities

| Topic | Evidence | Required treatment |
|---|---|---|
| Healing Salve prototype | Built-in schematic data resolves to prototype 10059; the currently supported usable salve path references 10079. | `SOURCE AMBIGUITY`; do not alias silently. Verify prototype semantics and retail behavior. |
| Mechanical Trap Springer naming | Retail schematic/component naming and current aliases do not form one clean canonical name. | Preserve source IDs and show aliases until runtime behavior resolves the identity. |
| Female-only race codes in Background masks | Some records list Dwarf, Gnome, Halfling, or Half-Ogre female codes although those are not playable choices. | Eligibility is mask intersection with playable race/sex combinations; do not invent those choices. |
| Background 29 effect text | Retail effect text lacks a separator between `cn +2` and `dx +1`. | Treat as unresolved parsing/data defect until source execution proves both contributions. |
| Background effect 342 | Several records have empty generic effect text but source applies time/environment behavior elsewhere. | Do not call these no-effect Backgrounds; implement and test the external rule. |
| Corpse `LOOT` interaction | Existing evidence establishes corpse inventory access, not every original verb/presentation detail. | Keep interaction wording/presentation bounded pending retail proof. |
| Retail audio comparison | The available retail-derived host was run under an audio-blocking condition. | OpenArcanum audio may be functionally complete at its boundary, but physical retail certification remains open. |

## Related matrices

- [OPENARCANUM_GAP_MATRIX.md](OPENARCANUM_GAP_MATRIX.md) — ranked confirmed gaps and likely implementation surfaces.
- [character-background-matrix.md](character-background-matrix.md) — creation rules and every source Background.
- [skill-matrix.md](skill-matrix.md) — every skill, cap, training effect, and runtime boundary.
- [technology-matrix.md](technology-matrix.md) — every learned degree schematic and discipline rules.
- [magic-matrix.md](magic-matrix.md) — all 80 spells and production status.

## Audit boundary

This audit read the official manual, Prima guide, `arcanum-ce` source, mounted retail DAT content, current OpenArcanum source, tests, and milestone/UI audits. It did not modify retail data or production project files, run the full test suite, or claim physical retail certification where the executable could not provide it. `NOT YET AUDITED` remains preferable to an inference presented as fact.
