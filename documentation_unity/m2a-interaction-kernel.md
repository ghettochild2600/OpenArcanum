# M2A Interaction Kernel and Portal Use

## Pre-change audit

At completed M1, input and portal state have no production interaction bridge:

```text
left click
  -> PlayerClickMoveInput
  -> screen/world/tile conversion
  -> PlayerNavigationController.TrySetDestination

loaded .sec/.mob portal
  -> WorldObjectSectorLoader
  -> persistent ObjectID + PersistentObjectState
  -> WorldMapSessionCoordinator.BindPortal
  -> PortalTransitionScheduler
  -> WorldObject.ApplyPortalState
  -> WorldObjectSpriteOwner presentation
```

`PlayerClickMoveInput` treats every undragged click as ground movement. `WorldObjectSectorLoader` creates portal
projections but exposes no authoritative ObjectID lookup or click-target selection. `WorldMapSessionCoordinator` owns
the persistent object table and portal scheduler, but has no command API. `WorldObject.RequestPortalOpen` is a narrow
legacy convenience call and does not validate actor identity, range, target type, or command results.

The object reader already decodes `SAP_USE` and `SAP_EXAMINE` numbers from instance and prototype records. The loader
does not currently project those numbers onto production `WorldObject` instances. The script VM has attachment-point
models and test/demo hosts, but no production world host; invoking a script-bearing door as though it were unscripted
would therefore skip authored behavior.

### Source interaction semantics

The checked-in `arcanum-ce` research source provides the narrow evidence needed for this slice:

- `anim_goal_node_use_object` performs range setup, move-near-object, then object use/default behavior.
- `sub_428930` assigns range 2 to portals and scenery; containers and critters use range 1.
- `sub_425130` accepts the action when `location_dist(actor, target) <= range`.
- `location_dist` is `max(abs(dx), abs(dy))`, the same Chebyshev metric used by eight-direction source-grid movement.
- `object_script_execute(..., SAP_USE, ...)` returns true when no script is attached or when the script permits default
  behavior. A script can explicitly suppress the built-in action.
- The portal default toggles closed/open state. `portal_open` makes the edge non-blocking on the first opening frame;
  `portal_close` restores blocking only on final frame 0. The existing `PortalTransitionScheduler` already reproduces
  that state/frame timing.

M2A therefore uses a portal interaction range of two source tiles, supports only unscripted animated portals, treats a
locked portal conservatively as blocked until key/lock rules exist, and delegates all state/frame changes to the
existing scheduler. Examine, script execution, lock/key resolution, sounds, containers, and other object defaults are
outside this slice.

## Implemented architecture

```text
PlayerClickMoveInput (input adapter)
  -> presentation-assisted deterministic portal hit test
     -> stable ArcanumObjectId only
  -> PlayerInteractionController (transient approach/cancellation orchestration)
     -> InteractionApproachPlanner (range-2 reachable source tile)
     -> PlayerNavigationController (existing route and cross-sector machinery)
     -> WorldMapSessionCoordinator.ExecuteInteraction(command)
        -> authoritative actor/target lookup
        -> type/range/script/lock/scheduler validation
        -> PortalTransitionScheduler.Request(toggle)
           -> PersistentObjectState + bound WorldObject projection
              -> WorldObjectSpriteOwner presentation
```

`WorldInteractionCommand` is a small immutable value containing actor ObjectID, target ObjectID, command type, and an
optional chosen map-global interaction position. `WorldInteractionResult` reports `Success`, `Approaching`,
`ActorNotFound`, `TargetNotFound`, `OutOfRange`, `NoReachableInteractionPosition`, `InvalidTarget`, `Blocked`,
`Unsupported`, or `Cancelled`. `WorldMapSessionCoordinator.ExecuteInteraction` is the authoritative execution
boundary. It resolves semantic `ArcanumObjectId` values against session-owned state, uses the canonical map-global PC
position for range, refuses authored `SAP_USE` scripts until a production script host exists, refuses locked doors
until key rules exist, and delegates the toggle to the already-bound `PortalTransitionScheduler`.

This paragraph records the completed M2A boundary. M2B subsequently replaced the blanket scripted-portal refusal with
the bounded, fail-closed production dispatch described in
[`m2b-sap-use-dispatch.md`](m2b-sap-use-dispatch.md); the M2A command, identity, approach, scheduler, and presentation
contracts remain unchanged.

Effective `SAP_USE` identity is copied from instance/prototype data into `PersistentObjectState`; it is not owned by a
sprite or GameObject. The scheduler remains the sole owner of Opening/Closing progress and stable `PortalOpen` state.
`WorldObject` is the loaded domain projection consumed by navigation and presentation. `WorldObjectSpriteOwner` only
shows the scheduler-selected source ART frame.

### Target selection

`WorldObjectTargetSelector` considers only active, persistent world portals with a rendered sprite. It tests the actual
readable ART alpha pixel when available, otherwise uses the sprite bounds. Overlap is deterministic: greater render
order wins and equal order is resolved by the lexical persistent ObjectID. The result returned to gameplay is only the
stable ObjectID; no Unity instance ID or collider becomes authoritative. A miss retains the existing ground-click
movement behavior.

### Approach and intent lifecycle

The approach controller retains only transient intent. A new manual navigation request cancels it; a second interaction
replaces it; disappearance/unload invalidates it; route failure ends it; and a visual rebuild leaves it untouched. The
approach planner reuses `DeterministicTilePathfinder`, searches walkable tiles within source range, and chooses by route
length followed by stable tile order. It does not duplicate movement or pathfinding.

The concrete lifecycle is `Idle -> ApproachingTarget -> Executing -> Completed/Cancelled`. An in-range click executes
immediately. A distant click records the ObjectID command, submits the chosen map-global approach destination to the
existing navigation controller, and executes only after the canonical PC position is within Chebyshev range two. A
manual ground destination, replacement target, missing target, unload, or failed route clears the pending command, so
it cannot execute later.

## Real-sector validation

Validated in Unity 6000.0.71f1 on `maps/arcanum1-024-fixed/122473678402.sec` using source-authored portal
`P_00019096_0001C870_00000164_00000001`, prototype 2036, `art/portal/toue3au0.art`, base ART ID `0x33102800`, rotation
5, seven frames at 8 FPS. The portal has no `SAP_USE` script and is unlocked.

Computer Use performed a literal Game-view click on the rendered door. `PlayerClickMoveInput` returned that exact
ObjectID and `Success`; frames played through the existing scheduler and the stable state became Open. The formerly
blocked rotation-5 edge became traversable and the production PC walked through it. A second Use closed the door and
restored blocking.

The same run then verified a distant approach and automatic Use, normal WALK/arrival behavior, Original/Enhanced
rebuilds during pending intent, Open state across full sector unload/reload, the same PC and door state instances,
manual-destination cancellation, second-target replacement, no stale execution, and exactly one coordinator, loader,
navigation controller, interaction controller, PC runtime, and PC sprite owner. The passing run recorded zero new
warnings and zero errors.

Unity validation after the final state boundary change:

- M2A interaction: 16 passed, 0 failed, 0 skipped.
- PlayerNavigation: 21 passed, 0 failed, 0 skipped.
- M1A lifecycle: 7 passed, 0 failed, 0 skipped.
- M1B cross-sector: 11 passed, 0 failed, 0 skipped.
- WorldSessionStateTests: 27 test cases passed in the complete run.
- PortalArtResolverTests: 2 test cases passed in the complete run.
- Complete EditMode: 255 passed, 0 failed, 0 skipped.

## Explicit non-goals

This slice does not implement inventory, containers, item use, Examine behavior, a production script host, sounds,
lockpicking or keys, combat, dialogue, quests, character progression, save serialization, or a general cursor/UI
system. Targeting is deliberately portal-only. Pending interaction intent is not serialized. A click cannot target an
unloaded cross-sector object; M1 navigation remains intact, but multi-sector object discovery is a later interaction
slice. Script-bearing, locked, single-frame, and currently-transitioning portals return explicit non-success results
instead of silently bypassing missing gameplay rules.
