using System;
using System.Collections.Generic;
using UnityEngine;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// A runtime, mutable game object — the source of truth for one placed thing (scenery, door,
    /// critter, …) and the bridge between its parsed data, its on-screen view, and interaction.
    /// State changes here (e.g. a door's art id) re-render the view and update <see cref="WorldState"/>.
    /// </summary>
    public sealed class WorldObject : MonoBehaviour
    {
        public ObjectType Type;
        public Vector2Int Tile;          // render-space tile (gx, gy)
        public uint ArtId;               // current art id (mutable — drives the sprite)
        public string DisplayName;       // description.mes name, for tooltips
        public int PrototypeNumber;
        public SpriteRenderer View;
        public Sprite Icon;              // item icon for the loot/inventory UI (no map sprite)
        public int Weight;               // OBJ_F_ITEM_WEIGHT (items only)
        public int Worth;                // OBJ_F_ITEM_WORTH — base coin value (items only)
        public WeaponFields WeaponData;  // parsed OBJ_F_WEAPON_* (weapon items only)
        public bool Blocks;              // contributes to the walkability grid
        public int SourceFlags;           // effective OBJ_F_FLAGS used by source-semantic traversal
        public Vector2 TilePosition;      // fractional sector-local gameplay position
        public bool IsMoving;
        public float PixelsPerUnit { get; internal set; } = 100f;

        // Interaction state (extend per type as features land).
        public bool IsOpen;              // portals/doors
        public bool PortalOpenable = true; // portals: false for single-frame art ("Non Opening Window")
        public bool Locked;              // portals: SAT_SET_LOCK_STATE (stored; player-side enforcement is later)
        public int DialogNum;            // NPCs: dialog number from OBJ_F_SCRIPTS (0 = none)
        public int UseScriptNum;         // SAP_USE script from OBJ_F_SCRIPTS (door teleporters/levers; 0 = none)
        public int ExamineScriptNum;     // SAP_EXAMINE script (signs/examinable scenery — usually floats text; 0 = none)
        public int HeartbeatScriptNum;       // SAP_HEARTBEAT script (periodic AI / self-gating NPCs; 0 = none)
        public int FirstHeartbeatScriptNum;  // SAP_FIRST_HEARTBEAT script (runs once; 0 = none)
        public bool Off;                 // OF_OFF — inactive/hidden (e.g. a heartbeat toggled it off); not rendered
        public bool FirstHeartbeatDone;  // SAP_FIRST_HEARTBEAT already fired
        public float NextHeartbeat;      // Time.time of the next heartbeat tick (engine ai_timeevent schedule)

        /// <summary>Per-attachment-point script state (engine <c>Script{flags, counters}</c> in
        /// <c>OBJ_F_SCRIPTS_IDX</c>): local flags bit-packed in <see cref="ScriptSlotState.Flags"/>, four
        /// 8-bit counters packed in <see cref="ScriptSlotState.Counters"/>. Persists across invocations —
        /// shared by the script VM (SCT/SAT local flags/counters) and the dialog <c>lf</c>/<c>lc</c> codes
        /// (which the engine stores on the SAP_DIALOG slot).</summary>
        public sealed class ScriptSlotState
        {
            public uint Flags;
            public uint Counters;
        }

        private Dictionary<int, ScriptSlotState> _scriptSlots;

        /// <summary>The persistent script state for one attachment point (created on first use).</summary>
        public ScriptSlotState ScriptSlot(int sap)
        {
            _scriptSlots ??= new Dictionary<int, ScriptSlotState>();
            if (!_scriptSlots.TryGetValue(sap, out ScriptSlotState s))
                _scriptSlots[sap] = s = new ScriptSlotState();
            return s;
        }

        /// <summary>All touched script slots (null until one is used) — the save system diffs them.</summary>
        public IReadOnlyDictionary<int, ScriptSlotState> ScriptSlotsView => _scriptSlots;
        public int InvLocation = -1;     // items: worn slot (1000–1008) or loose grid cell; -1 = unset

        // Combat/death state. (The full project also lazily attaches a character model — stats,
        // skills, effects — here; that layer hasn't migrated to this repository yet.)
        public bool IsDead;
        public int HpDamage;             // OBJ_F_HP_DAMAGE — damage taken; current HP = MaxHp − HpDamage (see Docs/Combat.md)
        public bool IsFollower;          // NPCs: recruited into the party via a dialog `jo` (join) effect
        public int ReactionBase = 50;    // NPCs: OBJ_F_NPC_REACTION_BASE — authored starting reaction (50 = neutral)
        public int RetailPriceMultiplier; // NPCs: OBJ_F_NPC_RETAIL_PRICE_MULTIPLIER — barter markup % (0 = unset)
        public string SubstituteInventoryOid; // NPCs: OBJ_F_NPC_SUBSTITUTE_INVENTORY — merchant store container OID key
        public int GoldQuantity;         // gold piles: OBJ_F_GOLD_QUANTITY — coins in the stack
        public int AmmoQuantity;         // ammo items: OBJ_F_AMMO_QUANTITY — shots left in the stack
        public int AmmoItemType = -1;    // ammo items: OBJ_F_AMMO_TYPE (matches a weapon's AmmoType)
        public int Material;             // OBJ_F_MATERIAL — drives item-drop/melee sound classes
        public int SoundEffect;          // OBJ_F_SOUND_EFFECT — the object's sound-id base (0 = none)
        public int BuyObjectScriptNum;   // NPCs: SAP_BUY_OBJECT script — merchant's "will I buy this?" veto (0 = none)
        public int DialogOverrideNum;    // NPCs: SAP_DIALOG_OVERRIDE script — its .dlg also holds barter chatter overrides
        public int ItemFlags;            // items: OBJ_F_ITEM_FLAGS (OIF_* — won't-sell, stolen, …)
        public int ItemSpell = -1;       // usable magic items: OBJ_F_ITEM_SPELL_1 (spell id; -1 = none — engine's 10000)
        public int SpellMana;            // usable magic items: OBJ_F_ITEM_SPELL_MANA_STORE charges (1/use, negative = infinite)
        public int WrittenSubtype = -1;  // written items: OBJ_F_WRITTEN_SUBTYPE (WRITTEN_TYPE_* — 5 = schematic); -1 = not written
        public int WrittenStartLine;     // written items: OBJ_F_WRITTEN_TEXT_START_LINE (a blueprint's schematic.mes key)
        public int NpcFlags;             // NPCs: OBJ_F_NPC_FLAGS (ONF_* — KOS 0x100, KOS_OVERRIDE 0x800, FENCE, …)
        public int CritterFlags;         // critters: OBJ_F_CRITTER_FLAGS (OCF_* — undead/animal/monster/mechanical …)
        public int AiPacket;             // NPCs: OBJ_F_NPC_AI_DATA — rules/ai_params.mes row (KOS/flee thresholds)
        public int SocialClass;          // NPCs: OBJ_F_NPC_SOCIAL_CLASS (SOCIAL_CLASS_* — 6 = guard)
        public int Origin;               // NPCs: OBJ_F_NPC_ORIGIN — home town/area id (guard protection, reputation filters)
        public int Faction;              // NPCs: OBJ_F_NPC_FACTION (0 = none; same nonzero faction = allies)
        public int WillKosScriptNum;     // NPCs: SAP_WILL_KOS script — kill-on-sight veto hook (0 = none)
        public int[] StatBase;           // NPCs: OBJ_F_CRITTER_STAT_BASE_IDX (proto/instance) — real base stats; null ⇒ defaults
        public int[] SpellTech;          // NPCs: OBJ_F_CRITTER_SPELL_TECH_IDX — college levels 0–16, tech degrees 17–24; null ⇒ none
        public int[] BasicSkills;        // NPCs: OBJ_F_CRITTER_BASIC_SKILL_IDX — ranks by BASIC_SKILL_* (combat/dialog)
        public int[] TechSkills;         // NPCs: OBJ_F_CRITTER_TECH_SKILL_IDX — ranks by TECH_SKILL_*
        public int[] Resistances;        // NPCs: OBJ_F_RESISTANCE_IDX — base damage resistances by RESISTANCE_TYPE_*
        public int? ArmorAc;             // items: OBJ_F_ARMOR_AC_ADJ — armour's AC bonus
        public int[] ArmorResist;        // items: OBJ_F_ARMOR_RESISTANCE_ADJ_IDX — armour's resistance bonus by type


        // Identity + inventory: holders (containers/critters/player) own a list of item WorldObjects.
        public string Oid;               // this object's ObjectID key
        public string ParentOid;         // holder's OID key (items only), else null
        public ArcanumObjectId Identity { get; internal set; }
        public ArcanumObjectId ParentIdentity { get; internal set; }
        public WorldMapSessionCoordinator Session { get; internal set; }
        public List<WorldObject> Inventory;  // lazily created when this object holds items

        public List<WorldObject> EnsureInventory() => Inventory ??= new List<WorldObject>();

        /// <summary>Supplied by the loader: produces the sprite for this object's current state.</summary>
        public Func<WorldObject, Sprite> ReRender;

        /// <summary>Supplied by the presentation owner for stateful portal frame changes. This changes
        /// only the existing visual frame; gameplay code remains responsible for validating interaction,
        /// collision, sounds, and scheduling the original engine's frame sequence.</summary>
        public Func<int, bool> SetVisualFrame;

        /// <summary>Swap the art id and refresh the sprite (used when a door opens, etc.).</summary>
        public void SetArt(uint artId)
        {
            ArtId = artId;
            if (ReRender != null && View != null)
            {
                Sprite s = ReRender(this);
                if (s != null) View.sprite = s;
            }
        }

        /// <summary>Shows an authored portal frame and keeps <see cref="ArtId"/> / <see cref="IsOpen"/>
        /// synchronized. Returns false when no portal presentation owns that frame.</summary>
        public bool TrySetPortalVisualFrame(int frameIndex)
        {
            if (Session != null) return false; // Managed portals can only transition through session state.
            if (Type != ObjectType.Portal || SetVisualFrame == null || !SetVisualFrame(frameIndex)) return false;
            ArtId = (ArtId & ~(31u << 14)) | ((uint)frameIndex << 14);
            IsOpen = frameIndex != 0;
            return true;
        }

        public bool RequestPortalOpen(bool open) => Session != null && Session.Portals.Request(Identity, open);

        internal void ApplyPortalState(uint artId, bool open)
        {
            ArtId = artId;
            IsOpen = open;
            SetVisualFrame?.Invoke(PortalTransitionScheduler.Frame(artId));
        }

        internal void ApplyMovementState(Vector2 tilePosition, uint artId, bool moving)
        {
            TilePosition = tilePosition;
            Tile = new Vector2Int(Mathf.RoundToInt(tilePosition.x), Mathf.RoundToInt(tilePosition.y));
            IsMoving = moving;
            transform.localPosition = IsoProjection.TileToWorld(tilePosition.x, tilePosition.y, PixelsPerUnit);
            if (View != null) View.sortingOrder = (Tile.x + Tile.y) * 2 + 1;
            if (ArtId != artId) SetArt(artId);
        }
    }
}
