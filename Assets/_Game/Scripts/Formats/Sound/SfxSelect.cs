using System;

namespace Arcanum.Formats.Sound
{
    /// <summary>Per-critter sound offsets over <c>OBJ_F_SOUND_EFFECT</c> (sfx.h:16-25).</summary>
    public enum CritterSound
    {
        CriticallyHit = 0,
        Dying = 1,
        DyingGruesome = 2,
        Fidgeting = 3,
        Attacking = 4,
        Alerted = 5,
        Agitated = 6
    }

    /// <summary>Per-weapon sound offsets over <c>OBJ_F_SOUND_EFFECT</c> (sfx.h:8-14).</summary>
    public enum WeaponSound
    {
        Use = 0,
        Busted = 1,
        Destroyed = 2,
        Miss = 3,
        Hit1 = 4,
        Hit2 = 5,
        CritHit = 6,
        OutOfAmmo = 7
    }

    /// <summary>Portal/container sound offsets over <c>OBJ_F_SOUND_EFFECT</c> (sfx.h:26-37).</summary>
    public enum PortalSound
    {
        Open = 0,
        Close = 1,
        Locked = 2
    }

    /// <summary>The engine's object materials (<c>OBJ_F_MATERIAL</c> values, materials.h order).</summary>
    public enum Material
    {
        Stone = 0,
        Brick,
        Wood,
        Plant,
        Flesh,
        Metal,
        Glass,
        Cloth,
        Liquid,
        Paper,
        Gas,
        Force,
        Fire,
        Powder
    }

    /// <summary>
    /// The sound-SELECTION key math — ports sfx.c verbatim: an event plus world state (materials, armor,
    /// tile surface) becomes an integer sound id for <see cref="SoundTable.Resolve"/>. The constants here
    /// are the engine's id-space layout (each cited), not tuning — the actual files live in the mes data.
    /// </summary>
    public static class SfxSelect
    {
        public const int FallbackMissSoundId = 4010; // sfx.c:286 — weapon with no authored miss sound

        // Melee material classes (sfx.c MeleeMaterialSound): FLESH=0, LIGHT_METAL=1, HEAVY_METAL=2, STONE=3, WOOD=4.
        private static readonly int[] MeleeClassByMaterial = // sfx_melee_sounds (sfx.c:64-79)
        {
            3, 3, 4, 4, 0, 2, 1, 0, 0, 0, 0, 1, 0, 0,
        };

        // Item pickup/drop classes (sfx.c ItemMaterialSound): FLESH=0, LIGHT_METAL=1, HEAVY_METAL=2,
        // STONE=3, WOOD=4, GLASS=5, CLOTH=6, PAPER=7.
        private static readonly int[] ItemClassByMaterial = // sfx_item_sounds (sfx.c:108-123)
        {
            3, 3, 4, 4, 0, 2, 5, 6, 0, 7, 0, 1, 0, 0,
        };

        // Footstep bases per tile surface (sfx.c:139-146 sfx_base_footstep_sound_ids), indexed by the
        // tilename.mes sound column: DIRT=0, SAND=1, SNOW=2, STONE=3, WATER=4, WOOD=5.
        private static readonly int[] FootstepBaseByTileSound = { 2904, 2912, 2916, 2920, 2928, 2932 };

        // Critter art-id armor codes (tig/art.h TigArtArmorType).
        public const int ArmorChain = 3, ArmorPlate = 4, ArmorPlateClassic = 6;

        /// <summary>A footstep id (sfx.c:330-357): plate armor uses its own bank (2900) on any surface;
        /// otherwise the tile's surface bank, +4 for chainmail on wood/stone/dirt; every bank holds 4
        /// random variants.</summary>
        public static int Footstep(int armorType, int tileSound, Func<int, int, int> random)
        {
            int id;
            if (armorType == ArmorPlate || armorType == ArmorPlateClassic)
            {
                id = 2900; // BASE_PLATE_ARMOR_FOOTSTEP_SOUND_ID (sfx.c:128)
            }
            else
            {
                if (tileSound < 0 || tileSound >= FootstepBaseByTileSound.Length) tileSound = 0;
                id = FootstepBaseByTileSound[tileSound];
                if (armorType == ArmorChain && (tileSound == 5 || tileSound == 3 || tileSound == 0))
                    id += 4; // the chainmail second bank (sfx.c:346-351)
            }

            return id + random(0, 3);
        }

        /// <summary>The material-matrix melee hit (sfx.c:277): <c>7000 + weaponClass·20 + targetClass·3 +
        /// rand(0,1)</c>. Target material is its ARMOR's material for armored critters; heavy metal at
        /// ≤ 2000 stones demotes to light (sfx.c:252-269).</summary>
        public static int MeleeHit(Material weaponMaterial, int weaponWeight, Material targetMaterial, Func<int, int, int> random)
        {
            int wm = MeleeClass(weaponMaterial, weaponWeight);
            int tm = MeleeClass(targetMaterial, int.MaxValue);
            return 7000 + 20 * wm + 3 * tm + random(0, 1);
        }

        /// <summary>Pickup/drop by material class (sfx.c:189/:210): base 5950/5960 + class; gold has its
        /// own pair 5958/5968 (sfx.c:89-99).</summary>
        public static int ItemPickup(Material material, int weight, bool isGold)
            => isGold ? 5958 : 5950 + ItemClass(material, weight);

        public static int ItemDrop(Material material, int weight, bool isGold)
            => isGold ? 5968 : 5960 + ItemClass(material, weight);

        /// <summary>An object-authored sound: its <c>OBJ_F_SOUND_EFFECT</c> base plus the action offset
        /// (critter death/attack, weapon use/miss, portal open/close/locked, …); −1 when unauthored.</summary>
        public static int ObjectSound(int soundEffectBase, int offset) => soundEffectBase > 0 ? soundEffectBase + offset : -1;

        private static int MeleeClass(Material m, int weight)
        {
            int cls = (int)m >= 0 && (int)m < MeleeClassByMaterial.Length ? MeleeClassByMaterial[(int)m] : 0;
            if (cls == 2 && weight <= 2000) cls = 1; // heavy metal but light item → light metal (sfx.c:267)
            return cls;
        }

        private static int ItemClass(Material m, int weight)
        {
            int cls = (int)m >= 0 && (int)m < ItemClassByMaterial.Length ? ItemClassByMaterial[(int)m] : 0;
            if (cls == 2 && weight <= 2000) cls = 1; // ITEM_MAT heavy-metal demotion (sfx.c:184-187)
            return cls;
        }
    }
}
