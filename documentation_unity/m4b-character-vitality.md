# M4B — Authoritative character vitality

## Pre-implementation source audit

This audit was recorded before M4B production changes. Evidence comes from the `arcanum-ce` reconstruction
(`obj.h`, `obj.c`, `object.c`, `critter.c`, `stat.c`, `effect.c`, `level.c`, and `script.c`) and retail `.pro`, `.sec`,
and `.mob` data read through OpenArcanum's existing parsers. Original Steam data remains read-only.

### Stored source fields and inheritance

- Common Int32 fields are `OBJ_F_HP_PTS` 27, `OBJ_F_HP_ADJ` 28, and `OBJ_F_HP_DAMAGE` 29. Critter Int32 fields are
  `OBJ_F_CRITTER_FATIGUE_PTS` 224, `OBJ_F_CRITTER_FATIGUE_ADJ` 225, and
  `OBJ_F_CRITTER_FATIGUE_DAMAGE` 226.
- The six fields inherit independently through the ordinary object field engine: an instance override wins; otherwise
  the prototype value is used. This differs from `OBJ_F_CRITTER_STAT_BASE_IDX`, whose instance override replaces the
  whole array.
- Newly defaulted PC/NPC objects receive the standard stat array (primary attributes 8 and Level 1). Their HP points,
  HP adjustment, HP damage, fatigue points, fatigue adjustment, and fatigue damage remain zero unless prototype or
  instance data supplies a value. Character creation explicitly clears HP and fatigue damage.
- HP/Fatigue "points" are purchased maximum-value increments, not current values. Their setters clamp only negative
  inputs to zero. Adjustment fields participate directly in the maximum formulas. M4B retains the resolved source
  values but does not implement character-point allocation or effects that change them.

### Maximum Hit Points

For PC/NPC critters, `object_hp_max` is:

```text
4 * HP_PTS + HP_ADJ + effective Willpower + 2 * (effective Strength + effective Level) + 4
```

The source calls `stat_level_get` for Strength, Willpower, and Level. Level defaults to 1 and is clamped by the stat
system to 0..51. M4B consumes M4A's authoritative effective Strength and Willpower, and retains the resolved source
Level as a typed vitality derivation input. Race/Gender therefore affect maximum HP only through M4A effective values.
There is no additional maximum-HP clamp in `object_hp_max`; max-HP effects are applied after the formula and are deferred.

### Current Hit Points and damage

The authoritative mutable field is accumulated non-negative `HP_DAMAGE`:

```text
current HP = maximum HP - HP_DAMAGE
```

`object_hp_damage_set` clamps only values below zero to zero. It does not clamp damage to maximum HP, so current HP can
be zero or negative. `critter_is_dead` interprets `current HP <= 0`; the common kill path writes the 32000 damage
sentinel. M4B preserves over-damage exactly but does not interpret it as death, change flags, animate, schedule recovery,
or perform combat.

Source script `GET_HIT_POINTS` returns current and maximum. Script healing ultimately reduces damage without taking it
below zero. M4B exposes only explicit damage/restoration mutations over the same stored representation; it does not
connect the production script host.

### Maximum Fatigue

For PC/NPC critters, `critter_fatigue_max` is:

```text
4 * FATIGUE_PTS + FATIGUE_ADJ
  + 2 * (effective Level + effective Constitution)
  + effective Willpower + 4
```

Constitution, Willpower, and Level are the dependencies; Strength is not. M4B consumes M4A's effective Constitution
and Willpower and the same resolved source Level. Race/Gender flow through those effective values. There is no separate
maximum-Fatigue clamp; maximum-Fatigue effects are deferred.

### Current Fatigue and fatigue damage

The authoritative mutable field is accumulated non-negative `OBJ_F_CRITTER_FATIGUE_DAMAGE`:

```text
current Fatigue = maximum Fatigue - fatigue damage
```

The setter clamps only the lower bound to zero and does not clamp to maximum, so current Fatigue can be zero or
negative. `critter_is_unconscious` interprets `current <= 0` for non-undead critters. M4B retains the state but does not
implement unconsciousness, knockdown, spell demaintenance, turn ending, or scheduled fatigue recovery.

Source script `GET_FATIGUE_POINTS` returns current and maximum. `HEAL_FATIGUE` subtracts from fatigue damage and clamps
at zero. M4B exposes the same narrow state mutation without script integration or wake-up behavior.

### Maximum changes while damage exists

Adding/removing source effects snapshots both maxima, applies the effect, and then adds `newMaximum - oldMaximum` to
the corresponding damage field. This keeps the derived current value unchanged; if a maximum reduction would make
damage negative, the setter's zero floor instead caps current at the new maximum. M4B applies this rule to the currently
supported M4A Race/Gender effect replacement. Future background, equipment, spell, injury, level, and arbitrary effect
changes must cross the same vitality synchronization boundary when implemented.

### Authoritative boundaries

```text
resolved retail fields + resolved source Level
  -> CharacterVitalityService persistent ObjectID-keyed vitality records

CharacterStatService effective Strength / Constitution / Willpower
  -> CharacterVitalityService maximum queries

stored non-negative HP damage / fatigue damage
  -> derived current values (maximum - damage)

future combat / spells / scripts / rest
  -> narrow vitality mutations (not implemented in M4B)

Unity WorldObject / GameObject / sprite owners
  -> presentation only; never vitality authority
```

## Smallest M4B change set

1. Extend the existing object reader/prototype projection only for the five missing vitality fields and make HP damage
   preserve instance-override presence.
2. Add one plain `CharacterVitalityService` owned by the world session. It stores source derivation inputs plus mutable
   damage fields by stable ObjectID and derives maximum/current values from M4A attributes.
3. Initialize the production PC explicitly as Level 1 with zero points/adjustments/damage. Initialize real NPCs with
   per-field instance-over-prototype precedence and reject changed-source ObjectID collisions.
4. Notify vitality when the currently supported Race/Gender effects change so source current-preservation semantics
   remain atomic. Do not add death, unconsciousness, regeneration, combat, UI, or script behavior.
5. Add focused domain/parser/lifecycle tests, a retail-fixture Play Mode validator, regression runners, and final status
   documentation.

## Retail fixtures

The primary real fixture is Human Female NPC prototype 17101,
`G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, in
`maps/arcanum1-024-fixed/101602821844.sec`. Its resolved stat array supplies Level 21; M4A already established effective
Strength 9, Constitution 16, and Willpower 8. The instance explicitly supplies HP points 0, HP damage 0, fatigue points
0, and fatigue damage 4; it inherits HP adjustment 4 and fatigue adjustment 0 from prototype 17101. The resulting
retail values are HP 76/76 and Fatigue 82/86. The production Human Male PC uses Level 1, effective Strength /
Constitution / Willpower 8, zero points/adjustments/damage, and therefore starts at HP 30/30 and Fatigue 30/30.

## Implementation and validation

### Implemented ownership and call graph

```text
WorldMapSessionCoordinator
  -> CharacterStatService
       -> persistent ObjectID-keyed base/effective attributes
       -> EffectiveAttributesChanged notification
  -> CharacterVitalityService
       -> persistent ObjectID-keyed source inputs and HP/Fatigue damage
       -> maximum/current derivation from CharacterStatService

WorldObjectSectorLoader
  -> resolve whole stat array plus six independent vitality scalars
  -> register source character attributes
  -> register source vitality exactly once per persistent PC/NPC identity

ProductionPlayerLifecycle
  -> WorldMapSessionCoordinator.GetOrCreatePlayer
       -> explicit Human Male attribute state
       -> explicit Level 1, zero-points/adjustments/damage vitality state

Unity WorldObject / sprite / GameObject lifecycle
  -> presentation only
  -> cannot create, replace, reset, or mutate authoritative vitality
```

`CharacterVitalitySource` retains the immutable resolved derivation inputs. `PersistentCharacterVitalityState` retains
the mutable accumulated HP and fatigue damage plus synchronized maxima. `CharacterVitalityService` is the only M4B
mutation/query boundary. Reloading the same source identity returns the same record; a changed type, prototype, or
resolved vitality source for that identity fails explicitly. The obsolete presentation-side `WorldObject.HpDamage`
counter was removed so Unity objects cannot become a second authority.

HP and fatigue damage may exceed their maxima. Damage and restore operations reject negative amounts, use checked
arithmetic, and leave the other vitality channel unchanged. Restore floors accumulated damage at zero. Supported
Race/Gender replacement is observed through `CharacterStatService`; the service shifts damage by the maximum delta,
preserving current values where possible and capping at a reduced maximum only when the source zero floor requires it.

### Validated source values

The source-audit command read the retail fixture and its prototype through the production parsers:

- instance: HP points 0, HP adjustment inherited, HP damage 0, fatigue points 0, fatigue adjustment inherited,
  fatigue damage 4;
- prototype 17101: HP points 0, HP adjustment 4, HP damage 0, fatigue points 0, fatigue adjustment 0,
  fatigue damage 2;
- resolved Level 21 state: HP points 0, HP adjustment 4, HP damage 0, fatigue points 0, fatigue adjustment 0,
  fatigue damage 4;
- effective Strength 9, Constitution 16, Willpower 8 produce HP 76/76 and Fatigue 82/86.

The explicit production PC baseline is Level 1, Human Male, effective Strength / Constitution / Willpower 8, with all
six vitality scalars zero, producing HP 30/30 and Fatigue 30/30.

### Test and Play Mode results

- Focused `M4BCharacterVitality`: 20 passed, 0 failed, 0 skipped, 0 inconclusive.
- Required regressions: M4A 13/13, M3E 21/21, M3D 18/18, M3C 13/13, M3B 13/13, M3A 8/8, M2B 8/8,
  M2A 16/16, PlayerNavigation 21/21, M1A 7/7, M1B 11/11, WorldSessionState 27/27, and PortalArtResolver 2/2.
- Complete EditMode suite: 369 passed, 0 failed, 0 skipped, 0 inconclusive.
- Unity 6000.0.71f1 compiled with zero compiler errors.
- Physical TestTerrain Play Mode validation loaded the exact retail NPC, verified the source values above, applied
  controlled NPC damage to HP 71/76 and Fatigue 75/86, and retained it through Original/Enhanced/Original rebuild,
  unload/reload, and A-to-B-to-A traversal. The production PC was mutated/restored to HP 26/30 and Fatigue 25/30 and
  retained the same state through the same presentation/sector lifecycle. Fourteen distinct vitality records were
  present for the loaded characters, with one record and one presentation per identity, unique coordinator/loader/
  navigation ownership, navigation still bound to the production PC, 0 new warnings, and 0 errors.
- The retained M3E/M3D/M3C regression suites verify that inventory capacity, stack transactions, and equipment
  mutations remain green while vitality is independently session-owned.

Death, unconsciousness, regeneration, combat/attack resolution, healing items or spells, poison, equipment/spell/
background effects, leveling, skills, UI, resting, encumbrance consequences, script-host integration, and save
serialization remain deferred.

The exact recommended next milestone is **M4C — Skill and Progression State**: begin with a source audit of skill
storage/ranks, XP/level fields and thresholds, point pools, prerequisite/order semantics, and PC/NPC initialization;
then implement only the smallest presentation-independent ObjectID-keyed state slice justified by that audit. Combat,
UI, dialogue, scripted progression rewards, and save serialization remain outside M4C unless separately authorized.
