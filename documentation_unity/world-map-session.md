# World map session state

Implemented on `feature/world-session-state` and validated in Unity 6000.0.71f1.

## Runtime boundary

Sector decoding and sprite ownership remain in `WorldObjectSectorLoader`, while mutable gameplay state survives that
presentation lifecycle in `WorldMapSessionCoordinator`:

`ObjectInstanceReader` -> `WorldObjectSectorLoader` -> `WorldMapSessionCoordinator` -> `WorldObject` ->
`WorldObjectSpriteOwner` -> `ArtTextureFactory`

The coordinator owns the in-memory state table, loaded runtime bindings and one portal transition scheduler. The loader
resolves source records, derives effective identities, restores state before creating runtime objects, and captures
state before presentation teardown. `WorldObjectSpriteOwner` remains presentation-only and never decides stable portal
state. Terrain and batched tile rendering remain outside this boundary.

## Persistent identity

State is keyed by `ArcanumObjectId`, using the same meaningful fields as the original engine rather than Unity instance
IDs, names, or raw padding bytes:

- `A`: authored permanent number.
- `GUID`: authored globally unique identity, used heavily by `.mob` records and references.
- `P`: engine-derived static identity containing full map tile coordinates, sector-list load index and map number.
- `NULL`: no persistent identity.
- `HANDLE`: process-local runtime handle; never a persistent state key.
- `BLOCKED`: prototype marker; never a placed-instance state key.

Static `.sec` records serialized with `NULL` receive the original engine's `objid_id_perm_by_load_order` equivalent at
load time. The loader combines sector coordinates with the local tile, uses the record's zero-based sector list index
as the temporary ID, and obtains the authoritative map number from `rules/MapList.mes`. `.mob` records retain their
serialized GUIDs. Inventory records remain nonvisual and retain their serialized parent ObjectID relationship.

The loader validates duplicate or conflicting persistent identities before replacing the current presentation. In the
default real sector this changed the audit from 93 GUID states plus 594 identity-less records to 687 persistent states:
594 positional and 93 GUID, with all 75 inventory parent references retained.

## State schema and lifecycle

`PersistentObjectState` deliberately stores only the state currently supported by production runtime behavior:

- effective ART ID, including facing and stable frame bits;
- portal open/closed state;
- lock and visibility/off state;
- source sector, type, prototype and authored location for collision validation;
- parent ObjectID for inventory/reference continuity;
- fractional sector-local tile position for controlled-critter movement.

Loading creates or reuses each record and applies it to the new `WorldObject`. Unloading cancels portal work, restores
the last stable portal state, captures supported runtime values, unbinds the object, then destroys presentation
ownership. Reloading reuses the same state objects. State is intentionally in-memory only; save-game serialization and
off-sector time-event continuation are later milestones.

Controlled-critter movement samples are written through the coordinator before presentation updates. If a sector is
unloaded mid-step, the fractional position is retained while WALK is normalized to STAND with the same facing, so a
reload never resumes a stale visual action or loses the gameplay position. Navigation details are documented in
`player-navigation.md`.

## Portal transitions

`PortalTransitionScheduler` is owned and ticked once by the coordinator. It reproduces the original source semantics:

- windows switch directly between frames 0 and 1;
- rotations 6/0 use door frames 4,5,6 to open and 5,4,0 to close;
- rotations 2/4 use door frames 1,2,3 to open and 2,1,0 to close;
- odd rotations normalize to the corresponding even source facing;
- the first frame is immediate and remaining frames use integer `1000 / ART FPS` millisecond intervals;
- invalid metadata does not invent transitions, overlapping requests are rejected, and cancellation is idempotent;
- cancellation rolls back to the last completed open/closed state before capture.

The scheduler updates the runtime ART frame bits and asks the existing presentation owner to display that already-owned
frame. Presentation cannot author stable managed portal state, and ordinary animation timing remains driven by original
ART metadata.

## Graphics rebuild boundary

Original/Enhanced rebuilds remain opt-in presentation operations. A rebuild recreates sprites from retained ART source
identity without changing runtime or persistent ART ID, portal state, transform, scale, visual offset, or selected
frame. Existing HD loader/fallback rules are unchanged. No transform compensation or terrain behavior was added.

## Validation

EditMode coverage validates identity equality/hashing, duplicate and source-collision rejection, positional key
formation, nonpersistent identity exclusion, parent retention, restoration after GameObject destruction, all eight door
facings, exact transition timing, direct windows, invalid metadata, overlapping requests, cancellation rollback,
presentation non-authority, and isolation of unrelated state. The final complete suite passed 200/200.

The production validator loaded `maps/arcanum1-024-fixed/122473678402.sec` and selected a real seven-frame, 8 FPS door:

- ObjectID `P_000190AE_0001C864_000000FB_00000001`
- prototype 2036
- ART `art/portal/toue3au0.art`
- ART ID `0x33102800`, requested rotation 5

Two complete cycles validated Open -> unload -> reload, Close -> unload -> reload, interrupted Opening rollback, and
interrupted Closing rollback. Observed frames were 1,2,3 opening and 2,1,0 closing at the source 0.125-second interval.
Each reload retained 549 visual owners, five animators, 706 frames, the same state record, transforms, presentation
offsets, texture ownership and the sector's one pre-existing `UnsupportedArtType` classification. The session ended
with 23 bound portals and zero active transitions. Graphics rebuilt Original -> Enhanced -> Original during lifecycle
checks without mutating gameplay state. The final Unity Console showed zero warnings and zero errors.

## Commits

- `1ea9b1a` Add typed in-memory world session state
- `52395dd` Own portal transitions and unload rollback in session state
- `1c75cbc` Derive persistent identities for static sector objects
- `c2049b3` Validate world session persistence and portal timing

## Remaining limitations

- State is session-memory only; no save-game serialization exists yet.
- Only one object sector is presented at a time, while state can accumulate from visited sectors.
- Dynamic creation/destruction, inventory attachment, scripts, collision, sounds, damage and full
  portal interaction policy remain outside this slice.
- The validation sector has one known unsupported ART-type record; its exact classification remains stable on reload.
- Terrain replacement, bulk extraction/upscaling and original game-data modification were not started.
