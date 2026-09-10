# M2C SAP_USE Candidate Audit

## Outcome

The mounted clean game corpus contains 22 distinct script numbers attached to placed portal records. M2B script 1162
is the only GREEN family under the M2C constraints, and it is already supported. Every additional family requires a
gameplay or presentation domain that this slice explicitly forbids. No production policy, VM, host, session, portal,
navigation, presentation, or test code was changed.

The audit therefore stops at the candidate gate, as required. Admitting one opcode in isolation would not reproduce the
selected script's observable source behavior and would weaken the fail-closed production boundary.

## Candidate table

All examples below are real placed portal records. Their serialized ObjectIDs are NULL; the existing loader derives
their stable positional ObjectIDs from full map location, load order, and map number when the sector is loaded.

| Class | Script and resource | Real portal example | Conditions | Actions | M2B support / missing semantics | Missing-system dependency |
|---|---|---|---|---|---|---|
| GREEN baseline only | 1162 — `scr/01162door_to_the_panarii_offices_use.scr` | `maps/caladon-panarrii temple/67108865.sec`, proto 2029, map tile 98,87 | `Eq` | `ReturnAndRunDefault`, `ReturnAndSkipDefault` | Fully supported; adds nothing new | None; this is the completed M2B case |
| YELLOW | 1463 — `scr/01463pollocks_door_use.scr` | `maps/arcanum1-024-fixed/68853695436.sec`, proto 2028, tile 62250,65700 | `True` | `PrintLine`, `ReturnAndRunDefault` | Only `PrintLine` is missing | Production message/dialog text presentation |
| YELLOW | 1598 — `scr/01598wellington_door_use.scr` | `maps/arcanum1-024-fixed/68786586570.sec`, proto 2028, tile 62127,65606 | `True` | `CallScriptIn`, `ReturnAndRunDefault` | Timed script call is missing | Persistent script scheduler; called script 1597 also needs `ObjIsOpen`, sound, portal mutation, and self-rescheduling |
| YELLOW | 2591 / 2595 / 2643 — K'na Tha, Dernholm pits, and Arronax teleport scripts | `maps/k'na tha/805306384.sec`, proto 2029; `maps/dernholm pits/335544326.sec`, proto 2031; `maps/void - arronax's interior/335544325.sec`, proto 2033 | `True` | `Teleport`, then run-default or skip-default | VM opcode exists, but the production host deliberately exposes no teleport | Map/world travel, destination validation, and transition ownership |
| YELLOW | 2970 — `scr/02970nasrudins_tomb_door_use.scr` | `maps/caladon-panarrii temple/67108864.sec`, proto 2029, tile 54,93 | `Eq`, `True` | `Goto`, `CallScriptIn`, `SetLockState`, `ReturnAndRunDefault` | Global-flag branch is supported; delayed call and authoritative lock mutation are not | Script scheduling, lock rules, and the same delayed close/sound family as 1598 |
| YELLOW | 30019 — `scr/30019unblockshroudedhills.scr` | `maps/arcanum1-024-fixed/88248157567.sec`, proto 2030, tile 90099,84189 | `SectorIsBlocked`, `True` | `ToggleSectorBlocked`, `ReturnAndRunDefault` | Exactly two narrow opcodes are missing | Authoritative blocked-sector state must affect map/cross-sector travel, which M2C forbids |
| RED | 1149 — `scr/01149secret_door_tomb_use.scr` | `maps/caladon-panarrii temple-basement/67108865.sec`, proto 2029, tile 87,82 | `True`, `Eq`, `ObjIsOpen` | object typing, arithmetic/assignment, portal/lock mutation, floating text, object creation, returns | Several unadmitted object/value/action families | Dynamic objects, presentation text, locks, and direct portal behavior |
| RED | 1177 — `scr/01177kan_hua_door.scr` | `maps/caladon-panarrii temple/134217729.sec`, proto 2029, tile 114,148 | `True`, `Eq`, `Le`, named/dialog/dead object checks | loops, calls, attack, remove-script, returns | Broad object focus and control/side-effect surface | NPC search, dialogue state, combat, script attachment mutation |
| RED | 1459 / 1523 / 1527 / 2056 / 2340 — guarded and quest-controlled doors | Tarant, Dernholm, Caladon Castle sectors; protos 2028/2035/2029 | global flags plus named/dead/invisible/hearing or PC quest checks | vicinity loops, object assignment, script calls, attack/movement, remove-script, lock/text actions, returns | Multiple unsupported neighboring semantics in every script | Quest state, NPC AI/perception, dialogue, combat, locks, and persistent script attachment state |
| RED | 30000–30006 — magical/mechanical/arrow/bullet/fire/electrical/poison trap templates | Bates tunnel and Caladon trap-disarm dungeon sectors; protos 2028–2031 | none; zero-entry scripts | none | Strict execution correctly reports `EmptyScript`; treating emptiness as successful default would weaken failure semantics | Trap event lifecycle and source trap behavior are absent |

## Why there is no additional GREEN family

- Script 1463 is superficially the smallest, but its only new behavior is displaying authored dialog text. A no-op host
  would be observably wrong, and building production dialog/message presentation is excluded.
- Script 30019 is the smallest state mutation, but its state is meaningful only when sector traversal consumes it.
  Adding an inert flag would not reproduce the source behavior; connecting it would begin forbidden map/world travel.
- Script 1598 cannot be reduced to `CallScriptIn`: its authentic callback (1597) checks portal state, plays sound,
  directly toggles the portal, and reschedules itself. It needs a persistent timed-event ownership design.
- The teleport scripts require a map transition boundary, while the remaining door scripts depend on multiple quest,
  NPC, combat, lock, dynamic-object, or presentation systems.
- The trap scripts are empty templates, not evidence for weakening strict empty-script failure.

## Preserved production boundary

The completed M2B architecture remains unchanged: broad interpreter capability stays in `ScriptVm`; production
SAP_USE admission remains a small source-proven subset; `WorldUseScriptDispatcher` is strict and fail-closed;
`WorldMapSessionCoordinator` owns interaction/session state; `PortalTransitionScheduler` alone owns portal transition;
and Unity presentation remains projection only. Script 1162 remains the sole admitted authentic portal family.

## Recommended next milestone

Defer further SAP_USE admission until the required owning domain exists. The exact recommended next milestone is M3A:
define typed session-owned containment/inventory state, deterministic dynamic item identity, and atomic transfer
transactions without UI or script integration. This follows the dependency-ordered parity roadmap and avoids inventing
inert script-host behavior. Revisit the audited portal families only after their real dependencies are authoritative;
the smallest later presentation candidate is script 1463, and the smallest later world-state candidate is script 30019.
