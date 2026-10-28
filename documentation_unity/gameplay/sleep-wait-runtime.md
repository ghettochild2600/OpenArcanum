# Sleep and Wait Runtime Authority

## Source boundary

The implementation follows `arcanum-ce/src/ui/sleep_ui.c`, `src/game/townmap.c`, sector `TownMapInfo`, and retail
`rules/townmap.mes`. It reuses `SourceTimeService`, M4 vitality, M9B party membership, and M10A maintained effects.
Presentation does not decide whether sleeping or waiting is legal.

## Location classification

`SleepLocationSource` reads the selected `.sec` file. `TownMapInfo == 0` permits wilderness sleep. A nonzero town-map
identity permits town waiting only when that exact `townmap.mes` entry contains `[w:1]`; every missing, malformed,
non-waitable, or unreadable location is unavailable. The resolver fails closed.

| Mode | Available choices | Time | Recovery | Maintained effects |
|---|---|---|---|---|
| Wilderness sleep | 1, 2, 4, 8, 24 hours; until 07:00; until 20:00; until healed | One `SourceTimeService` hour per step | Each hour: HP + Heal Rate and fatigue + 3 x Heal Rate for the PC and eligible followers | Canceled once before time advances |
| Town wait | 1, 2, 4, 8, 24 hours; until 07:00; until 20:00 | Same source clock | None | Canceled once before time advances |
| Unavailable | None | No mutation | No mutation | No mutation |

Dead, unconscious, or in-combat PCs reject before mutation. Town Until Healed also rejects before time, vitality, or
effect mutation. Save/load persists the source clock and authoritative consequences; no transient sleep transaction is
restored.

## Bed boundary

The source takes a scenery bed object, runs its SAP_USE script, checks `OBJ_F_SCENERY_WHOS_IN_ME`, enters/leaves the
bed, and doubles per-hour recovery. Current OpenArcanum object state does not yet carry an exact occupant/rental/use-
script authority for this path. Free, rented, occupied, unavailable, and illegal bed cases therefore remain one
explicit fail-closed boundary. No generic scenery heuristic or map-name exception is used.

## Validation

Automated coverage proves exact town-map `[w:1]` parsing, wilderness recovery, follower recovery, town no-recovery,
Until Healed rejection, time advancement, maintained-effect cancellation, clock projection, invalid-state rejection,
and Save V1 state reconstruction. The complete EditMode suite passed **1229/1229**, with no failed, skipped, or
inconclusive tests.

The production UI-C.5 validator passed in the existing Unity Editor. At the authentic crash-site sector it proved
town-wait classification, the seven-option wait presentation, one-hour SourceTime advancement, no recovery, and
maintained-effect cancellation. It then selected an authentic `TownMapInfo == 0` sector, proved one-hour wilderness
recovery and the same time/effect semantics, and returned to the crash site. F/S shortcuts, HUD controls, and dynamic
clock refresh were also exercised. The final cleared Console contained 0 errors and 0 warnings.

## Follow-up boundary

Bed support requires a narrow source-backed vertical slice: scenery subtype recognition, stable occupant state,
SAP_USE execution/rollback, rental permission from existing scripts/campaign facts, enter/leave-bed lifecycle, and
the two-hour-equivalent recovery multiplier. Do not implement only the visual bed interaction or infer permission from
ART identity.
