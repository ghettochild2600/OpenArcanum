# M10A Phase 1 Magic Runtime Audit

Date: 2026-09-27  
Branch: `feature/session-save-load`  
Starting HEAD: `35a00da024edebc0c6942d391a7453f9864aedb3`

## Result

M10A Phase 1 is implemented and validated as a bounded production vertical slice. Magic authority is owned by a
coordinator service keyed by persistent ObjectIDs. It consumes the existing M4 character/vitality/derived-stat
services, M6 V1 save transaction, M8 turn and real-time schedulers, and M8D/M8E death path rather than duplicating
them. Unity objects, animations, graphics mode, and validation UI remain presentation only.

The vertical slice implements three named retail spells from different effect families:

| ID | Spell | College / rank | Target | Cost | AP | Range | Duration | Implemented source effect |
|---:|---|---|---|---:|---:|---:|---|---|
| 15 | Strength of Earth | Earth 1 | living, non-dead critter; self allowed | 5 fatigue | 4 | 99 | maintained; 1 fatigue / 80,000 source-game ms | four source effect-161 applications represented as one inspectable +4 Strength modifier; `No_Stack:0` |
| 55 | Harm | Necromantic Black 1 | living, non-dead critter; self rejected | 5 fatigue | 4 | 99 | instantaneous | aptitude-scaled 3-40 normal damage, magic resistance, M4 vitality, M8D/M8E death |
| 60 | Minor Healing | Necromantic White 1 | damaged living, non-dead, non-mechanical critter; self allowed | 5 fatigue | 4 | 99 | instantaneous | aptitude-scaled 5-30 hit-point restoration capped by M4 vitality |

Teleportation was considered and rejected for this phase. The current M7 world-transition authority cannot yet
represent its arbitrary destination semantics without widening M10A or bypassing stable world authority.

## Source archaeology

The primary reference was the `arcanum-ce` reconstruction and the shipped rule data mounted by the Unity source-audit
harness:

- `magictech.c` defines 16 colleges with five spells each, so IDs 0-79 map by `college = id / 5` and
  `rank = id % 5 + 1`.
- Character SpellTech source slots 0-15 store college ranks, slot 16 stores magic mastery, and slots 17-24 are the
  separate technology disciplines. The existing object/prototype readers already expose these arrays.
- Default rank prerequisites are Willpower 6/9/12/15/18 and levels 1/1/5/10/15.
- The default source range is 99 tiles.
- A cast may overdraw fatigue only while the post-cost value remains above -15. A maintained spell cannot start if
  its initial cost would leave fatigue below zero.
- Dwarf cast cost is doubled. Mastery of the spell's college halves cast cost using source integer arithmetic.
- Maintained-spell capacity is `Intelligence / 4`, capped at five.
- Aptitude-scaled values use `minimum + max(0, aptitude) * (maximum - minimum) / 100`.
- Negative target aptitude increases effective magic resistance. A successful resistance roll reduces Harm after
  scaling, with direct damage retaining a minimum of one.
- `AG_THROW_SPELL` consumes exactly four turn-based AP. The three selected records have `CastingAnim=-1`; the source
  throw-spell goal therefore uses the generic unarmed action ART timing while spell eye-candy owns visuals.
- The source saves and restores magic runtime information. Phase 1 therefore persists learned college state and
  authoritative maintained effects while intentionally normalizing in-flight cast/scheduler state.

Primary source references:

- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/magictech.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/spell.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/spell.h>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/anim.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/anim_private.h>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/critter.c>
- <https://github.com/alexbatalov/arcanum-ce/blob/main/src/game/obj_flags.h>

`M10AMagicSourceAudit` also mounted the local retail archives and independently logged the exact shipped definitions
for IDs 15, 55, and 60 before runtime implementation.

## Authoritative runtime

### Typed definitions and requests

`SpellDefinition` separates immutable source semantics from `SpellCastRequest`, `SpellCastResult`, and
`ActiveSpellEffect`. `PhaseOneSpellCatalog` contains only the three audited retail definitions; no unnamed synthetic
production spell exists. A request currently carries caster ObjectID, spell ID, and target ObjectID because those are
the only target forms used by this slice.

### Learned spells and eligibility

`MagicStateService` imports source college ranks/mastery for arbitrary PC, NPC, or follower identities. It validates
the spell, persistent caster, learned rank, level, Willpower, consciousness, fatigue, maintained-slot capacity,
target, duplicate-effect rule, range/line of sight, and combat ownership before any mutation. Invalid requests do not
change fatigue, AP, scheduler state, vitality, effects, or turn ownership.

### Fatigue and turn authority

M4B remains the only fatigue authority. Magic computes the audited source cost and commits it through
`CharacterVitalityService`. In turn-based combat, M8 validates the current actor and four available AP, then commits
exactly four AP. A cast with four AP remaining does not invent an early turn advance; reaching zero uses the existing
M8 participant progression.

### Real-time timing

M8H remains the only real-time scheduler. A spell request is preflighted, then scheduled as an inspectable
`SpellCast` action. The production timing provider resolves the caster's retail unarmed generic-action ART,
calculates the action-frame effect time and full-animation recovery time, and exposes the existing BUSY/READY state.
M10A resolves the effect once at M8H's effect boundary and owns no parallel timer or frame counter.

The physical proof found and fixed one integration defect: after clearing the critter weapon-art field, SpellCast had
to select the source unarmed art variant (`1`) just as the throw-spell animation requires. Without that selection the
retail ART lookup failed closed with `TimingUnavailable`.

### Targets, traversal, and resistance

The selected target classes are living critter and damaged living critter. Minor Healing additionally rejects the
source mechanical flag (`OCF_MECHANICAL`, `0x20000000`). Cross-sector, dead, missing, forbidden-self, undamaged-heal,
out-of-range, and hard line-of-sight targets fail transactionally. Combat navigation supplies exact tile distance and
hard projectile traversal when present; noncombat same-sector placement supplies the bounded distance fallback.

Harm reads M4D Magic resistance and Magick/Tech aptitude. Its deterministic aptitude magnitude is resolved first;
source magic resistance then reduces damage before M4B mutation. Physical armor is not substituted.

### Effect architecture, stacking, and time

Instantaneous healing/damage call M4B. Lethal damage invokes the established M8D/M8E exactly-once death consequence
path. Strength of Earth registers a keyed, non-base attribute modifier in `CharacterStatService`; effective and
derived values compose from it without overwriting source/base state. Its source `No_Stack` rule rejects a second
copy before cost or effect mutation. `EndEffect` removes the modifier exactly once.

`MagicStateService.AdvanceTime` is the source-time boundary. The M8 1,000 ms completed-round hook advances it once per
combat boundary, and explicit source-time advances process every crossed 80,000 source-game-ms upkeep boundary in deterministic
effect-ID order. An unavailable caster or inability to maintain the effect ends and cleanly reverses it. Unity frame
time is not magic authority.

The project still has no general authoritative noncombat world clock. Bridging this boundary to a future world-time
service, plus proving finite-duration expiration with one authentic timed spell, is the precise remaining core item
listed under closure assessment; Phase 1 does not pretend that the combat round clock is universal.

Phase 2 subsequently established that source `magictech.c` multiplies configured real-time seconds by eight before
scheduling against game time. It therefore corrected the Phase 1 10,000-ms placeholder to an 80,000-source-ms
upkeep interval and moved elapsed authority to the shared source-time service.

## Save V1

The existing V1 envelope now has an optional `magic` domain. It stores:

- elapsed authoritative magic time and the next effect identity;
- per-character 16 college ranks and mastery;
- maintained effect identity, spell, caster, target, magnitude, start, and next upkeep time.

Restore validates all identities, ranks, mastery, effect IDs, spell family, magnitude, timestamps, references, and
No_Stack uniqueness before swapping service roots. Restored character knowledge wins over immutable source defaults
when sector presentation re-registers objects. Restored effects rebuild keyed character modifiers after M4 roots are
installed. Older V1 documents with no magic domain remain valid and start with empty runtime magic state.

The current request, casting animation, M8H pending action, combat state, targeting preview, diagnostics, and random
source are never serialized. The automated and physical proofs saved while a Harm cast was pending: load retained
knowledge and Strength of Earth, but discarded the pending cast and inactive-combat normalization prevented replay.
The save version remains 1.

## Automated validation

Focused M10A EditMode tests: **21/21**.

They cover the three retail definitions; invalid/unknown/known state; prerequisites; unconscious caster; exact fatigue
and overdraw boundaries; Dwarf/mastery cost; target legality; mechanical healing exclusion; range/LOS; deterministic
scaling and resistance; healing cap; No_Stack application/end/upkeep; exact turn AP; M8H effect/recovery and
exactly-once resolution; M8D/M8E lethal consequences; NPC source knowledge; and V1 persistence/transient
normalization.

Targeted older-system regressions: **178/178**.

- M4A attributes **13/13** because persistent effects extend effective attributes.
- M4B vitality **20/20** because casts consume fatigue and mutate HP.
- M4D derived stats **25/25** because aptitude/resistance and modifier notifications are consumed.
- M6A session save/load **25/25** and M6B migration/slots **32/32** because V1 gained an optional validated domain.
- M8B turn-based combat **23/23** because spell casting adds an AP commit boundary.
- M8D defeat **11/11** and M8E consequences **7/7** because lethal Harm uses those authorities.
- M8H real-time combat **22/22** because its action/timing records now carry spell requests/results.

No M7 or M9 focused suites were rerun: movement magic was deferred and no M9 production code changed. The complete
suite remained the global regression gate for every unrelated completed subsystem.

Complete EditMode suite: **910/910**, with **0 failed, 0 skipped, 0 inconclusive**. This is the 889-test baseline plus
21 M10A tests.

## Physical Play Mode validation

Computer Use drove the already-open Unity 6000.0.71f1 Editor and the production TestTerrain composition. The accepted
run used the production PC, the authentic Polar Bear Cub ObjectID, retail critter ART, production navigation,
production combat, M4 services, M8D/M8E, graphics rebuild, and production V1 save/load.

The representative proof passed:

1. unknown spell rejected before fatigue/vitality mutation;
2. turn-based Harm consumed exactly 4 AP and 5 fatigue, left the same actor with remaining AP, and damaged through M4;
3. Minor Healing restored its exact bounded magnitude through M4 and respected the maximum;
4. real-time Harm used production ART action/recovery times, remained non-mutating before the effect boundary,
   resolved exactly once, and changed BUSY to READY only at recovery completion;
5. Strength of Earth applied one +4 modifier, rejected a duplicate transactionally, and reverted once when the
   caster became unavailable;
6. Original -> Enhanced -> Original rebuilds did not change effect identity, magic time, or effective Strength;
7. V1 restored knowledge and the maintained effect while discarding a pending cast/combat transaction;
8. lethal Harm produced death and the processed M8E marker, and a second consequence call could not replay reward.

Accepted harness result: **0 warnings, 0 errors**.

## Deferred families and closure assessment

### 1. Required for M10A closure

A small bounded Phase 2 remains:

- introduce or bind an authoritative noncombat source-time service to `MagicStateService.AdvanceTime`;
- represent finite-duration expiration explicitly and prove it with one authentic timed spell;
- audit and prove the corresponding source dispel/cancellation end reason without moving ownership into UI or
  presentation.

This is runtime closure work, not a request to implement all spells.

### 2. Spell content using the already-complete Phase 1 runtime

Additional instantaneous healing/damage and maintained attribute-modifier spells that fit the implemented target and
effect families are content expansion. All 80 retail spells do not need individual M10A hand validation.

### 3. Deferred to M10B

Technology disciplines, schematics, technical aptitude use, crafted-item runtime, and technology UI are wholly
separate from magic despite sharing source character arrays.

### 4. Deferred to later UI/social/campaign work

Final spell selection/targeting HUD belongs to M12C. Spellcasting AI, follower spell policy, charm/control/social
behavior, summons, resurrection presentation/campaign fallout, and script-specific quest effects belong to their
own later owners.

### 5. Source ambiguous or dependency not ready

- Teleportation/location spells require a source-faithful arbitrary-location M7 boundary.
- Spell eye-candy records, audio, and script callbacks remain presentation/integration hooks; the gameplay timing is
  bounded to the audited `CastingAnim=-1` throw-spell path, but exact visual/audio callback composition is not yet
  represented.
- Environmental, projectile, corpse, summon, control, and opposing-effect families need their own source audits and
  typed target/effect boundaries before production implementation.

M10A Phase 1 is complete and provides the reusable production foundation. M10A as a whole remains open only for the
small Phase 2 clock/finite-duration/dispel closure above. M10B has not started.
