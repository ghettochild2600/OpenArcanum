# M4A — Authoritative character attributes

## Source audit (recorded before production changes)

The audit used the `arcanum-ce` reconstruction (`stat.h`, `stat.c`, `effect.h`, `effect.c`, `obj.h`, and `obj.c`) together with the retail `rules/effect.mes`, `.pro`, `.sec`, and `.mob` data read through OpenArcanum's existing parsers. Original game data remained read-only.

### Field and value mapping

PCs and NPCs are both critters. Their stored stat array is `OBJ_F_CRITTER_STAT_BASE_IDX`, object-field ordinal **220**, serialized as a sparse `INT32_ARRAY` with 28 source slots. The eight primary attributes are the first eight slots:

| Source slot | Source name | Typed M4A name | Default | Ordinary minimum | Ordinary maximum |
|---:|---|---|---:|---:|---:|
| 0 | `STAT_STRENGTH` | `Strength` | 8 | 1 | 20 |
| 1 | `STAT_DEXTERITY` | `Dexterity` | 8 | 1 | 20 |
| 2 | `STAT_CONSTITUTION` | `Constitution` | 8 | 1 | 20 |
| 3 | `STAT_BEAUTY` | `Beauty` | 8 | 1 | 20 |
| 4 | `STAT_INTELLIGENCE` | `Intelligence` | 8 | 1 | 20 |
| 5 | `STAT_PERCEPTION` | `Perception` | 8 | 1 | 20 |
| 6 | `STAT_WILLPOWER` | `Willpower` | 8 | 1 | 20 |
| 7 | `STAT_CHARISMA` | `Charisma` | 8 | 1 | 20 |

`STAT_GENDER` is slot 26 (`Female = 0`, `Male = 1`) and `STAT_RACE` is slot 27 (`Human = 0`, `Dwarf = 1`, `Elf = 2`, `HalfElf = 3`, `Gnome = 4`, `Halfling = 5`, `HalfOrc = 6`, `HalfOgre = 7`, `DarkElf = 8`, `Ogre = 9`, `Orc = 10`). These are character identity inputs, not inferred from ART.

`stat_set_defaults` writes 8 into all eight primary slots. The prototype owns the full array by default. An instance inherits the prototype field until the first array write; `obj_arrayfield_store`/`sub_40D2A0` then copies the **whole** prototype array before changing one element. Consequently, the source-compatible read hierarchy is `instance.StatBase ?? prototype.StatBase`, never a per-slot merge. A present instance array is the complete override snapshot.

`stat_base_get` returns the stored primary slot. Current/effective primary attributes are not stored separately: `stat_level_get` starts from that base, applies special environment/background logic, poison penalties, the effect pipeline, and finally clamps to the source minimum/maximum.

### Race bounds and innate effects

The ordinary maximum is 20 except for these race-aware maxima from `stat_level_max`:

- Dwarf and Half-Orc: Strength and Constitution 21.
- Elf and Dark Elf: Dexterity, Beauty, and Willpower 21.
- Half-Elf: Dexterity 21.
- Gnome: Willpower 22.
- Halfling: Dexterity 22.
- Half-Ogre: Strength 24.
- Ogre: Strength 26.
- Orc: Strength and Constitution 22.

`stat_base_set(STAT_RACE)` removes the old Race-caused effect and adds effect `64 + race`; `stat_base_set(STAT_GENDER)` removes the old Gender-caused effect and adds effect 330 only for Female. Retail `rules/effect.mes` defines these primary-attribute additions:

| Identity | Effect | Primary attribute changes |
|---|---:|---|
| Human | 64 | none |
| Dwarf | 65 | Strength +1, Constitution +1, Charisma -1, Dexterity -1 |
| Elf | 66 | Willpower +1, Dexterity +1, Beauty +1, Constitution -2, Strength -1 |
| Half-Elf | 67 | Dexterity +1, Beauty +1, Constitution -1 |
| Gnome | 68 | Willpower +2, Constitution -2 |
| Halfling | 69 | Dexterity +2, Strength -3 |
| Half-Orc | 70 | Strength +1, Constitution +1, Beauty -2, Charisma -2 |
| Half-Ogre | 71 | Strength +4, Beauty -1, Intelligence -4 |
| Dark Elf | 72 | Dexterity +1, Beauty +1 |
| Ogre | 73 | Strength +6, Beauty -6, Intelligence -6 |
| Orc | 74 | Strength +2, Constitution +2, Beauty -4, Charisma -4, Intelligence -1 |
| Female | 330 | Strength -1, Constitution +1 |
| Male | — | none |

The retail rows also contain skill, resistance, age, aptitude-point, critical-hit, and reaction changes. Those are outside M4A and are not admitted into the M4A primary-attribute calculator.

### Other modifier domains and ordering

Background is stored separately in `OBJ_F_PC_BACKGROUND`. Ordinary background changes are effects with cause Background. Three backgrounds also have location-dependent primary changes directly in `stat_level_get`: Agoraphobic (indoor Intelligence +2; outdoor Strength +2 and Dexterity/Intelligence/Willpower -2), Hydrophobic on water (the same +2/-2 pattern), and Afraid of the Dark in darkness (the same +2/-2 pattern). Poison subtracts `min(poisonLevel / 100, 3)` from Strength and Dexterity.

Race, Background, Class, and Gender are innate effect causes. Bless, Curse, Item, Spell, Injury, and Tech are non-innate effect causes. Equipment therefore affects an attribute through Item-caused effects, not by rewriting its base slot. The generic effect order is percentage, multiply, divide, additive sum, minimum floor, maximum ceiling; `stat_level_get` then performs the final source-range clamp.

M4A admits only the fully audited additive **Race** and **Gender** primary-attribute effects. Background, poison, equipment, spell, injury, technology, bless/curse, and class inputs remain explicit deferred domains. Because every admitted modifier is additive, their result is summed before the source final clamp without inventing behavior for the other operators.

### Direct derived-stat consumers (audit only)

- Effective Strength: carry weight `500 * Strength` (source weight units); damage bonus `Strength - 10`, negative values divided by two, doubled at extraordinary Strength (effective Strength 20+).
- Effective Dexterity: AC adjustment `Dexterity - 10`; speed equals Dexterity, +5 at extraordinary Dexterity.
- Effective Constitution: heal rate `(Constitution + 1) / 3`; poison recovery equals Constitution. Extraordinary Constitution also zeroes poison on a poison-level write.
- Effective Beauty: reaction-modifier lookup for ordinary values; Beauty 20+ uses `2 * (5 * Beauty - 50)`.
- Effective Charisma: maximum followers `Charisma / 4` before persuasion-training adjustment.
- Maximum hit points directly include effective Strength and Willpower: `4 * hit-point-points + hit-point-adjustment + Willpower + 2 * (Strength + level) + 4`, before maximum-hit-point effects.
- Maximum fatigue directly includes effective Constitution and Willpower: `4 * fatigue-points + fatigue-adjustment + 2 * (level + Constitution) + Willpower + 4`, before maximum-fatigue effects.
- Poison resistance receives `5 * (Constitution - 4)` when effective Constitution is above 4, in addition to the rest of the resistance pipeline.
- Intelligence and Willpower are requirements for spell/technology acquisition; all eight feed their associated skills elsewhere in the source. Perception also feeds range/detection systems.

M4A does not implement any of these consumers. It exposes typed effective attributes so later milestones do not decode prototypes or presentation objects.

### Real NPC fixture

The chosen fixture is source-authored, persistent, present in an already validated crash-site sector, and has a complete instance override:

- Sector: `maps/arcanum1-024-fixed/101602821844.sec`
- Mobile source: `maps/arcanum1-024-fixed/g_065ece33_acf4_4b3a_b98e_9ab36c467e6f.mob`
- ObjectID: `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`
- Prototype: 17101
- Race: Human (0)
- Gender: Female (0)
- Prototype primary base: `[8, 8, 8, 8, 8, 8, 8, 8]`
- Instance primary base in source order (Strength, Dexterity, Constitution, Beauty, Intelligence, Perception, Willpower, Charisma): `[10, 9, 15, 10, 10, 10, 8, 10]`
- Expected effective primary values after the source Female effect: `[9, 9, 16, 10, 10, 10, 8, 10]`

This single fixture proves both whole-array instance precedence and a source-defined base/effective distinction.

### Development PC baseline

Until character creation exists, the deterministic production PC is explicitly **Human, Male**, with all eight stored base attributes at the source default of 8. Its expected M4A effective attributes are also all 8. This state is independent of its ART identity; the existing ART happens to present a human male but is not consulted when initializing attributes.

## Smallest M4A change set (proposed before production changes)

1. Add source-numbered attribute/race/gender types, an immutable eight-value base set, persistent character state, and a character-stat service under `Runtime/Character`.
2. Make the existing map-session coordinator own one character-stat service keyed by stable `ArcanumObjectId`.
3. Initialize the production PC from the explicit baseline in `GetOrCreatePlayer`.
4. During real NPC registration, pass the parsed instance and prototype arrays to the character service; keep `instance ?? prototype` selection inside that domain boundary and fail explicitly when a critter source is invalid.
5. Calculate effective values from immutable base plus the audited Race and Gender additions, followed by the race-aware source clamp. Permit only narrow race/gender mutation so source-style removal/reapplication can be tested; expose no general stat, equipment, spell, or temporary-effect mutation API.
6. Add focused EditMode coverage and a Play Mode validation command using the exact fixture above. Do not add attribute authority to `WorldObject`, sprite owners, or other Unity presentation components.

## Implementation and validation

### Ownership and call graph

```text
retail .pro + .sec/.mob
  -> ObjectProtoReader / ObjectInstanceReader
  -> WorldObjectSectorLoader (selects persistent critters)
  -> WorldMapSessionCoordinator.Characters
  -> CharacterStatService (stable ObjectID registry + typed query boundary)
  -> PersistentCharacterState (immutable base set; race/gender identity inputs)
  -> gameplay consumers call GetBaseAttribute / GetEffectiveAttribute

WorldObject / Transform / SpriteOwner
  <- disposable presentation only; never read back into attribute authority
```

`CharacterAttribute` preserves source IDs 0..7. `CharacterAttributeSet` owns exactly eight validated stored values and exposes defensive copies. `CharacterRace` includes all eleven source races rather than conflating race with the five critter body ART types. `CharacterGender` preserves source values. `CharacterStatService` is a plain character-domain service owned once by `WorldMapSessionCoordinator`; its dictionary is keyed by `ArcanumObjectId` and is never cleared by presentation or sector teardown.

Real NPC initialization takes both parser outputs and chooses the complete instance array when present, otherwise the prototype array. Missing arrays, short arrays, non-PC/NPC object types, nonpersistent identities, invalid race/gender/attribute IDs, invalid base ranges, changed-source reuse, and missing state queries fail explicitly. The production PC is registered through the same service, but from its documented development baseline rather than a prototype or its presentation ART.

M4A exposes narrow `SetRace` and `SetGender` boundaries because the source replaces their caused effects when those identity inputs change. It exposes no arbitrary base-stat setter and no general effect insertion API. Effective queries add the audited retail Race and Gender primary changes and then apply the source race-aware final clamp. Base values remain unchanged when either identity input changes.

Effective Strength is therefore authoritative and directly available through `Characters.GetEffectiveAttribute(objectId, CharacterAttribute.Strength)`. M4A deliberately does not multiply it into carry weight or compute damage bonus.

### Automated validation

Unity 6000.0.71f1 compiled with 0 compiler errors. Results, all with 0 failed, 0 skipped, and 0 inconclusive:

| Suite | Passed |
|---|---:|
| M4A focused | 13 |
| M3D stack transactions | 18 |
| M3C equipment state | 13 |
| M3B inventory commands | 13 |
| M3A inventory state | 8 |
| M2B SAP_USE | 8 |
| M2A interaction | 16 |
| PlayerNavigation | 21 |
| M1A lifecycle | 7 |
| M1B traversal | 11 |
| WorldSessionState | 27 |
| PortalArtResolver | 2 |
| Complete EditMode | 328 |

Focused tests cover all eight source IDs, the real-fixture constants, instance-over-prototype selection, prototype fallback, the explicit PC baseline, invalid inputs and missing state, race-specific base limits, source Race/Gender changes and removal, base/effective separation, final clamp behavior, collision detection, stable lookup, presentation destruction, reload, and PC A-to-B-to-A reuse.

### Play Mode validation

Computer Use drove the existing TestTerrain scene and the M4A validation coroutine. It loaded the exact NPC fixture, resolved its stable ObjectID and prototype, verified all eight source base/effective values, temporarily changed the PC to Female and back to Male, rebuilt in Original and Enhanced modes and restored the initial mode, reloaded the NPC sector, traversed PC A-to-B-to-A, and verified the same PC/NPC character records throughout. Final checks found one coordinator, one sector owner, one navigation controller, one world-object root, one PC presentation, one PC sprite owner, one fixture NPC presentation, one presentation per persistent identity, no orphan sprite owners, and navigation still bound only to the production PC.

The final retail-sector session contained 14 character records (13 source NPCs across the visited sectors plus the production PC), with no duplicate keyed state. The validator reported **0 new warnings and 0 errors**.

### Deferred work

Background/environment, poison, equipment/item, spell, injury, technology, class, bless/curse, percentage/multiply/divide/min/max effects, derived statistics, leveling/XP, skills, combat, character creation, dialogue checks, AI, UI, and save serialization remain unimplemented. Their source semantics are documented above so future systems can enter through the character-stat boundary without decoding object fields themselves.

The exact recommended next milestone is **M3E — source-faithful item weight, effective-Strength carry capacity, container capacity, and atomic transfer guards**. It should consume M4A effective Strength and must not add encumbrance movement penalties, equipment bonuses, character progression, UI, or save serialization unless separately scoped.
