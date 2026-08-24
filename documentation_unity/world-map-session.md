# Proposed world map session boundary

This milestone keeps sector decoding and sprite construction in `WorldObjectSectorLoader`, but puts mutable
gameplay state above that presentation lifecycle. The smallest production path is:

`ObjectInstanceReader` -> `WorldObjectSectorLoader` -> `WorldMapSessionCoordinator` -> `WorldObject` ->
`WorldObjectSpriteOwner` -> `ArtTextureFactory`

The reader retains both serialized 24-byte `ObjectID` values: the instance's own ID and an item's parent ID.
The coordinator keys state by the engine's semantic ID forms, not by Unity instance IDs or object names:

- `A`: authored permanent number; retained by the original engine across map pool clears.
- `GUID`: authored globally unique ID, used heavily by `.mob` records and references.
- `P`: static positional ID made from map, full tile location, and the authored temporary ID at that tile.
- `NULL`: no authoritative identity; never receives a fabricated persistent record.
- `HANDLE`: process-local runtime handle; never serialized as authoritative state.
- `BLOCKED`: prototype marker; identifies prototype records, not placed instances.

Prototype identity supplies defaults and is not the instance key. Inventory membership is the item-parent
ObjectID relationship; inventory records remain nonvisual, and unresolved parents may legitimately live outside
the loaded sector. Positional IDs are map-scoped by their serialized map member. Semantic keys ignore unused and
padding bytes just as the original `objid_is_equal` implementation does.

`WorldMapSessionCoordinator` will own the current in-memory map/session identity, the loaded-sector identity, and
the OID-indexed state table. It captures state before presentation teardown and restores it after the same source
records are recreated. The first state slice is deliberately narrow: effective ART ID (including facing/frame),
portal phase/open state, lock bit, and visibility/off state. There is no movement API in this production path yet,
so it does not persist or compensate transforms.

`WorldObject` remains the mutable runtime representation. `WorldObjectSpriteOwner` owns decoded ART/HD sprites and
can rebuild them, but should not decide portal state. A single coordinator-owned portal scheduler should advance all
active doors/windows using the original ART frame count and FPS. It reproduces `portal_open`/`portal_close` frame
selection exactly and updates `WorldObject.ArtId` before asking the presentation to show a frame. Sector unload
cancels active presentation work and restores the last stable portal state before capture; off-sector continuation
is intentionally deferred until the broader time-event system exists.

Terrain and batched tile rendering remain outside this boundary.
