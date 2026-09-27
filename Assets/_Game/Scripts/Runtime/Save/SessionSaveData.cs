using System.Collections.Generic;

namespace Arcanum.Runtime.Save
{
    public sealed class SessionSaveData
    {
        public string Format { get; set; }
        public int Version { get; set; }
        public WorldSaveData World { get; set; }
        public List<ObjectSaveData> Objects { get; set; }
        public List<CharacterSaveData> Characters { get; set; }
        public CampaignSaveData Campaign { get; set; }
        public PartySaveData Party { get; set; }
        public MagicSaveData Magic { get; set; }
        public TechnologySaveData Technology { get; set; }
    }

    public sealed class TechnologySaveData
    {
        public List<TechnologyCharacterSaveData> Characters { get; set; }
    }

    public sealed class TechnologyCharacterSaveData
    {
        public string Identity { get; set; }
        public int[] DisciplineRanks { get; set; }
    }

    public sealed class MagicSaveData
    {
        public long ElapsedMilliseconds { get; set; }
        public long NextEffectId { get; set; }
        public List<MagicCharacterSaveData> Characters { get; set; }
        public List<ActiveSpellEffectSaveData> ActiveEffects { get; set; }
    }

    public sealed class MagicCharacterSaveData
    {
        public string Identity { get; set; }
        public int[] CollegeRanks { get; set; }
        public int MasteryCollege { get; set; }
    }

    public sealed class ActiveSpellEffectSaveData
    {
        public long Id { get; set; }
        public int SpellId { get; set; }
        public string CasterIdentity { get; set; }
        public string TargetIdentity { get; set; }
        public int Magnitude { get; set; }
        public long StartedAtMilliseconds { get; set; }
        public long NextUpkeepAtMilliseconds { get; set; }
        public long ExpiresAtMilliseconds { get; set; }
    }

    public sealed class PartySaveData
    {
        public string LeaderIdentity { get; set; }
        public List<PartyMemberSaveData> Members { get; set; }
    }

    public sealed class PartyMemberSaveData
    {
        public string Identity { get; set; }
        public bool Forced { get; set; }
    }

    public sealed class WorldSaveData
    {
        public string CurrentMap { get; set; }
        public string SelectedSector { get; set; }
        public ulong NextDynamicIdentity { get; set; }
        public PlayerSaveData Player { get; set; }
        public List<string> Tombstones { get; set; }
    }

    public sealed class PlayerSaveData
    {
        public string Identity { get; set; }
        public string MapPath { get; set; }
        public float MapX { get; set; }
        public float MapY { get; set; }
        public uint ArtId { get; set; }
    }

    public sealed class ObjectSaveData
    {
        public string Identity { get; set; }
        public string AuthoredParentIdentity { get; set; }
        public string SourceSector { get; set; }
        public int Type { get; set; }
        public int PrototypeNumber { get; set; }
        public int NameIndex { get; set; }
        public int SocialClass { get; set; }
        public long? AuthoredLocation { get; set; }
        public uint ArtId { get; set; }
        public bool Off { get; set; }
        public bool Locked { get; set; }
        public int UseScriptNum { get; set; }
        public int DialogNum { get; set; }
        public int ItemFlags { get; set; }
        public uint? InventoryArtId { get; set; }
        public int WeaponFlags { get; set; }
        public int GenericFlags { get; set; }
        public int UnitWeight { get; set; }
        public int FootprintWidth { get; set; }
        public int FootprintHeight { get; set; }
        public int InventoryLocation { get; set; }
        public int? StackQuantity { get; set; }
        public bool PortalOpen { get; set; }
        public float TileX { get; set; }
        public float TileY { get; set; }
        public PlacementSaveData Placement { get; set; }
        public bool RuntimeCreated { get; set; }
        public bool DeathConsequencesProcessed { get; set; }
    }

    public sealed class PlacementSaveData
    {
        public int Kind { get; set; }
        public string Sector { get; set; }
        public float TileX { get; set; }
        public float TileY { get; set; }
        public string ParentIdentity { get; set; }
        public int WornLocation { get; set; }
    }

    public sealed class CharacterSaveData
    {
        public string Identity { get; set; }
        public int ObjectType { get; set; }
        public int? PrototypeNumber { get; set; }
        public CharacterAttributeSaveData Attributes { get; set; }
        public CharacterVitalitySaveData Vitality { get; set; }
        public CharacterProgressionSaveData Progression { get; set; }
        public CharacterDerivedSaveData Derived { get; set; }
    }

    public sealed class CharacterAttributeSaveData
    {
        public int[] BaseValues { get; set; }
        public int SourceRace { get; set; }
        public int SourceGender { get; set; }
        public int Race { get; set; }
        public int Gender { get; set; }
        public bool HasInstanceStatOverride { get; set; }
    }

    public sealed class CharacterVitalitySaveData
    {
        public int SourceLevel { get; set; }
        public int HitPointPoints { get; set; }
        public int HitPointAdjustment { get; set; }
        public int SourceHitPointDamage { get; set; }
        public int FatiguePoints { get; set; }
        public int FatigueAdjustment { get; set; }
        public int SourceFatigueDamage { get; set; }
        public int HitPointDamage { get; set; }
        public int FatigueDamage { get; set; }
    }

    public sealed class CharacterProgressionSaveData
    {
        public int SourceLevel { get; set; }
        public int SourceExperience { get; set; }
        public int SourceUnspentPoints { get; set; }
        public int[] SourceBasicPacked { get; set; }
        public int[] SourceTechnicalPacked { get; set; }
        public bool IsMonstrous { get; set; }
        public bool HasInstanceStatOverride { get; set; }
        public bool HasInstanceBasicSkillOverride { get; set; }
        public bool HasInstanceTechnicalSkillOverride { get; set; }
        public int Experience { get; set; }
        public int Level { get; set; }
        public int UnspentPoints { get; set; }
        public int[] PurchasedPoints { get; set; }
        public int[] Training { get; set; }
    }

    public sealed class CharacterDerivedSaveData
    {
        public int BaseArmorClass { get; set; }
        public int[] Resistances { get; set; }
        public int SourceAlignment { get; set; }
        public int Alignment { get; set; }
        public int MagickPoints { get; set; }
        public int TechPoints { get; set; }
        public int TechnologyPointAdjustment { get; set; }
        public int ReactionBase { get; set; }
        public bool IsAloof { get; set; }
        public bool IsMonstrous { get; set; }
        public bool HasInstanceStatOverride { get; set; }
        public bool HasInstanceArmorClassOverride { get; set; }
        public bool HasInstanceResistanceOverride { get; set; }
        public bool HasInstanceReactionOverride { get; set; }
    }

    public sealed class CampaignSaveData
    {
        public int StoryState { get; set; }
        public ulong QuestClock { get; set; }
        public List<IndexedIntSaveData> GlobalVariables { get; set; }
        public List<int> GlobalFlags { get; set; }
        public List<IndexedIntSaveData> PcVariables { get; set; }
        public List<int> PcFlags { get; set; }
        public List<QuestSaveData> Quests { get; set; }
        public List<ScriptAttachmentSaveData> Attachments { get; set; }
        public List<ReactionSaveData> Reactions { get; set; }
        public List<int> KnownAreas { get; set; } = new List<int>();
    }

    public sealed class IndexedIntSaveData
    {
        public int Index { get; set; }
        public int Value { get; set; }
    }

    public sealed class QuestSaveData
    {
        public int Number { get; set; }
        public int PcState { get; set; }
        public int GlobalState { get; set; }
        public uint TimestampDays { get; set; }
        public uint TimestampMilliseconds { get; set; }
    }

    public sealed class ScriptAttachmentSaveData
    {
        public string Identity { get; set; }
        public int AttachmentPoint { get; set; }
        public uint Flags { get; set; }
        public uint Counters { get; set; }
    }

    public sealed class ReactionSaveData
    {
        public string NpcIdentity { get; set; }
        public string PcIdentity { get; set; }
        public int Adjustment { get; set; }
    }
}
