using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Combat
{
    /// <summary>Arcanum's 5 damage types — order matches the engine's <c>DAMAGE_TYPE_*</c> enum.</summary>
    public enum DamageType { Normal, Poison, Electrical, Fire, Fatigue }

    /// <summary>Which skill governs an attack with this weapon (engine <c>item_weapon_skill</c>, item.c:3308).</summary>
    public enum WeaponSkill { Melee, Bow, Throwing, Firearms }

    /// <summary>
    /// A weapon's combat stats (from <c>OBJ_F_WEAPON_*</c>). A plain data model consumed by
    /// <see cref="CombatMath"/> — independent of how it's loaded. Unarmed/fist is represented by
    /// <see cref="Unarmed"/> (range 1, melee, small normal damage). NOTE: real per-weapon parsing from
    /// protos is pending a proto-field-walk fix (see Docs/Combat.md); build/test with explicit instances.
    /// </summary>
    public sealed class Weapon
    {
        public const int DamageTypeCount = 5;

        public WeaponSkill Skill = WeaponSkill.Melee;
        public readonly int[] DamageMin = new int[DamageTypeCount]; // OBJ_F_WEAPON_DAMAGE_LOWER_IDX[type]
        public readonly int[] DamageMax = new int[DamageTypeCount]; // OBJ_F_WEAPON_DAMAGE_UPPER_IDX[type]
        public int BonusToHit;      // OBJ_F_WEAPON_BONUS_TO_HIT
        public int SpeedFactor;     // OBJ_F_WEAPON_SPEED_FACTOR (→ AP cost & recoil)
        public int Range = 1;       // OBJ_F_WEAPON_RANGE (1 = melee)
        public int MinStrength;     // OBJ_F_WEAPON_MIN_STRENGTH
        public int AmmoType = 10000; // OBJ_F_WEAPON_AMMO_TYPE (10000 = none)
        public int AmmoConsumption = 1; // OBJ_F_WEAPON_AMMO_CONSUMPTION — shots drawn from the stack per attack
        public int MissileAid = -1;  // OBJ_F_WEAPON_MISSILE_AID — projectile art id (−1 = none; the flying arrow/bullet)
        public uint ItemArtId;       // the wielded item's own art — a thrown weapon (MissileAid −1) flies itself
        public int SoundEffect;      // OBJ_F_SOUND_EFFECT — the weapon's authored sound bank (0 = material fallback)
        public int MaterialId;       // OBJ_F_MATERIAL — the melee-matrix hit-sound class
        public int Weight;           // OBJ_F_ITEM_WEIGHT — heavy-metal sound demotion threshold

        /// <summary>Needs ammo items to fire (bows/guns; thrown weapons and melee don't).</summary>
        public bool UsesAmmo => AmmoType != 10000;

        public bool IsRanged => Range > 1;

        /// <summary>Source <c>combat_attack_cost</c> for the effective weapon speed.</summary>
        public int AttackActionPointCost
            => SpeedFactor > 24 ? 1 : SpeedFactor > 20 ? 2 : System.Math.Max(1, 8 - SpeedFactor / 3);

        public Weapon Clone()
        {
            var copy = new Weapon
            {
                Skill = Skill,
                BonusToHit = BonusToHit,
                SpeedFactor = SpeedFactor,
                Range = Range,
                MinStrength = MinStrength,
                AmmoType = AmmoType,
                AmmoConsumption = AmmoConsumption,
                MissileAid = MissileAid,
                ItemArtId = ItemArtId,
                SoundEffect = SoundEffect,
                MaterialId = MaterialId,
                Weight = Weight,
            };
            System.Array.Copy(DamageMin, copy.DamageMin, DamageMin.Length);
            System.Array.Copy(DamageMax, copy.DamageMax, DamageMax.Length);
            return copy;
        }

        /// <summary>Bare hands: melee, range 1, light normal damage (engine unarmed: min = base−25 (≥1), max = base+5).</summary>
        public static Weapon Unarmed()
        {
            var w = new Weapon { Skill = WeaponSkill.Melee, Range = 1, SpeedFactor = 10 };
            w.DamageMin[(int)DamageType.Normal] = 1;
            w.DamageMax[(int)DamageType.Normal] = 3;
            return w;
        }

        /// <summary>Builds a real weapon from parsed <c>OBJ_F_WEAPON_*</c> data (now that protos read
        /// correctly). Skill is decided like the engine's <c>item_weapon_skill</c>.</summary>
        public static Weapon FromFields(WeaponFields f)
        {
            if (f == null) return Unarmed();
            var w = new Weapon
            {
                BonusToHit = f.BonusToHit,
                SpeedFactor = f.SpeedFactor > 0 ? f.SpeedFactor : 10,
                Range = f.Range > 0 ? f.Range : 1,
                MinStrength = f.MinStrength,
                AmmoType = f.AmmoType,
                AmmoConsumption = f.AmmoConsumption,
                MissileAid = f.MissileAid,
                Skill = ResolveSkill(f),
            };
            for (int t = 0; t < DamageTypeCount && t < f.DamageMin.Length; t++)
            {
                w.DamageMin[t] = f.DamageMin[t];
                w.DamageMax[t] = f.DamageMax[t];
            }
            return w;
        }

        private const int OwfBoomerangs = 0x40; // OBJ_F_WEAPON_FLAGS bit (throwable returns)
        private const int AmmoArrow = 0;
        private const int AmmoBullet = 1;
        private const int AmmoCharge = 2;
        private const int AmmoFuel = 3;
        private static WeaponSkill ResolveSkill(WeaponFields f)
        {
            if ((f.Flags & OwfBoomerangs) != 0) return WeaponSkill.Throwing;
            if (f.Range >= 3 && f.MagicTechComplexity < 0
                && f.AmmoType is AmmoBullet or AmmoCharge or AmmoFuel)
                return WeaponSkill.Firearms;
            if (f.AmmoType == AmmoArrow) return WeaponSkill.Bow;
            return WeaponSkill.Melee;
        }
    }
}
