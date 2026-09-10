# M2B Production SAP_USE Dispatch

## Bounded source target

M2B adds production script dispatch for one source-authored portal case without broadening interaction beyond the M2A
portal contract. The selected source asset is `scr/01162door_to_the_panarii_offices_use.scr`, attached to real portals
in `maps/caladon-panarrii temple/67108865.sec`. The physically validated instance is
`P_0000005E_0000005F_000000F6_0000003B`.

Script 1162 compares global flag 2087 with 1. A match returns `ReturnAndRunDefault`; otherwise it returns
`ReturnAndSkipDefault`. This is a useful first production case because it exercises authentic script-controlled default
behavior without inventory, quests, dialogue, combat, character progression, or a script-owned portal renderer.

## Final ownership and call graph

```text
physical Game-view click
  -> PlayerClickMoveInput
     -> WorldObjectTargetSelector (presentation-assisted alpha hit test)
        -> stable portal ArcanumObjectId only
  -> PlayerInteractionController (transient approach/cancellation)
     -> existing source-grid navigation and arrival
     -> WorldMapSessionCoordinator.ExecuteInteraction(command)
        -> authoritative actor/target/state/range validation
        -> WorldUseScriptDispatcher.DispatchUse(actorId, targetId, SAP_USE)
           -> ScriptDatabase source lookup
           -> bounded production policy preflight
           -> ScriptVm.ExecuteStrict(SAP_USE context)
              -> ProductionUseScriptHost (opaque stable ObjectID references)
              -> session-owned ScriptGlobals
           -> explicit ScriptExecutionResult
        -> skip default: successful handled interaction, no built-in action
        -> run default: existing lock/phase checks
           -> PortalTransitionScheduler.Request(toggle)
              -> PersistentObjectState
                 -> bound WorldObject domain projection
                    -> WorldObjectSpriteOwner presentation
```

`WorldMapSessionCoordinator` remains the authoritative interaction and session boundary. `WorldObjectSectorLoader`
loads the production `ScriptDatabase` from the same read-only VFS and binds it to the session, but does not own script
state or interaction decisions. `WorldObject`, `WorldObjectSpriteOwner`, input, and the validation scene remain
projections/adapters rather than global gameplay authorities.

## Identity and lifecycle contracts

The production PC contract from M1 is unchanged: the session owns deterministic identity
`G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01`, canonical map position, sector, and critter ART state. The dedicated
`ProductionPlayerLifecycle` binds and unbinds one Unity presentation, navigation binds only that PC, and neither visual
rebuild nor sector reload creates a replacement identity or NPC fallback.

Script focus values are deliberately opaque `WorldScriptObjectReference` values. For this slice, Triggerer is the
session PC ObjectID and Attachee is the persistent portal ObjectID. Unity objects, component instance IDs, sprite
owners, and colliders never enter the VM as authoritative identity. `SAP_USE` identity remains on
`PersistentObjectState`, while `ScriptGlobals` belongs to the map session and survives visual rebuilds and sector
unload/reload. Save serialization remains out of scope.

## Strict execution and default policy

The old `ScriptVm.Execute` API is retained for existing callers. Production dispatch uses `ExecuteStrict`, whose result
separates successful execution from missing, empty, invalid-context, invalid-line, unsupported-opcode, runaway, and
runtime failures. Every failure suppresses the built-in action.

The M2B production policy preflights the entire script before execution. It admits only the condition/action vocabulary
needed for the selected source behavior: true/equality/less-or-equal/global-flag/local-flag conditions and no-op,
goto, return-and-skip-default, or return-and-run-default actions. Unsupported focus, host operations, value kinds,
control flow, missing scripts, and runaway execution fail closed with explicit result metadata. No partially supported
script can mutate portal state and then fall through.

The VM decides only whether default behavior may run. It does not toggle the portal. A successful skip-default branch
returns a handled interaction with no requested portal state. A successful run-default branch returns to
`WorldMapSessionCoordinator`, which performs the existing lock and transition checks and asks the existing
`PortalTransitionScheduler` to toggle. This preserves source-grid collision timing, animation ownership, rendering,
and M2A cancellation/approach behavior.

## Validation

Validated in Unity 6000.0.71f1 on the real Panarii temple sector and script listed above. Computer Use performed a
literal Game-view click on the rendered portal. The stable target was selected, the production PC moved through the
existing navigation route, arrival executed SAP_USE 1162 with the stable PC/portal context, and flag 2087 permitted the
built-in scheduler transition. The same run first proved the clear-flag skip-default branch, then verified visual
rebuild and full sector unload/reload preservation, one PC and one instance of every production owner/controller, and
zero new warnings or errors. A focused cancellation test also proved that cancelling a pending distant approach leaves
the script resolver uncalled and cannot schedule a later portal transition.

Final test results:

- M2B SAP_USE: 8 passed, 0 failed, 0 skipped.
- M2A interaction: 16 passed, 0 failed, 0 skipped.
- PlayerNavigation: 21 passed, 0 failed, 0 skipped.
- M1A lifecycle: 7 passed, 0 failed, 0 skipped.
- M1B cross-sector: 11 passed, 0 failed, 0 skipped.
- Complete EditMode: 263 passed, 0 failed, 0 skipped.

## Remaining M2 work

M2B intentionally does not generalize the production host beyond this proven source case. Remaining work includes
researching and admitting additional SAP_USE condition/action families in bounded source-driven slices, source-accurate
lock/key behavior, sounds, Examine, containers and ground items, other object defaults, cursor/UI affordances, unloaded
cross-sector target discovery, and eventually versioned save serialization. Inventory, dialogue, combat, quests, and
character progression have not begun.
