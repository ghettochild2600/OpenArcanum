using Arcanum.Formats.Objects;
using Arcanum.Formats.Sound;
using Arcanum.Runtime.World;
using UnityEngine;
using Material = Arcanum.Formats.Sound.Material;

namespace Arcanum.Runtime.Audio
{
    /// <summary>
    /// The ONLY audio subscriber: wires <see cref="GameEvents"/> to the <see cref="SfxSelect"/> selection
    /// key math and the services. Holds no filenames and no tuning — sound ids come from the engine's
    /// id-space layout + the mes data; positions come from the event payloads.
    /// </summary>
    public sealed class AudioDirector
    {
        // UiSound → the engine's SND_INTERFACE_* id block (snd.h:4-32) — the id VALUES are the engine's
        // interface id space; the files they map to live in snd_interface.mes.
        private static readonly int[] UiIds =
        {
            /* ButtonClick  */ 3000,
            /* WindowOpen   */ 3012,
            /* WindowClose  */ 3013,
            /* BookOpen     */ 3008,
            /* BookClose    */ 3009,
            /* BookPageTurn */ 3010,
            /* BookSwitch   */ 3011,
            /* LevelUp      */ 3005,
            /* QuestComplete*/ 3028,
            /* Exclamation  */ 3004,
        };

        private readonly AudioService _sfx;
        private readonly MusicService _music;
        private static int Rand(int lo, int hi) => Random.Range(lo, hi + 1);

        public AudioDirector(GameEvents events, AudioService sfx, MusicService music)
        {
            _sfx = sfx;
            _music = music;

            events.Ui.Played += OnUi;
            events.World.PortalToggled += OnPortalToggled;
            events.World.ContainerToggled += OnContainerToggled;
            events.World.Footstep += OnFootstep;
            events.World.SchemeChanged += OnSchemeChanged;
            events.Items.Moved += OnItemMoved;
            events.Combat.AttackResolved += OnAttackResolved;
            events.Combat.CritterDied += OnCritterDied;
            events.Combat.CombatChanged += OnCombatChanged;
        }

        private void OnUi(UiSound sound)
        {
            int i = (int)sound;
            if (i >= 0 && i < UiIds.Length) _sfx.PlayUi(UiIds[i]);
        }

        private void OnPortalToggled(WorldObject portal, bool open)
        {
            int id = SfxSelect.ObjectSound(portal.SoundEffect, (int)(open ? PortalSound.Open : PortalSound.Close));
            if (id > 0) _sfx.PlayAt(id, portal.transform.position);
        }

        private void OnContainerToggled(WorldObject container, bool open)
        {
            int id = SfxSelect.ObjectSound(container.SoundEffect, (int)(open ? PortalSound.Open : PortalSound.Close));
            if (id > 0) _sfx.PlayAt(id, container.transform.position);
        }

        private void OnFootstep(WorldObject walker, int tileSound)
        {
            int armor = Formats.Art.CritterArtResolver.ArmorOf(walker.ArtId);
            _sfx.PlayAt(SfxSelect.Footstep(armor, tileSound, Rand), walker.transform.position);
        }

        private void OnItemMoved(WorldObject item, WorldObject holder, ItemMove kind)
        {
            bool gold = item.Type == ObjectType.Gold;
            var material = (Material)item.Material;
            int id = kind == ItemMove.Drop
                ? SfxSelect.ItemDrop(material, item.Weight, gold)
                : SfxSelect.ItemPickup(material, item.Weight, gold);
            _sfx.PlayUi(id); // inventory moves are UI-close, engine plays them flat
        }

        private void OnAttackResolved(AttackEvent e)
        {
            if (e.Target == null) return;
            Vector3 at = e.Target.transform.position;
            if (!e.Hit)
            {
                int miss = SfxSelect.ObjectSound(e.Weapon?.SoundEffect ?? 0, (int)WeaponSound.Miss);
                _sfx.PlayAt(miss > 0 ? miss : SfxSelect.FallbackMissSoundId, at);
                return;
            }

            // Authored weapon hit (HIT_1/HIT_2 at random), else the material-matrix fallback.
            int id = e.Weapon != null && e.Weapon.SoundEffect > 0
                ? e.Weapon.SoundEffect + (Rand(0, 1) == 0 ? (int)WeaponSound.Hit1 : (int)WeaponSound.Hit2)
                : SfxSelect.MeleeHit((Material)(e.Weapon?.MaterialId ?? 0), e.Weapon?.Weight ?? 0,
                    (Material)TargetMaterial(e.Target), Rand);
            _sfx.PlayAt(id, at);
            // The victim's pain bark (sfx_critter_sound CRITICALLY_HIT on real hits).
            if (e.Critical)
            {
                int hurt = SfxSelect.ObjectSound(e.Target.SoundEffect, (int)CritterSound.CriticallyHit);
                if (hurt > 0) _sfx.PlayAt(hurt, at);
            }
        }

        private void OnCritterDied(WorldObject critter)
        {
            int id = SfxSelect.ObjectSound(critter.SoundEffect, (int)CritterSound.Dying);
            if (id > 0) _sfx.PlayAt(id, critter.transform.position);
        }

        private void OnCombatChanged(bool active) => _music.SetCombat(active);

        private void OnSchemeChanged(int music, int ambient) => _music.PlayScheme(music, ambient);


        // The engine keys the hit sound off the victim's ARMOR material when it wears one (sfx.c:252-256).
        private static int TargetMaterial(WorldObject target)
        {
            if (target.Inventory != null)
                foreach (WorldObject item in target.Inventory)
                    if (item.InvLocation == 1005 && item.Type == ObjectType.Armor) // ITEM_INV_LOC_ARMOR
                        return item.Material;
            return target.Material;
        }
    }
}
