# M1 Slice: Shared Sector Ownership and Production PC Lifecycle

## Pre-change ownership and call graph

This graph records the implementation at `21d8088` before this slice changed production code.

```text
TestTerrain scene
├─ TileMapDemo (terrain GameObject)
│  ├─ serialized SectorPath
│  ├─ Start -> EnsureData -> RenderCurrentSector
│  └─ LoadSector(path) -> writes SectorPath -> RenderCurrentSector
│
└─ WorldObjectSectorLoader (object GameObject)
   ├─ independent serialized sectorPath
   ├─ Start -> LoadSector(sectorPath)
   ├─ LoadSector -> Session.ValidateSector/ClearObjects/BeginSector
   ├─ creates WorldObject + WorldObjectSpriteOwner presentations
   └─ builds SectorNavigationMap
      └─ PlayerNavigationController.Update -> TryBindConfiguredPlayer
         ├─ configured ObjectID, else first ObjectType.Pc
         └─ optional development flag -> lexically first NPC
```

`WorldMapSessionCoordinator` owned persistent object state and portal transitions, but not sector selection. Terrain did
not know it existed. The object loader created or found it lazily, and both presentation owners could therefore display
different sectors. `PlayerNavigationController` owned player discovery and could silently bind an NPC when the scene flag
was enabled. There was no authoritative player state distinct from a sector-authored `WorldObject` presentation.

## Smallest bounded change set

1. Add an assembly-neutral sector-presentation contract in `Arcanum.World` so both owners can be driven without a new
   dependency cycle.
2. Extend `WorldMapSessionCoordinator` to register the terrain and object owners, normalize one selected sector, drive
   both presentations, and publish selection/unload events.
3. Preserve standalone demo behavior only when no selection authority is bound; in the production test scene, route the
   terrain browser and startup selection through the coordinator.
4. Add a plain `PersistentPlayerState` owned by the session plus a deterministic GUID identity.
5. Add `ProductionPlayerLifecycle` to create/bind/unbind the PC presentation for the selected sector. The loader remains
   presentation/data plumbing; it does not become the player authority.
6. Remove normal configured-player NPC fallback. Navigation accepts the production lifecycle's explicit PC binding and
   continues to write movement through session state.
7. Add edit-mode tests for shared selection, failure cleanup, deterministic player identity, lifecycle state restoration,
   and NPC non-selection. Update real-sector validation to use the new coordinator path.

## Explicit non-goals

This slice does not implement cross-sector path continuation, inventory, interaction rules, combat, character creation
or progression, dialogue/scripts, save serialization, world-map travel, or a new global asset manager.

## Implemented ownership and call graph

```text
TestTerrain production composition
└─ WorldMapSessionCoordinator (authoritative session/domain owner)
   ├─ SelectSector(normalized path)
   │  ├─ TileMapDemo.PresentSector -> terrain presentation only
   │  ├─ WorldObjectSectorLoader.PresentSector -> source objects/navigation map
   │  └─ SectorSelected event
   │     └─ ProductionPlayerLifecycle.SpawnAndBind
   │        ├─ session.GetOrCreatePlayer(deterministic GUID, position, ART state)
   │        ├─ loader.CreatePlayerPresentation -> ordinary critter resolver/SpriteOwner
   │        └─ navigation.TryBind(explicit ObjectType.Pc)
   ├─ movement samples -> PersistentPlayerState -> bound WorldObject projection
   └─ ClearSelectedSector
      ├─ SectorUnloading -> lifecycle.Unbind -> navigation.Unbind
      ├─ object owner captures state and destroys its presentation root
      └─ terrain owner clears its presentation
```

`WorldMapSessionCoordinator` is the sole normalized sector selector. `TileMapDemo` and
`WorldObjectSectorLoader` implement the same presentation-owner boundary and do not select production sectors
independently. `ProductionPlayerLifecycle` owns only creation and binding of the PC projection. `WorldObject`,
`WorldObjectSpriteOwner`, the loader, and demo components remain presentation/data adapters rather than gameplay
authorities.

## Production PC identity and lifecycle contract

- Gameplay/session identity is deterministic GUID ObjectID `G_C9B7E725_E71A_F54A_B1E4_0A62FA6BCA01`, created from
  GUID `25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01` and owned by `PersistentPlayerState`.
- Presentation identity is separate: base ART ID `0x28100000`. Its fields are type `2` (`CRITTER`), human body,
  male, villager clothing `V1`, no shield, unarmed, and `STAND`. The unmodified production resolver returns
  `art/critter/hmm/hmmv1xaa.art`, present in the original source archive.
- The interrupted placeholder `0x18100000` was invalid because its high nibble selected ART type `1` (wall); the
  male bit does not replace the required critter type nibble. No NPC was selected, cloned, reclassified, or used as a
  fallback, and no special renderer was added.
- On selection, the session creates or reuses its one player-state record and the lifecycle creates one PC projection.
  Navigation binds only that explicit PC. Rebuilding sprites leaves the session record and runtime identity untouched.
  Unload captures fractional position and ART state, unbinds the projection, and removes it. Reload creates a new
  projection of the same session identity/state; an interrupted walk is normalized to `STAND` at the preserved facing.

## Validation (2026-09-07, Unity 6000.0.71f1)

- Compile/domain reload: clean, 0 warnings and 0 errors.
- Focused `M1Lifecycle` EditMode suite: 7 passed, 0 failed, 0 skipped.
- Focused `PlayerNavigation` EditMode suite: 21 passed, 0 failed, 0 skipped.
- Complete EditMode suite, including world-session and portal tests: 228 passed, 0 failed, 0 skipped.
- Computer Use entered Play Mode on real sector `maps/arcanum1-024-fixed/101602821844.sec`, issued a physical Game-view
  click, and the click was accepted and moved the production PC.
- Real-sector validation passed shared terrain/object selection, visible normal-resolver PC rendering, explicit PC-only
  navigation binding, graphics rebuild, unload/reload state restoration, and post-reload movement. It asserted exactly
  one coordinator, loader, lifecycle, navigation controller, `WorldObjects` root, production PC presentation, and PC
  `WorldObjectSpriteOwner`; the final Unity Console contained 0 warnings and 0 errors.

## Remaining M1 work

M1A is complete. The next bounded slice is cross-sector continuation: preserve a destination intent across an
authoritative sector boundary, select/present the adjacent sector through the coordinator, place the same session-owned
PC at the correct entry tile, and resume source-grid routing. Save serialization and all interaction/combat/inventory/
dialogue/progression work remain out of scope.
