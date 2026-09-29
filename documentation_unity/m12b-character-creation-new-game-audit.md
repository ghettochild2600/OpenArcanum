# M12B Character Creation / New Game Audit

Date: 2026-09-28

Branch: `feature/session-save-load`

Starting HEAD: `8bed4f08f599ed6f7e29d2e3ca068f138845ed27`

## Outcome

M12B is complete as the bounded authoritative character-creation and New Game runtime. A UI-independent creation
specification validates retail race, gender, portrait, background, attributes, skills, spell colleges, technology
disciplines, and Character Point spending. One finalize transaction resets stale session authority, constructs the
production PC through the existing M3/M4/M10 services, and enters the retail START_MAP. Creation metadata is an
optional Save V1 domain; unfinished editor state is never persisted. The polished creation presentation remains
M12C and was not started.

## Source archaeology

The behavioral reference was the Arcanum CE translation of retail character creation and New Game code, especially
`charedit`, `mainmenu`, `background`, `stat`, `skill`, `spell`, `tech`, and `player`. The implementation also reads the
mounted retail `rules/backgrnd.mes`, `mes/gameback.mes`, `rules/effect.mes`, `portrait/gameport.mes`, and MapList data.
The original `Steam-Clean` tree was read only.

The audited retail rules used by M12B are:

- eight playable races: Human, Dwarf, Elf, Half-Elf, Gnome, Halfling, Half-Orc, and Half-Ogre;
- male bodies for all eight races, while female bodies exist only for Human, Elf, Half-Elf, and Half-Orc;
- all eight raw attributes begin at 8 and continue to use M4's race/gender adjustments, minimum, and racial maxima;
- level 1, 0 XP, five unspent Character Points, age 20, alignment 0, and no initial damage;
- one Character Point per raw attribute increase, purchased skill point, sequential spell, or technology degree;
- four skill units per purchased point, bounded by the M4C governing-attribute cap;
- spell ranks are sequential within a college, require Intelligence 5, Willpower 6/9/12/15/18, and minimum levels
  1/1/5/10/15;
- technology degrees require Intelligence 0/5/8/11/13/15/17/19;
- names contain 1 through 23 characters and may not remain the source placeholder;
- the generic PC prototype is 16066 and the ordinary initial clothing ART bit is part of the authoritative body ART.

Retail race body indices are Human 0, Dwarf 1, Elf 4, Half-Elf 0, Gnome 2, Halfling 2, Half-Orc 0, and Half-Ogre 3.
Portrait rows are parsed by their HU/DW/EL/HA/GN/HE/HO/HG plus M/F identity token, so legality comes from source data
rather than a parallel hand-maintained portrait table. Graphics mode is presentation only and never enters character
identity.

## Authoritative model and validation

`CharacterCreationSpecification` owns only editable choices. `CharacterCreationValidationResult` reports a typed
failure plus exact spent/remaining points. `FinalizedCharacterSpecification` is the immutable normalized result.
`CharacterCreationStateService`, owned by `WorldMapSessionCoordinator`, is the sole finalization boundary. None of
these types depends on an M12C widget or temporary GameObject.

Validation rejects an unknown or unavailable source, illegal race/gender or portrait, wrong age, unknown/restricted
background, unsupported background effect, attributes outside M4 bounds, invalid skill/spell/technology prerequisites,
and Character Point overspend before runtime mutation. An already-finalized session cannot finalize twice.

The complete retail background catalog is projected generically from five-row `backgrnd.mes` groups: text ID, effect
ID, race/gender restriction, starting Gold, and starting item prototypes. Supported effect terms apply attribute,
skill, resistance, magic-point, and technology-point modifiers. Unknown effect syntax fails closed rather than being
silently approximated. Permanent attribute and skill effects use stable keyed M4 modifiers, so restore and repeated
projection cannot duplicate them.

Authentic examples used during validation include:

- background 0, No Significant Background: unrestricted, no effect, 400 Gold;
- background 1, Snake Handler: +20 Poison Resistance and -1 Beauty, 400 Gold;
- background 3, Raised by Elves: Human female only, -1 to technological skills, 400 Gold, and starting item 8157.

Retail background 2 has a malformed `{22}{400}` condition/gold row in the shipped data and resolves no valid
race/gender combination. M12B preserves that source result instead of inventing a repair.

## Existing authority integration

Finalization reuses, rather than duplicates, established systems:

- M3 creates Gold and background items with normal dynamic identities, containment, capacity, and natural equipment;
- M4 owns race/gender attribute effects, progression, level/XP/remaining points, vitality, derived resistances,
  alignment, and aptitude;
- M10A receives the selected sequential college ranks and exposes normal learned spells;
- M10B receives selected discipline degrees, including its existing schematic-unlock behavior;
- M6 captures and restores all committed state;
- M7 owns sector selection and production presentation;
- downstream combat, party, economy, social, and crafting services observe the same production PC authority.

The finalize preflight resolves every required starting prototype. Commit first clears the prior authoritative session,
then creates the default persistent production-player identity, applies attributes and keyed background effects, adds
Gold/items, equips an unoccupied natural worn location, and selects the authentic start sector. Any unexpected commit
failure resets the session again, leaving no half-created PC.

The complete EditMode suite initially found two M8A failures in direct coordinator traversal. The new reuse path in
`GetOrCreatePlayer` returned before updating the existing PC's sector and combat actor source. The fix preserves the
same authority while calling the established sector relocation and combat-source registration operations. Focused,
M8A, targeted, and complete-suite reruns prove the correction.

## Campaign start

MapList contains one START_MAP: map id 1, `Arcanum1-024-fixed`, at global `(92958,82592)`. Source sector conversion
selects `maps/arcanum1-024-fixed/86570436012.sec` and local tile `(30,32)`. New Game clears stale maps, objects,
campaign state, combat, active effects, party, economy, social state, crafting knowledge, and prior creation metadata
before constructing the new PC. The final sector is selected through the production world pipeline, not TestTerrain.

The retail intro movies (IDs 1 and 7), shopping-map presentation (map 14), final creation screens, input flow, and
audio are presentation work deferred beyond M12B. No substitute quest, flag, party, time, or cutscene behavior was
invented. Pregenerated retail characters are data that populate the same creation specification/finalize pipeline;
they do not require a second runtime authority. Importing and presenting the complete pregen catalog remains content/
M12C work.

## Save V1

Save format remains V1. The optional `characterCreation` payload stores stable production identity, name, background
ID and source text ID, portrait ID, age, and spent Character Points. Race/gender, attributes, progression, derived
state, vitality, spells, technology, items, Gold, map placement, and other world state continue to round-trip through
their existing domains. Restore verifies background source identity and portrait legality, then reapplies keyed
background effects without duplication. Older V1 saves without creation metadata remain valid. Editable creation
choices and incomplete transactions do not persist.

## Validation

- Unity 6000.0.71f1 compilation: clean.
- Focused `M12BCharacterCreationNewGame`: **13/13**, failed/skipped/inconclusive **0/0/0**.
- Directly affected regressions:
  - M3A inventory **8/8** and M3C equipment **13/13**;
  - M4A attributes **13/13**, M4B vitality **20/20**, M4C progression **29/29**, M4D derived stats **25/25**;
  - M6A save/load **25/25** and M7A local transition **24/24**;
  - M8A core combat **22/22**, added after the complete suite exposed direct-relocation coupling;
  - M10A magic **31/31** and M10B technology **12/12**;
  - combined **222/222**, failed/skipped/inconclusive **0/0/0**.
- Complete EditMode: **1014/1014**, failed/skipped/inconclusive **0/0/0**.
- The full suite emitted ten intentional fail-closed dialogue compatibility warnings from existing tests. They were
  inspected and cleared. Final Console: **0 logs, 0 warnings, 0 errors**.
- `git diff --check`: clean.

Computer Use physical Play Mode validation used TestTerrain only as the harness host, while exercising the production
`WorldMapSessionCoordinator`, mounted retail data, actual PC presentation, and authentic campaign sector. It proved a
Human male with portrait 1005 and Raised by Elves background 3; exact four spent/one unspent Character Points; Earth
rank 1; Herbology Novice; item 8157 naturally equipped; 400 Gold; map 1/sector `86570436012.sec`/local `(30,32)`;
immediate Save V1 round-trip; and authoritative identity/state across Original -> Enhanced -> Original. The same
pipeline then finalized an authentic male Dwarf with portrait 1001, body index 1, effective Strength 9, background-0
Gold, and the same campaign entry. Physical validation ended with **0 warnings and 0 errors** and restored the user's
initial graphics mode.

## Closure assessment

The core character-creation/New Game runtime is complete: mounted source data can produce a validated authentic
character, commit it exactly once into a clean authoritative session, enter the retail campaign start, and save/load
immediately. Additional background-by-background viewing, portrait browsing, complete pregen presentation, polished
creation screens, shopping flow, intro movies, and audio are presentation/content work and do not leave a second
runtime authority unfinished. M12C was not started.
