# M4C Skill and Progression Source Audit

This document records the source audit completed before M4C production changes. The authoritative references are the retail data mounted read-only through `GameData` and the Arcanum CE recovery in `src/game/skill.c`, `skill.h`, `level.c`, `stat.c`, and `stat.h`.

## Exact skill identity and storage

The source uses 12 basic skill IDs followed by four technical skill IDs. OpenArcanum will expose one typed, numerically exact ID space:

| ID | Skill | Source group/index | Governing effective attribute |
|---:|---|---|---|
| 0 | Bow | Basic 0 | Dexterity |
| 1 | Dodge | Basic 1 | Dexterity |
| 2 | Melee | Basic 2 | Dexterity |
| 3 | Throwing | Basic 3 | Dexterity |
| 4 | Backstab | Basic 4 | Dexterity |
| 5 | Pick Pocket | Basic 5 | Dexterity |
| 6 | Prowling | Basic 6 | Perception |
| 7 | Spot Trap | Basic 7 | Perception |
| 8 | Gambling | Basic 8 | Intelligence |
| 9 | Haggle | Basic 9 | Willpower |
| 10 | Heal | Basic 10 | Intelligence |
| 11 | Persuasion | Basic 11 | Charisma |
| 12 | Repair | Tech 0 | Intelligence |
| 13 | Firearms | Tech 1 | Perception |
| 14 | Pick Locks | Tech 2 | Dexterity |
| 15 | Disarm Traps | Tech 3 | Perception |

Basic and technical skills occupy object array fields 221 and 222 respectively. Each entry packs permanent purchased points in bits 0–5 and training in bits 6–7. Instance arrays replace prototype arrays as a whole when present. One purchased point produces four base/effective skill units before effects and caps.

The governing-stat cap table for effective attribute values 1 through 20 is:

`3,3,3,3,3,7,7,7,11,11,11,15,15,15,19,19,19,20,20,20`

Effects are not part of this slice, so M4C effective skill is `min(4 * purchased points, governing-stat cap, 20)`. The source's monstrous-critter melee override is tied to critter flags and additional special training rules; it is recorded but not treated as ordinary purchased state.

## Training

Training IDs are exact: None 0, Apprentice 1, Expert 2, Master 3. Minimum effective skill levels are 0, 1, 9, and 18. Upward changes must be exactly one tier at a time and meet the minimum; equal assignments are no-ops and direct downward assignments are allowed by the source. The Educator background propagation and trainer/dialogue mechanics are outside M4C.

## Progression

The relevant stat-array slots are Level 17, Experience Points 18, and Unspent Character Points 21. Retail defaults are level 1, XP 0, and five unspent points. Source maxima are level sentinel 51, XP 2,000,000,000, and 56 unspent points.

Only PCs recalculate levels from awarded XP. The loop crosses any number of thresholds atomically, awards one character point per gained playable level, and one additional point at every fifth level. The source uses level 51 as the terminal XP-table sentinel: reaching its threshold caps XP without changing the stored playable level above 50. Auto-level schemes, followers, difficulty/effect XP modifiers, UI notifications, and XP-producing gameplay are outside M4C.

The exact `rules/xp_level.mes` threshold table and the selected retail fixture's resolved packed fields were captured by `OpenArcanum/M4C/Run Source Audit` before production implementation.

### Retail audit results

`rules/xp_level.mes` contains these cumulative thresholds:

| Levels | Thresholds |
|---|---|
| 1–10 | 0, 2,100, 4,600, 7,700, 11,400, 15,500, 20,300, 25,600, 31,600, 38,300 |
| 11–20 | 45,600, 53,600, 62,400, 71,900, 82,200, 93,300, 105,300, 118,200, 132,000, 146,700 |
| 21–30 | 162,500, 179,300, 197,200, 216,300, 236,500, 257,900, 280,600, 304,600, 330,000, 356,800 |
| 31–40 | 385,100, 414,900, 446,300, 479,500, 514,300, 551,000, 589,500, 630,000, 672,500, 717,100 |
| 41–50 | 764,000, 813,100, 864,600, 918,500, 975,000, 1,034,200, 1,096,200, 1,161,100, 1,229,000, 1,300,000 |

Key 51 is absent. Combined with the recovered `LEVEL_MAX == 51` terminal branch, this confirms level 50 is the highest playable level and 51 is not a playable threshold. M4C treats level 50 as the maximum and retains the source XP field's independent 2,000,000,000 bound.

The chosen real fixture is `maps/arcanum1-024-fixed/101602821844.sec`, ObjectID `G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F`, NPC prototype 17101. It has an instance stat override: level 21, XP 162,500, and zero unspent points. Its instance basic array is `[2,0,2,1,0,0,0,0,2,1,1,1]`; its instance technical array is `[0,1,0,0]`. Both prototype arrays are all zero, proving whole-array instance precedence. These entries contain no training bits. Governing effective attributes are Dexterity 9, Intelligence 10, Perception 10, Willpower 8, and Charisma 10. The decoded base ranks are Bow 8, Melee 8, Throwing 4, Gambling 8, Haggle 4, Heal 4, Persuasion 4, and Firearms 4.

The same sector also proves packed training data: ObjectID `G_CF3204ED_D908_5D40_9828_5D7E0C46313C`, prototype 17096, has entries 131 (`3 | Expert << 6`) and 66 (`2 | Apprentice << 6`). This confirms point count and training are independent values packed into the same source byte-sized entry.

## Bounded decisions

- Authoritative state will be session-owned and keyed by persistent `ArcanumObjectId`; Unity objects remain projections.
- Production PC initialization is explicit: level 1, XP 0, five character points, zero purchased skill points, and no training.
- NPC initialization uses exact instance-over-prototype source arrays and stat slots.
- Skill purchases cost one character point and change one purchased point atomically only if the next four-unit base rank fits the governing effective-attribute cap.
- Technical-skill aptitude side effects, attribute advancement, character creation/leveling UI, combat/quest XP sources, dialogue checks, equipment effects, magic/tech progression, crafting, trainer interactions, and save serialization are deferred.
- Attribute advancement is deferred because the recovered setter is coupled to the complete effect/prerequisite graph; M4C does not invent a partial rule.

## Implemented authority and lifecycle

`WorldMapSessionCoordinator` owns one lazy `CharacterProgressionService` beside the existing character-stat and
vitality services. The progression service owns `PersistentCharacterProgressionState` records keyed by stable
`ArcanumObjectId`; each record carries immutable source identity plus mutable XP, level, unspent character points,
purchased skill points, and training. No `GameObject`, `WorldObject`, sprite owner, or editor harness owns these
values.

The production registration graph is:

`WorldObjectSectorLoader` -> resolve instance/prototype source fields -> `CharacterStatService` ->
`CharacterProgressionService` -> `CharacterVitalityService`

The loader registers real PC/NPC source state before constructing presentation. Re-registering the same ObjectID and
source returns the same progression record; a changed source or identity collision fails explicitly. Sector unload,
visual rebuild, and traversal therefore affect only projections. The production PC is registered explicitly through
the session coordinator with the documented development baseline.

`CharacterProgressionService` exposes typed base/effective skill and training queries, exact threshold queries, a
PC-only atomic XP award, an atomic one-point skill increase, and a narrow training assignment operation. NPC XP does
not silently recalculate levels. Monstrous melee remains a source-derived special case: effective melee begins at 20,
is still bounded by the governing Dexterity cap, and training is derived from effective rank plus levels 10/20/30.

M4B now consumes progression through `ICharacterLevelProvider`. A level change triggers vitality recomputation while
retaining accumulated damage semantics; it does not reset HP or fatigue. The vitality service keeps its source-level
fallback for isolated callers that do not supply the progression service.

## Validation

Focused M4C EditMode coverage passed **29/29** with zero failures, skips, or inconclusive tests. It covers typed IDs,
source precedence and packed training, PC/NPC initialization, governing caps, all training tiers and transition
failure, exact XP thresholds, below/single/multiple-threshold awards, level and point bounds, atomic skill failures,
monstrous melee, stable ObjectID records, M4A effective-attribute use, and M4B level/damage integration.

The required regression matrix passed **227/227** across M4C, M4B, M4A, M3E, M3D, M3C, M3B, M3A, M2B, M2A,
PlayerNavigation, M1A, M1B, WorldSessionState, and PortalArtResolver. The complete EditMode suite passed **398/398**.
Unity compilation completed with zero C# errors.

Computer Use Play Mode validation loaded the real NPC fixture and confirmed level 21, XP 162,500, Bow 8, and
Firearms 4. The production PC began at level 1, XP 0, five points, and no skills/training. A controlled 2,100-XP award
advanced it to level 2 and awarded one point. Spending one point raised Bow to rank 4 and returned the pool to five;
a second increase failed the Dexterity-8 governing cap with no mutation. Maximum HP and fatigue rose from 30 to 32
while damaged current values remained unchanged. Exact state/reference identity survived Original -> Enhanced ->
Original rebuild, NPC unload/reload, and PC A -> B -> A traversal. The final session held 14 unique progression
records with unique session/presentation owners and recorded **0 new warnings and 0 errors**.

## Deferred boundary and recommended next milestone

M4C deliberately does not implement attribute purchase, technical-aptitude side effects, temporary/background/
equipment/spell skill modifiers, auto-level schemes, XP-producing combat/quest/dialogue systems, interactive trainers,
character creation or leveling UI, crafting, magic/technology progression, or save serialization.

The exact recommended next milestone is **M4D — Remaining Derived Character Statistics and Alignment/Reaction
Inputs**. Begin with a source audit of the remaining non-vital derived-stat fields and alignment/reaction storage and
ordering, then add only ObjectID-keyed presentation-independent state needed by later gameplay consumers. Keep combat,
dialogue, UI, script-host expansion, and save serialization deferred.
