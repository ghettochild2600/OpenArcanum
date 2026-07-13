using System;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime
{
    /// <summary>How an item changed hands (drives the pickup/drop/equip sounds).</summary>
    public enum ItemMove { PickUp, Drop, Equip }

    /// <summary>Semantic UI moments — mapped onto the engine's <c>SND_INTERFACE_*</c> id block by the
    /// audio subscriber, in data terms, not here.</summary>
    public enum UiSound
    {
        ButtonClick, WindowOpen, WindowClose, BookOpen, BookClose, BookPageTurn, BookSwitch,
        LevelUp, QuestComplete, Exclamation,
    }

    /// <summary>One resolved attack, for whoever cares (audio, combat log, …).</summary>
    public readonly struct AttackEvent
    {
        public readonly WorldObject Attacker;
        public readonly WorldObject Target;
        public readonly Combat.Weapon Weapon;
        public readonly bool Hit;
        public readonly bool Critical;

        public AttackEvent(WorldObject attacker, WorldObject target, Combat.Weapon weapon, bool hit, bool critical)
        {
            Attacker = attacker; Target = target; Weapon = weapon; Hit = hit; Critical = critical;
        }
    }

    /// <summary>
    /// The game's event hub (DI singleton), split into domain sections: gameplay systems publish semantic
    /// moments through their own section and know nothing about the subscribers (audio today; combat log,
    /// achievements, tutorials tomorrow). Plain C# events — no allocations, no string topics.
    /// </summary>
    public sealed class GameEvents
    {
        public readonly CombatEvents Combat = new CombatEvents();
        public readonly WorldEvents World = new WorldEvents();
        public readonly ItemEvents Items = new ItemEvents();
        public readonly UiEvents Ui = new UiEvents();
        // (The full project adds a Magick section — spell-cast audio — which follows the spell system.)
    }

    public sealed class CombatEvents
    {
        public event Action<AttackEvent> AttackResolved;
        public event Action<WorldObject> CritterDied;
        public event Action<bool> CombatChanged; // true = fighting somewhere near the player

        public void RaiseAttackResolved(in AttackEvent e) => AttackResolved?.Invoke(e);
        public void RaiseCritterDied(WorldObject critter) => CritterDied?.Invoke(critter);
        public void RaiseCombatChanged(bool active) => CombatChanged?.Invoke(active);
    }

    public sealed class WorldEvents
    {
        public event Action<WorldObject, int> Footstep;          // walker, tilename.mes surface sound type
        public event Action<WorldObject, bool> PortalToggled;    // portal, opened
        public event Action<WorldObject, bool> ContainerToggled; // container, opened
        public event Action<int, int> SchemeChanged;             // music idx, ambient idx (map/sector/script)

        public void RaiseFootstep(WorldObject walker, int tileSound) => Footstep?.Invoke(walker, tileSound);
        public void RaisePortalToggled(WorldObject portal, bool open) => PortalToggled?.Invoke(portal, open);
        public void RaiseContainerToggled(WorldObject container, bool open) => ContainerToggled?.Invoke(container, open);
        public void RaiseSchemeChanged(int music, int ambient) => SchemeChanged?.Invoke(music, ambient);
    }

    public sealed class ItemEvents
    {
        public event Action<WorldObject, WorldObject, ItemMove> Moved; // item, holder, kind

        public void RaiseMoved(WorldObject item, WorldObject holder, ItemMove kind) => Moved?.Invoke(item, holder, kind);
    }

    public sealed class UiEvents
    {
        public event Action<UiSound> Played;

        public void Raise(UiSound sound) => Played?.Invoke(sound);
    }
}
