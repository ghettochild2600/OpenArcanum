using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Social;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Arcanum.Runtime.Save
{
    public enum SessionSaveFailure
    {
        None,
        InvalidPath,
        NoActiveSession,
        SerializationFailed,
        WriteFailed,
    }

    public enum SessionLoadFailure
    {
        None,
        InvalidPath,
        ReadFailed,
        MalformedJson,
        UnknownFormat,
        UnsupportedVersion,
        MissingRequiredField,
        InvalidIdentity,
        DuplicateIdentity,
        InvalidWorld,
        InvalidObject,
        InvalidPlacement,
        InvalidReference,
        ContainmentCycle,
        InvalidStack,
        InvalidCharacter,
        InvalidCampaign,
        InvalidParty,
        PresentationRebuildFailed,
        RestoreFailed,
    }

    public readonly struct SessionSaveResult
    {
        public SessionSaveFailure Failure { get; }
        public string Message { get; }
        public bool Succeeded => Failure == SessionSaveFailure.None;

        internal SessionSaveResult(SessionSaveFailure failure, string message = null)
        {
            Failure = failure;
            Message = message;
        }
    }

    public readonly struct SessionLoadResult
    {
        public SessionLoadFailure Failure { get; }
        public string Message { get; }
        public bool Succeeded => Failure == SessionLoadFailure.None;

        internal SessionLoadResult(SessionLoadFailure failure, string message = null)
        {
            Failure = failure;
            Message = message;
        }
    }

    internal sealed class SessionRestorePlan
    {
        internal string SelectedSector;
        internal ulong NextDynamicIdentity;
        internal PersistentPlayerState Player;
        internal Dictionary<ArcanumObjectId, PersistentObjectState> Objects;
        internal HashSet<ArcanumObjectId> Tombstones;
        internal CharacterStatService Characters;
        internal CharacterProgressionService Progression;
        internal CharacterVitalityService Vitality;
        internal InventoryCapacityService InventoryCapacity;
        internal CharacterDerivedStatService DerivedStats;
        internal CampaignStateService Campaign;
        internal List<PartyMember> PartyMembers;
        internal MagicSaveData Magic;
        internal TechnologySaveData Technology;
        internal EconomySaveData Economy;
        internal SocialSaveData Social;
    }

    /// <summary>Versioned, presentation-independent persistence for the bounded M1-M5 session state.</summary>
    public sealed class SessionSaveService
    {
        public const string FormatIdentifier = "OpenArcanum.SessionSave";
        public const int CurrentVersion = 1;

        internal static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64,
        };

        private readonly WorldMapSessionCoordinator _session;

        public SessionSaveService(WorldMapSessionCoordinator session)
            => _session = session ?? throw new ArgumentNullException(nameof(session));

        public SessionSaveResult SaveSession(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new SessionSaveResult(SessionSaveFailure.InvalidPath, "A save path is required.");
            if (_session.PlayerState == null || !_session.HasSelectedSector)
                return new SessionSaveResult(SessionSaveFailure.NoActiveSession,
                    "A production player and selected sector are required.");

            string json;
            try
            {
                json = SerializeCurrentSession();
            }
            catch (Exception ex)
            {
                return new SessionSaveResult(SessionSaveFailure.SerializationFailed, ex.Message);
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                return new SessionSaveResult(SessionSaveFailure.InvalidPath, ex.Message);
            }

            string directory = Path.GetDirectoryName(fullPath);
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (string.IsNullOrEmpty(directory))
                    return new SessionSaveResult(SessionSaveFailure.InvalidPath, "Save directory is required.");
                Directory.CreateDirectory(directory);
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                string written = File.ReadAllText(temporary, Encoding.UTF8);
                SessionLoadResult validation = TryBuildPlan(written, out _);
                if (!validation.Succeeded)
                    return new SessionSaveResult(SessionSaveFailure.SerializationFailed,
                        "The written temporary save did not validate: " + validation.Message);

                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
                return new SessionSaveResult(SessionSaveFailure.None);
            }
            catch (Exception ex)
            {
                return new SessionSaveResult(SessionSaveFailure.WriteFailed, ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch
                {
                    // The authoritative result has already been returned; abandoned sibling temp files are non-save data.
                }
            }
        }

        public SessionLoadResult LoadSession(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new SessionLoadResult(SessionLoadFailure.InvalidPath, "A save path is required.");
            string json;
            try
            {
                json = File.ReadAllText(Path.GetFullPath(path), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return new SessionLoadResult(SessionLoadFailure.ReadFailed, ex.Message);
            }
            return LoadJson(json);
        }

        public string SerializeCurrentSession()
            => JsonConvert.SerializeObject(CaptureData(), JsonSettings) + "\n";

        internal string SerializeData(SessionSaveData data)
            => JsonConvert.SerializeObject(data, JsonSettings) + "\n";

        public SessionLoadResult LoadJson(string json)
        {
            SessionLoadResult result = TryBuildPlan(json, out SessionRestorePlan plan);
            if (!result.Succeeded) return result;

            SessionRestorePlan rollback = null;
            try
            {
                if (_session.PlayerState != null && _session.HasSelectedSector)
                {
                    string current = SerializeCurrentSession();
                    SessionLoadResult rollbackResult = TryBuildPlan(current, out rollback);
                    if (!rollbackResult.Succeeded)
                        return new SessionLoadResult(SessionLoadFailure.RestoreFailed,
                            "The current session could not be captured transactionally: " + rollbackResult.Message);
                }

                if (_session.ApplyRestorePlan(plan)) return new SessionLoadResult(SessionLoadFailure.None);
                if (rollback != null) _session.ApplyRestorePlan(rollback);
                return new SessionLoadResult(SessionLoadFailure.PresentationRebuildFailed,
                    "Authoritative state validated, but the restored sector presentation could not be rebuilt.");
            }
            catch (Exception ex)
            {
                try
                {
                    if (rollback != null) _session.ApplyRestorePlan(rollback);
                }
                catch
                {
                    return new SessionLoadResult(SessionLoadFailure.RestoreFailed,
                        "Restore and rollback both failed: " + ex.Message);
                }
                return new SessionLoadResult(SessionLoadFailure.RestoreFailed, ex.Message);
            }
        }

        internal SessionSaveData CaptureData()
        {
            _session.CaptureLoadedStateForSave();
            PersistentPlayerState player = _session.PlayerState
                ?? throw new InvalidOperationException("No production player is registered.");
            if (!_session.HasSelectedSector) throw new InvalidOperationException("No sector is selected.");

            var data = new SessionSaveData
            {
                Format = FormatIdentifier,
                Version = CurrentVersion,
                World = new WorldSaveData
                {
                    CurrentMap = _session.CurrentMap,
                    SelectedSector = _session.SelectedSector,
                    NextDynamicIdentity = _session.NextDynamicIdentity,
                    Player = new PlayerSaveData
                    {
                        Identity = player.Identity.Key,
                        MapPath = _session.CurrentMap,
                        MapX = player.MapPosition.x,
                        MapY = player.MapPosition.y,
                        ArtId = player.ArtId,
                    },
                    Tombstones = _session.RemovedObjectIdentities.Select(identity => identity.Key)
                        .OrderBy(key => key, StringComparer.Ordinal).ToList(),
                },
                Objects = _session.States.Values.OrderBy(state => state.Identity.Key, StringComparer.Ordinal)
                    .Select(CaptureObject).ToList(),
                Characters = _session.Characters.States.Values
                    .OrderBy(state => state.Identity.Key, StringComparer.Ordinal).Select(CaptureCharacter).ToList(),
                Campaign = _session.Campaign.ExportSaveData(),
                Magic = _session.Magic.ExportSaveData(),
                Technology = _session.Technology.ExportSaveData(),
                Economy = _session.Economy.ExportSaveData(),
                Social = _session.Social.ExportSaveData(),
                Party = new PartySaveData
                {
                    LeaderIdentity = _session.Party.Leader.Key,
                    Members = _session.Party.Members.Select(member => new PartyMemberSaveData
                    {
                        Identity = member.Identity.Key,
                        Forced = member.Forced,
                    }).ToList(),
                },
            };
            data.Campaign.Reactions = _session.DerivedStats.ExportReactionAdjustments()
                .Select(value => new ReactionSaveData
                {
                    NpcIdentity = value.NpcIdentity.Key,
                    PcIdentity = value.PcIdentity.Key,
                    Adjustment = value.Adjustment,
                }).ToList();
            return data;
        }

        private ObjectSaveData CaptureObject(PersistentObjectState state)
            => new()
            {
                Identity = state.Identity.Key,
                AuthoredParentIdentity = state.AuthoredParentIdentity.IsNull ? null : state.AuthoredParentIdentity.Key,
                SourceSector = state.SourceSector,
                Type = (int)state.Type,
                PrototypeNumber = state.PrototypeNumber,
                NameIndex = state.NameIndex,
                SocialClass = state.SocialClass,
                AiData = state.AiData,
                Origin = state.Origin,
                Faction = state.Faction,
                SourceWorth = state.SourceWorth,
                MaximumHitPoints = state.MaximumHitPoints,
                HitPointDamage = state.HitPointDamage,
                RetailPriceMultiplier = state.RetailPriceMultiplier,
                InventorySourceId = state.InventorySourceId,
                SubstituteInventoryIdentity = state.SubstituteInventoryIdentity.IsNull
                    ? null : state.SubstituteInventoryIdentity.Key,
                NpcFlags = state.NpcFlags,
                BuyObjectScriptNum = state.BuyObjectScriptNum,
                ContainerFlags = state.ContainerFlags,
                AuthoredLocation = state.AuthoredLocation,
                ArtId = state.ArtId,
                Off = state.Off,
                Locked = state.Locked,
                UseScriptNum = state.UseScriptNum,
                DialogNum = state.DialogNum,
                ItemFlags = state.ItemFlags,
                InventoryArtId = state.InventoryArtId,
                WeaponFlags = state.WeaponFlags,
                GenericFlags = state.GenericFlags,
                UnitWeight = state.UnitWeight,
                FootprintWidth = state.InventoryFootprint.Width,
                FootprintHeight = state.InventoryFootprint.Height,
                InventoryLocation = state.InventoryLocation,
                StackQuantity = state.StackQuantity,
                PortalOpen = state.PortalOpen,
                TileX = state.TilePosition.x,
                TileY = state.TilePosition.y,
                Placement = CapturePlacement(state.Placement),
                RuntimeCreated = state.IsRuntimeCreated,
                DeathConsequencesProcessed = state.DeathConsequencesProcessed,
            };

        private static PlacementSaveData CapturePlacement(ObjectPlacement placement)
            => new()
            {
                Kind = (int)placement.Kind,
                Sector = placement.Kind == ObjectPlacementKind.World ? placement.Sector : null,
                TileX = placement.Kind == ObjectPlacementKind.World ? placement.TilePosition.x : 0,
                TileY = placement.Kind == ObjectPlacementKind.World ? placement.TilePosition.y : 0,
                ParentIdentity = placement.Kind == ObjectPlacementKind.World ? null : placement.ParentIdentity.Key,
                WornLocation = placement.Kind == ObjectPlacementKind.Equipped ? (int)placement.WornLocation : 0,
            };

        private CharacterSaveData CaptureCharacter(PersistentCharacterState state)
        {
            PersistentCharacterVitalityState vitality = _session.Vitality.Get(state.Identity);
            PersistentCharacterProgressionState progression = _session.Progression.Get(state.Identity);
            PersistentCharacterDerivedState derived = _session.DerivedStats.Get(state.Identity);
            int[] basic = new int[CharacterSkillRules.BasicSkillCount];
            int[] technical = new int[CharacterSkillRules.TechnicalSkillCount];
            int[] purchased = new int[CharacterSkillRules.SkillCount];
            int[] training = new int[CharacterSkillRules.SkillCount];
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills)
            {
                if (CharacterSkillRules.IsTechnical(skill))
                    technical[CharacterSkillRules.SourceGroupIndex(skill)] = progression.Source.GetPacked(skill);
                else basic[CharacterSkillRules.SourceGroupIndex(skill)] = progression.Source.GetPacked(skill);
                purchased[(int)skill] = progression.GetPurchasedPoints(skill);
                training[(int)skill] = (int)progression.GetStoredTraining(skill);
            }
            int[] resistances = CharacterDerivedStatRules.AllResistances
                .Select(resistance => derived.Source.GetResistance(resistance)).ToArray();
            return new CharacterSaveData
            {
                Identity = state.Identity.Key,
                ObjectType = (int)state.ObjectType,
                PrototypeNumber = state.PrototypeNumber,
                Attributes = new CharacterAttributeSaveData
                {
                    BaseValues = state.BaseAttributes.ToArray(),
                    SourceRace = (int)state.SourceRace,
                    SourceGender = (int)state.SourceGender,
                    Race = (int)state.Race,
                    Gender = (int)state.Gender,
                    HasInstanceStatOverride = state.HasInstanceStatOverride,
                },
                Vitality = new CharacterVitalitySaveData
                {
                    SourceLevel = vitality.Source.Level,
                    HitPointPoints = vitality.Source.HitPointPoints,
                    HitPointAdjustment = vitality.Source.HitPointAdjustment,
                    SourceHitPointDamage = vitality.Source.HitPointDamage,
                    FatiguePoints = vitality.Source.FatiguePoints,
                    FatigueAdjustment = vitality.Source.FatigueAdjustment,
                    SourceFatigueDamage = vitality.Source.FatigueDamage,
                    HitPointDamage = vitality.HitPointDamage,
                    FatigueDamage = vitality.FatigueDamage,
                },
                Progression = new CharacterProgressionSaveData
                {
                    SourceLevel = progression.Source.Level,
                    SourceExperience = progression.Source.Experience,
                    SourceUnspentPoints = progression.Source.UnspentCharacterPoints,
                    SourceBasicPacked = basic,
                    SourceTechnicalPacked = technical,
                    IsMonstrous = progression.Source.IsMonstrous,
                    HasInstanceStatOverride = progression.Source.HasInstanceStatOverride,
                    HasInstanceBasicSkillOverride = progression.Source.HasInstanceBasicSkillOverride,
                    HasInstanceTechnicalSkillOverride = progression.Source.HasInstanceTechnicalSkillOverride,
                    Experience = progression.Experience,
                    Level = progression.Level,
                    UnspentPoints = progression.UnspentCharacterPoints,
                    PurchasedPoints = purchased,
                    Training = training,
                },
                Derived = new CharacterDerivedSaveData
                {
                    BaseArmorClass = derived.Source.BaseArmorClass,
                    Resistances = resistances,
                    SourceAlignment = derived.Source.Alignment,
                    Alignment = derived.Alignment,
                    MagickPoints = derived.Source.MagickPoints,
                    TechPoints = derived.Source.TechPoints,
                    TechnologyPointAdjustment = derived.TechnologyPointAdjustment,
                    ReactionBase = derived.Source.ReactionBase,
                    IsAloof = derived.Source.IsAloof,
                    IsMonstrous = derived.Source.IsMonstrous,
                    HasInstanceStatOverride = derived.Source.HasInstanceStatOverride,
                    HasInstanceArmorClassOverride = derived.Source.HasInstanceArmorClassOverride,
                    HasInstanceResistanceOverride = derived.Source.HasInstanceResistanceOverride,
                    HasInstanceReactionOverride = derived.Source.HasInstanceReactionOverride,
                },
            };
        }

        internal SessionLoadResult TryBuildPlan(string json, out SessionRestorePlan plan)
        {
            plan = null;
            SessionLoadResult migration = SessionSaveMigrator.TryMigrateToCurrent(json, out SessionSaveData data);
            if (!migration.Succeeded) return migration;
            if (data.World?.Player == null || data.World.Tombstones == null || data.Objects == null
                || data.Characters == null || data.Campaign == null)
                return Failure(SessionLoadFailure.MissingRequiredField, "A required V1 domain is missing.");

            SessionLoadResult result = BuildWorld(data, out plan);
            if (!result.Succeeded) return result;
            result = BuildCharacters(data.Characters, plan);
            if (!result.Succeeded) { plan = null; return result; }
            result = BuildMagic(data.Magic, plan);
            if (!result.Succeeded) { plan = null; return result; }
            result = BuildTechnology(data.Technology, plan);
            if (!result.Succeeded) { plan = null; return result; }
            result = BuildEconomy(data.Economy, plan);
            if (!result.Succeeded) { plan = null; return result; }
            result = BuildSocial(data.Social, plan);
            if (!result.Succeeded) { plan = null; return result; }
            result = BuildCampaign(data.Campaign, plan);
            if (!result.Succeeded) { plan = null; return result; }
            result = BuildParty(data.Party, plan);
            if (!result.Succeeded) { plan = null; return result; }
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildEconomy(EconomySaveData data, SessionRestorePlan plan)
        {
            // V1 saves written before M11A legitimately have no economy domain.
            if (data == null)
            {
                plan.Economy = null;
                return new SessionLoadResult(SessionLoadFailure.None);
            }
            if (data.Merchants == null)
                return Failure(SessionLoadFailure.InvalidObject, "The economy state is incomplete.");
            var merchants = new HashSet<ArcanumObjectId>();
            foreach (EconomyMerchantSaveData value in data.Merchants)
            {
                if (value == null || !TryIdentity(value.MerchantIdentity, out ArcanumObjectId merchant)
                    || !TryIdentity(value.InventoryOwnerIdentity, out ArcanumObjectId owner)
                    || !merchants.Add(merchant) || value.InventorySourceId < 1
                    || value.NextRestockAtMilliseconds < 0 || value.GeneratedIdentities == null
                    || !plan.Objects.TryGetValue(merchant, out PersistentObjectState merchantState)
                    || merchantState.Type != ObjectType.Npc
                    || !plan.Objects.TryGetValue(owner, out PersistentObjectState ownerState)
                    || ownerState.Type is not (ObjectType.Npc or ObjectType.Container))
                    return Failure(SessionLoadFailure.InvalidObject,
                        $"Economy merchant '{value?.MerchantIdentity}' is invalid.");
                var generated = new HashSet<ArcanumObjectId>();
                foreach (string key in value.GeneratedIdentities)
                    if (!TryIdentity(key, out ArcanumObjectId identity) || !generated.Add(identity)
                        || !plan.Objects.TryGetValue(identity, out PersistentObjectState item)
                        || item.ParentIdentity != owner)
                        return Failure(SessionLoadFailure.InvalidObject,
                            $"Economy generated object '{key}' is invalid.");
            }
            plan.Economy = data;
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildSocial(SocialSaveData data, SessionRestorePlan plan)
        {
            // Earlier V1 saves legitimately have no social domain.
            if (data == null) { plan.Social = null; return new SessionLoadResult(SessionLoadFailure.None); }
            if (data.Reputations == null || data.Hostilities == null)
                return Failure(SessionLoadFailure.InvalidCampaign, "The social state is incomplete.");
            var reputations = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialReputationSaveData value in data.Reputations)
            {
                if (value == null || !TryIdentity(value.PcIdentity, out ArcanumObjectId pc)
                    || pc != plan.Player.Identity || value.ReputationId < ReputationId.Minimum
                    || value.ReputationId > ReputationId.Maximum || value.AcquiredAtMilliseconds < 0
                    || !reputations.Add(pc.Key + ":" + value.ReputationId))
                    return Failure(SessionLoadFailure.InvalidCampaign, "A social reputation is invalid.");
            }
            var hostilities = new HashSet<string>(StringComparer.Ordinal);
            foreach (SocialHostilitySaveData value in data.Hostilities)
            {
                if (value == null || !TryIdentity(value.SourceIdentity, out ArcanumObjectId source)
                    || !TryIdentity(value.TargetIdentity, out ArcanumObjectId target)
                    || source == target || target != plan.Player.Identity
                    || !plan.Objects.TryGetValue(source, out PersistentObjectState npc)
                    || npc.Type != ObjectType.Npc || !hostilities.Add(source.Key + ":" + target.Key))
                    return Failure(SessionLoadFailure.InvalidCampaign, "A social hostility is invalid.");
            }
            plan.Social = data;
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildMagic(MagicSaveData data, SessionRestorePlan plan)
        {
            // V1 saves written before M10A legitimately have no magic domain.
            if (data == null)
            {
                plan.Magic = null;
                return new SessionLoadResult(SessionLoadFailure.None);
            }
            if (data.ElapsedMilliseconds < 0 || data.NextEffectId < 1
                || data.Characters == null || data.ActiveEffects == null)
                return Failure(SessionLoadFailure.InvalidCharacter, "The magic state is incomplete or invalid.");

            var characters = new HashSet<ArcanumObjectId>();
            foreach (MagicCharacterSaveData value in data.Characters)
            {
                if (value == null || !TryIdentity(value.Identity, out ArcanumObjectId identity)
                    || !plan.Characters.TryGet(identity, out _) || !characters.Add(identity)
                    || value.CollegeRanks?.Length != 16 || value.CollegeRanks.Any(rank => rank < 0 || rank > 5)
                    || value.MasteryCollege < -1 || value.MasteryCollege >= 16)
                    return Failure(SessionLoadFailure.InvalidCharacter,
                        $"Magic knowledge for '{value?.Identity}' is invalid.");
            }

            var effectIds = new HashSet<long>();
            var spellTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (ActiveSpellEffectSaveData value in data.ActiveEffects)
            {
                if (value == null || value.Id < 1 || value.Id >= data.NextEffectId || !effectIds.Add(value.Id)
                    || !PhaseOneSpellCatalog.TryGet(value.SpellId, out SpellDefinition spell)
                    || (!spell.Maintained && spell.DurationSourceMilliseconds <= 0)
                    || spell.EffectFamily is not (SpellEffectFamily.AttributeModifier or SpellEffectFamily.CritterFlag)
                    || !TryIdentity(value.CasterIdentity, out ArcanumObjectId caster)
                    || !TryIdentity(value.TargetIdentity, out ArcanumObjectId target)
                    || !plan.Characters.TryGet(caster, out _) || !plan.Characters.TryGet(target, out _)
                    || !plan.Vitality.TryGet(caster, out _) || !plan.Vitality.TryGet(target, out _)
                    || value.Magnitude != spell.AttributeMagnitude || value.StartedAtMilliseconds < 0
                    || value.StartedAtMilliseconds > data.ElapsedMilliseconds
                    || spell.Maintained && value.NextUpkeepAtMilliseconds <= value.StartedAtMilliseconds
                    || !spell.Maintained && value.NextUpkeepAtMilliseconds != 0
                    || spell.DurationSourceMilliseconds > 0
                       && (value.ExpiresAtMilliseconds <= value.StartedAtMilliseconds
                           || value.ExpiresAtMilliseconds - value.StartedAtMilliseconds
                           != spell.DurationSourceMilliseconds)
                    || spell.DurationSourceMilliseconds == 0 && value.ExpiresAtMilliseconds != 0
                    || !spellTargets.Add(value.SpellId + ":" + target.Key))
                    return Failure(SessionLoadFailure.InvalidCharacter,
                        $"Active magic effect '{value?.Id}' is invalid.");
            }
            plan.Magic = data;
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildTechnology(TechnologySaveData data, SessionRestorePlan plan)
        {
            // Earlier V1 saves legitimately have no technology domain.
            if (data == null) { plan.Technology = null; return new SessionLoadResult(SessionLoadFailure.None); }
            if (data.Characters == null)
                return Failure(SessionLoadFailure.InvalidCharacter, "The technology state is incomplete.");
            var characters = new HashSet<ArcanumObjectId>();
            foreach (TechnologyCharacterSaveData value in data.Characters)
                if (value == null || !TryIdentity(value.Identity, out ArcanumObjectId identity)
                    || !plan.Characters.TryGet(identity, out _) || !characters.Add(identity)
                    || value.DisciplineRanks?.Length != 8
                    || value.DisciplineRanks.Any(rank => rank < 0 || rank > 7))
                    return Failure(SessionLoadFailure.InvalidCharacter,
                        $"Technology knowledge for '{value?.Identity}' is invalid.");
            plan.Technology = data;
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildParty(PartySaveData data, SessionRestorePlan plan)
        {
            if (data == null)
            {
                plan.PartyMembers = new List<PartyMember>();
                return new SessionLoadResult(SessionLoadFailure.None);
            }
            if (!TryIdentity(data.LeaderIdentity, out ArcanumObjectId leader)
                || leader != plan.Player.Identity || data.Members == null)
                return Failure(SessionLoadFailure.InvalidParty, "The party leader or member list is invalid.");

            // Validation must use the restored roots without mutating the active session.
            var members = new List<PartyMember>();
            var seen = new HashSet<ArcanumObjectId>();
            int ordinaryCount = 0;
            foreach (PartyMemberSaveData value in data.Members)
            {
                if (value == null || !TryIdentity(value.Identity, out ArcanumObjectId identity)
                    || !plan.Objects.TryGetValue(identity, out PersistentObjectState state)
                    || state.Type != ObjectType.Npc || state.Off
                    || !plan.Characters.TryGet(identity, out _) || !plan.Vitality.TryGet(identity, out _)
                    || !seen.Add(identity))
                    return Failure(SessionLoadFailure.InvalidParty,
                        $"Party member '{value?.Identity}' is not a valid persistent NPC.");
                members.Add(new PartyMember(identity, value.Forced));
                if (!value.Forced) ordinaryCount++;
            }
            int capacity = plan.DerivedStats.GetDerivedStat(plan.Player.Identity,
                CharacterDerivedStat.MaximumFollowers);
            if (ordinaryCount > capacity)
                return Failure(SessionLoadFailure.InvalidParty,
                    $"The saved party has {ordinaryCount} ordinary followers but capacity is {capacity}.");
            plan.PartyMembers = members;
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildWorld(SessionSaveData data, out SessionRestorePlan plan)
        {
            plan = null;
            WorldSaveData world = data.World;
            string sector = WorldMapSessionCoordinator.NormalizeSector(world.SelectedSector);
            if (sector == null || !SectorCoordinate.TryParse(sector, out SectorCoordinate selected)
                || !string.Equals(world.CurrentMap, selected.MapPath, StringComparison.Ordinal)
                || world.NextDynamicIdentity == 0)
                return Failure(SessionLoadFailure.InvalidWorld, "The selected map/sector or dynamic allocator is invalid.");
            PlayerSaveData playerData = world.Player;
            if (!TryIdentity(playerData.Identity, out ArcanumObjectId playerIdentity)
                || playerIdentity != ProductionPlayerLifecycle.DefaultPlayerIdentity
                || !Finite(playerData.MapX) || !Finite(playerData.MapY)
                || !string.Equals(playerData.MapPath, selected.MapPath, StringComparison.Ordinal))
                return Failure(SessionLoadFailure.InvalidWorld, "The production player identity or map position is invalid.");
            var mapPosition = new Vector2(playerData.MapX, playerData.MapY);
            if (SectorCoordinate.FromGlobal(playerData.MapPath, mapPosition).Path != sector)
                return Failure(SessionLoadFailure.InvalidWorld, "The PC map-global position does not belong to the selected sector.");

            var objects = new Dictionary<ArcanumObjectId, PersistentObjectState>();
            ulong maximumDynamic = 0;
            foreach (ObjectSaveData value in data.Objects)
            {
                SessionLoadResult objectResult = TryRestoreObject(value, out PersistentObjectState state);
                if (!objectResult.Succeeded) return objectResult;
                if (state.Identity == playerIdentity || !objects.TryAdd(state.Identity, state))
                    return Failure(SessionLoadFailure.DuplicateIdentity, $"Duplicate ObjectID {state.Identity}.");
                if (state.Identity.TryGetSessionDynamicSequence(out ulong sequence))
                    maximumDynamic = Math.Max(maximumDynamic, sequence);
            }

            var tombstones = new HashSet<ArcanumObjectId>();
            foreach (string key in world.Tombstones)
            {
                if (!TryIdentity(key, out ArcanumObjectId identity))
                    return Failure(SessionLoadFailure.InvalidIdentity, $"Invalid tombstone ObjectID '{key}'.");
                if (identity == playerIdentity || objects.ContainsKey(identity) || !tombstones.Add(identity))
                    return Failure(SessionLoadFailure.DuplicateIdentity, $"Conflicting tombstone {identity}.");
                if (identity.TryGetSessionDynamicSequence(out ulong sequence))
                    maximumDynamic = Math.Max(maximumDynamic, sequence);
            }
            if (world.NextDynamicIdentity <= maximumDynamic)
                return Failure(SessionLoadFailure.InvalidWorld, "The dynamic allocator would collide with restored identities.");

            SessionLoadResult references = ValidateObjectRelationships(objects, playerIdentity, tombstones);
            if (!references.Succeeded) return references;
            plan = new SessionRestorePlan
            {
                SelectedSector = sector,
                NextDynamicIdentity = world.NextDynamicIdentity,
                Player = new PersistentPlayerState(playerIdentity, sector, selected.ToLocal(mapPosition), playerData.ArtId),
                Objects = objects,
                Tombstones = tombstones,
            };
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult TryRestoreObject(ObjectSaveData value, out PersistentObjectState state)
        {
            state = null;
            if (value?.Placement == null)
                return Failure(SessionLoadFailure.MissingRequiredField, "An object or its placement is missing.");
            if (!TryIdentity(value.Identity, out ArcanumObjectId identity))
                return Failure(SessionLoadFailure.InvalidIdentity, $"Invalid object ObjectID '{value.Identity}'.");
            if (!Enum.IsDefined(typeof(ObjectType), value.Type) || !Finite(value.TileX) || !Finite(value.TileY)
                || value.FootprintWidth < 1 || value.FootprintHeight < 1 || value.SourceWorth < 0
                || value.MaximumHitPoints < 0 || value.HitPointDamage < 0 || value.InventorySourceId < 0)
                return Failure(SessionLoadFailure.InvalidObject, $"Object {identity} contains invalid source values.");
            ObjectType type = (ObjectType)value.Type;
            bool stackable = type is ObjectType.Ammo or ObjectType.Gold;
            if (stackable ? !value.StackQuantity.HasValue || value.StackQuantity.Value < 1
                    : value.StackQuantity.HasValue)
                return Failure(SessionLoadFailure.InvalidStack, $"Object {identity} has an invalid stack quantity.");
            if (value.RuntimeCreated != (identity.Type == ArcanumObjectIdType.SessionDynamic))
                return Failure(SessionLoadFailure.InvalidObject, $"Object {identity} has inconsistent dynamic identity state.");
            string sourceSector = WorldMapSessionCoordinator.NormalizeSector(value.SourceSector);
            if (sourceSector == null || !SectorCoordinate.TryParse(sourceSector, out _))
                return Failure(SessionLoadFailure.InvalidObject, $"Object {identity} has an invalid source sector.");
            if (!TryNullableIdentity(value.AuthoredParentIdentity, out ArcanumObjectId authoredParent))
                return Failure(SessionLoadFailure.InvalidIdentity, $"Object {identity} has an invalid authored parent.");
            if (!TryNullableIdentity(value.SubstituteInventoryIdentity, out ArcanumObjectId substituteInventory))
                return Failure(SessionLoadFailure.InvalidIdentity,
                    $"Object {identity} has an invalid substitute inventory.");
            SessionLoadResult placementResult = TryRestorePlacement(value.Placement, out ObjectPlacement placement);
            if (!placementResult.Succeeded) return placementResult;

            try
            {
                ObjectProtoInfo prototype = _session.ResolvePrototype(value.PrototypeNumber);
                state = new PersistentObjectState(identity, authoredParent, sourceSector, type, value.PrototypeNumber,
                    value.NameIndex, value.SocialClass, value.AiData, value.Origin, value.Faction,
                    value.SourceWorth, value.MaximumHitPoints,
                    value.HitPointDamage, value.RetailPriceMultiplier, value.InventorySourceId,
                    substituteInventory, value.NpcFlags, value.BuyObjectScriptNum, value.ContainerFlags,
                    value.AuthoredLocation, value.ArtId, value.Off, value.Locked,
                    value.UseScriptNum, value.DialogNum, value.ItemFlags, value.InventoryArtId, value.WeaponFlags,
                    value.GenericFlags, value.UnitWeight,
                    new InventoryFootprint(value.FootprintWidth, value.FootprintHeight), value.InventoryLocation,
                    value.StackQuantity, value.PortalOpen, new Vector2(value.TileX, value.TileY), placement,
                    value.RuntimeCreated,
                    prototype?.Weapon != null ? Combat.Weapon.FromFields(prototype.Weapon) : null,
                    prototype?.AmmoItemType, value.DeathConsequencesProcessed);
            }
            catch (Exception ex)
            {
                return Failure(SessionLoadFailure.InvalidObject, ex.Message);
            }
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private static SessionLoadResult TryRestorePlacement(PlacementSaveData value, out ObjectPlacement placement)
        {
            placement = default;
            if (!Enum.IsDefined(typeof(ObjectPlacementKind), value.Kind))
                return Failure(SessionLoadFailure.InvalidPlacement, "Unknown placement kind.");
            try
            {
                switch ((ObjectPlacementKind)value.Kind)
                {
                    case ObjectPlacementKind.World:
                    {
                        string sector = WorldMapSessionCoordinator.NormalizeSector(value.Sector);
                        if (sector == null || !SectorCoordinate.TryParse(sector, out _) || !Finite(value.TileX)
                            || !Finite(value.TileY))
                            return Failure(SessionLoadFailure.InvalidPlacement, "Invalid world placement.");
                        placement = ObjectPlacement.InWorld(sector, new Vector2(value.TileX, value.TileY));
                        break;
                    }
                    case ObjectPlacementKind.Contained:
                        if (!TryIdentity(value.ParentIdentity, out ArcanumObjectId containedParent))
                            return Failure(SessionLoadFailure.InvalidIdentity, "Invalid containment parent.");
                        placement = ObjectPlacement.ContainedBy(containedParent);
                        break;
                    case ObjectPlacementKind.Equipped:
                        if (!TryIdentity(value.ParentIdentity, out ArcanumObjectId equippedParent)
                            || !Enum.IsDefined(typeof(WornLocation), value.WornLocation)
                            || !WornLocations.IsValid((WornLocation)value.WornLocation))
                            return Failure(SessionLoadFailure.InvalidPlacement, "Invalid equipped placement.");
                        placement = ObjectPlacement.EquippedBy(equippedParent, (WornLocation)value.WornLocation);
                        break;
                }
            }
            catch (Exception ex)
            {
                return Failure(SessionLoadFailure.InvalidPlacement, ex.Message);
            }
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private static SessionLoadResult ValidateObjectRelationships(
            IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> objects, ArcanumObjectId player,
            HashSet<ArcanumObjectId> tombstones)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (PersistentObjectState state in objects.Values)
            {
                if (state.Placement.Kind == ObjectPlacementKind.World) continue;
                ArcanumObjectId parent = state.ParentIdentity;
                if (tombstones.Contains(parent))
                    return Failure(SessionLoadFailure.InvalidReference, $"Missing parent {parent} for {state.Identity}.");
                bool externalAuthoredParent = parent != player && !objects.ContainsKey(parent)
                    && !state.IsRuntimeCreated && state.AuthoredParentIdentity == parent;
                if (parent != player && !objects.ContainsKey(parent) && !externalAuthoredParent)
                    return Failure(SessionLoadFailure.InvalidReference, $"Missing parent {parent} for {state.Identity}.");
                if (parent == state.Identity)
                    return Failure(SessionLoadFailure.ContainmentCycle, $"Object {state.Identity} contains itself.");
                if (!externalAuthoredParent && parent != player
                    && !WorldMapSessionCoordinator.IsInventoryOwnerType(objects[parent].Type))
                    return Failure(SessionLoadFailure.InvalidReference, $"Parent {parent} cannot own inventory.");
                if (state.Placement.Kind == ObjectPlacementKind.Equipped)
                {
                    if (!externalAuthoredParent && parent != player && objects[parent].Type != ObjectType.Npc)
                        return Failure(SessionLoadFailure.InvalidPlacement, "Only critters can own equipped items.");
                    if (!WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out WornLocation natural)
                        || natural != WornLocation.Ring1 && natural != state.Placement.WornLocation
                        || natural == WornLocation.Ring1 && state.Placement.WornLocation is not WornLocation.Ring1 and not WornLocation.Ring2)
                        return Failure(SessionLoadFailure.InvalidPlacement,
                            $"Item {state.Identity} is incompatible with {state.Placement.WornLocation}.");
                    string slot = parent.Key + ":" + (int)state.Placement.WornLocation;
                    if (!occupied.Add(slot))
                        return Failure(SessionLoadFailure.InvalidPlacement, $"Duplicate equipped slot {slot}.");
                }
            }
            foreach (PersistentObjectState state in objects.Values)
            {
                var seen = new HashSet<ArcanumObjectId> { state.Identity };
                ArcanumObjectId current = state.ParentIdentity;
                while (current.IsPersistent && current != player && objects.TryGetValue(current, out PersistentObjectState parent))
                {
                    if (!seen.Add(current))
                        return Failure(SessionLoadFailure.ContainmentCycle,
                            $"Containment cycle includes {state.Identity}.");
                    current = parent.ParentIdentity;
                }
            }
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildCharacters(IReadOnlyList<CharacterSaveData> values, SessionRestorePlan plan)
        {
            var characters = new CharacterStatService();
            var seen = new HashSet<ArcanumObjectId>();
            try
            {
                foreach (CharacterSaveData value in values)
                {
                    if (value?.Attributes == null || value.Vitality == null || value.Progression == null
                        || value.Derived == null || !TryIdentity(value.Identity, out ArcanumObjectId identity)
                        || !seen.Add(identity))
                        return Failure(SessionLoadFailure.InvalidCharacter, "A character record is missing or duplicated.");
                    if (!Enum.IsDefined(typeof(ObjectType), value.ObjectType)
                        || (ObjectType)value.ObjectType is not ObjectType.Pc and not ObjectType.Npc)
                        return Failure(SessionLoadFailure.InvalidCharacter, $"Character {identity} has an invalid type.");
                    ObjectType objectType = (ObjectType)value.ObjectType;
                    if (objectType == ObjectType.Pc
                        ? identity != plan.Player.Identity || value.PrototypeNumber.HasValue
                        : !value.PrototypeNumber.HasValue || !plan.Objects.TryGetValue(identity, out PersistentObjectState npc)
                          || npc.Type != ObjectType.Npc || npc.PrototypeNumber != value.PrototypeNumber.Value)
                        return Failure(SessionLoadFailure.InvalidCharacter, $"Character {identity} has no matching owner.");
                    CharacterAttributeSaveData attributes = value.Attributes;
                    if (attributes.BaseValues?.Length != CharacterAttributeSet.Count
                        || !Enum.IsDefined(typeof(CharacterRace), attributes.SourceRace)
                        || !Enum.IsDefined(typeof(CharacterGender), attributes.SourceGender)
                        || !Enum.IsDefined(typeof(CharacterRace), attributes.Race)
                        || !Enum.IsDefined(typeof(CharacterGender), attributes.Gender))
                        return Failure(SessionLoadFailure.InvalidCharacter, $"Character {identity} attributes are invalid.");
                    var state = new PersistentCharacterState(identity, objectType, value.PrototypeNumber,
                        new CharacterAttributeSet(attributes.BaseValues, (CharacterRace)attributes.SourceRace),
                        (CharacterRace)attributes.SourceRace, (CharacterGender)attributes.SourceGender,
                        attributes.HasInstanceStatOverride);
                    state.SetRace((CharacterRace)attributes.Race);
                    state.SetGender((CharacterGender)attributes.Gender);
                    characters.AddRestored(state);
                }

                if (!seen.Contains(plan.Player.Identity))
                    return Failure(SessionLoadFailure.InvalidCharacter, "The production PC has no character state.");

                var progression = new CharacterProgressionService(characters);
                foreach (CharacterSaveData value in values)
                {
                    ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                    CharacterProgressionSaveData source = value.Progression;
                    if (source.SourceBasicPacked?.Length != CharacterSkillRules.BasicSkillCount
                        || source.SourceTechnicalPacked?.Length != CharacterSkillRules.TechnicalSkillCount
                        || source.PurchasedPoints?.Length != CharacterSkillRules.SkillCount
                        || source.Training?.Length != CharacterSkillRules.SkillCount
                        || source.Experience < 0 || source.Experience > CharacterProgressionService.MaximumExperience
                        || source.Level < CharacterVitalitySource.MinimumLevel
                        || source.Level > CharacterVitalitySource.MaximumLevel
                        || source.UnspentPoints < 0
                        || source.UnspentPoints > CharacterProgressionService.MaximumUnspentCharacterPoints)
                        return Failure(SessionLoadFailure.InvalidCharacter, $"Character {identity} progression is invalid.");
                    var progressionSource = new CharacterProgressionSource(source.SourceLevel,
                        source.SourceExperience, source.SourceUnspentPoints, source.SourceBasicPacked,
                        source.SourceTechnicalPacked, source.IsMonstrous, source.HasInstanceStatOverride,
                        source.HasInstanceBasicSkillOverride, source.HasInstanceTechnicalSkillOverride);
                    var progressionState = new PersistentCharacterProgressionState(identity,
                        (ObjectType)value.ObjectType, value.PrototypeNumber, progressionSource);
                    var training = new SkillTrainingLevel[CharacterSkillRules.SkillCount];
                    for (int index = 0; index < CharacterSkillRules.SkillCount; index++)
                    {
                        if (source.PurchasedPoints[index] < 0
                            || source.PurchasedPoints[index] > CharacterSkillRules.MaximumPurchasedPoints
                            || !Enum.IsDefined(typeof(SkillTrainingLevel), source.Training[index]))
                            return Failure(SessionLoadFailure.InvalidCharacter,
                                $"Character {identity} skill {index} is invalid.");
                        training[index] = (SkillTrainingLevel)source.Training[index];
                    }
                    progressionState.SetProgression(source.Experience, source.Level, source.UnspentPoints);
                    progressionState.RestoreSkills(source.PurchasedPoints, training);
                    progression.AddRestored(progressionState);
                }

                var vitality = new CharacterVitalityService(characters, progression);
                foreach (CharacterSaveData value in values)
                {
                    ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                    CharacterVitalitySaveData source = value.Vitality;
                    if (source.HitPointDamage < 0 || source.FatigueDamage < 0)
                        return Failure(SessionLoadFailure.InvalidCharacter, $"Character {identity} damage is invalid.");
                    var vitalitySource = new CharacterVitalitySource(source.SourceLevel, source.HitPointPoints,
                        source.HitPointAdjustment, source.SourceHitPointDamage, source.FatiguePoints,
                        source.FatigueAdjustment, source.SourceFatigueDamage);
                    PersistentCharacterVitalityState state = vitality.GetOrCreateRestored(identity,
                        (ObjectType)value.ObjectType, value.PrototypeNumber, vitalitySource);
                    state.SetDamage(source.HitPointDamage, source.FatigueDamage);
                }

                var capacity = new InventoryCapacityService(_session);
                var derived = new CharacterDerivedStatService(characters, progression, capacity);
                foreach (CharacterSaveData value in values)
                {
                    ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                    CharacterDerivedSaveData source = value.Derived;
                    if (source.Resistances?.Length != CharacterDerivedStatRules.ResistanceCount
                        || source.Alignment < CharacterDerivedStatRules.MinimumAlignment
                        || source.Alignment > CharacterDerivedStatRules.MaximumAlignment)
                        return Failure(SessionLoadFailure.InvalidCharacter, $"Character {identity} derived inputs are invalid.");
                    var derivedSource = new CharacterDerivedSource(source.BaseArmorClass, source.Resistances,
                        source.SourceAlignment, source.MagickPoints, source.TechPoints, source.ReactionBase,
                        source.IsAloof, source.IsMonstrous, source.HasInstanceStatOverride,
                        source.HasInstanceArmorClassOverride, source.HasInstanceResistanceOverride,
                        source.HasInstanceReactionOverride);
                    PersistentCharacterDerivedState state = derived.GetOrCreateRestored(identity,
                        (ObjectType)value.ObjectType, value.PrototypeNumber, derivedSource);
                    state.SetAlignment(source.Alignment);
                    if (source.TechnologyPointAdjustment < 0)
                        return Failure(SessionLoadFailure.InvalidCharacter,
                            $"Character {identity} technology point adjustment is invalid.");
                    state.SetTechnologyPointAdjustment(source.TechnologyPointAdjustment);
                }
                plan.Characters = characters;
                plan.Progression = progression;
                plan.Vitality = vitality;
                plan.InventoryCapacity = capacity;
                plan.DerivedStats = derived;
            }
            catch (Exception ex)
            {
                return Failure(SessionLoadFailure.InvalidCharacter, ex.Message);
            }
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private SessionLoadResult BuildCampaign(CampaignSaveData data, SessionRestorePlan plan)
        {
            if (data.GlobalVariables == null || data.GlobalFlags == null || data.PcVariables == null
                || data.PcFlags == null || data.Quests == null || data.Attachments == null || data.Reactions == null
                || data.KnownAreas == null
                || data.StoryState < 0)
                return Failure(SessionLoadFailure.InvalidCampaign, "A campaign collection is missing or invalid.");
            if (!ValidateIndexed(data.GlobalVariables, CampaignStateService.GlobalVariableCount)
                || !ValidateIndexes(data.GlobalFlags, CampaignStateService.GlobalFlagCount)
                || !ValidateIndexed(data.PcVariables, CampaignStateService.PcVariableCount)
                || !ValidateIndexes(data.PcFlags, CampaignStateService.PcFlagCount))
                return Failure(SessionLoadFailure.InvalidCampaign, "Campaign variables or flags are invalid.");

            if (data.KnownAreas.Count > 0 && _session.AreaSource == null)
                return Failure(SessionLoadFailure.InvalidCampaign, "Area source metadata is unavailable.");
            var knownAreas = new HashSet<int>();
            foreach (int value in data.KnownAreas)
                if (value <= 0 || !_session.AreaSource.TryGet(value, out _) || !knownAreas.Add(value))
                    return Failure(SessionLoadFailure.InvalidCampaign, $"Known area {value} is invalid.");

            var questNumbers = new HashSet<int>();
            ulong maximumTimestamp = 0;
            foreach (QuestSaveData quest in data.Quests)
            {
                if (!questNumbers.Add(quest.Number)
                    || (uint)(quest.Number - CampaignStateService.FirstQuestNumber) >= CampaignStateService.QuestCount
                    || !ValidRawQuestState(quest.PcState)
                    || !Enum.IsDefined(typeof(QuestState), quest.GlobalState)
                    || quest.TimestampMilliseconds >= 86400000u)
                    return Failure(SessionLoadFailure.InvalidCampaign, $"Quest {quest.Number} is invalid.");
                maximumTimestamp = Math.Max(maximumTimestamp,
                    (ulong)quest.TimestampDays * 86400000UL + quest.TimestampMilliseconds);
            }
            if (data.QuestClock < maximumTimestamp)
                return Failure(SessionLoadFailure.InvalidCampaign, "The quest clock precedes a saved timestamp.");

            var attachments = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScriptAttachmentSaveData attachment in data.Attachments)
            {
                if (!TryIdentity(attachment.Identity, out ArcanumObjectId identity)
                    || identity != plan.Player.Identity && !plan.Objects.ContainsKey(identity)
                       && !plan.Tombstones.Contains(identity)
                    || (uint)attachment.AttachmentPoint > (uint)Arcanum.Formats.Script.Sap.CriticalMiss
                    || !attachments.Add(identity.Key + ":" + attachment.AttachmentPoint))
                    return Failure(SessionLoadFailure.InvalidCampaign, "A script attachment is invalid.");
            }
            var reactions = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReactionSaveData reaction in data.Reactions)
            {
                if (!TryIdentity(reaction.NpcIdentity, out ArcanumObjectId npc)
                    || !TryIdentity(reaction.PcIdentity, out ArcanumObjectId pc)
                    || pc != plan.Player.Identity || !plan.Characters.TryGet(npc, out PersistentCharacterState npcState)
                    || npcState.ObjectType != ObjectType.Npc
                    || !reactions.Add(npc.Key + ":" + pc.Key))
                    return Failure(SessionLoadFailure.InvalidCampaign, "A reaction adjustment is invalid.");
                plan.DerivedStats.AddRestoredReactionAdjustment(npc, pc, reaction.Adjustment);
            }
            try
            {
                var campaign = new CampaignStateService();
                if (_session.AreaSource != null)
                    campaign.BindAreaSource(_session.AreaSource);
                campaign.RestoreSaveData(data);
                plan.Campaign = campaign;
            }
            catch (Exception ex)
            {
                return Failure(SessionLoadFailure.InvalidCampaign, ex.Message);
            }
            return new SessionLoadResult(SessionLoadFailure.None);
        }

        private static bool ValidateIndexed(IReadOnlyList<IndexedIntSaveData> values, int count)
        {
            var seen = new HashSet<int>();
            foreach (IndexedIntSaveData value in values)
                if (value == null || (uint)value.Index >= count || value.Value == 0 || !seen.Add(value.Index))
                    return false;
            return true;
        }

        private static bool ValidateIndexes(IReadOnlyList<int> values, int count)
        {
            var seen = new HashSet<int>();
            foreach (int value in values)
                if ((uint)value >= count || !seen.Add(value)) return false;
            return true;
        }

        private static bool ValidRawQuestState(int value)
        {
            int allowed = QuestLog.BotchedModifier | ((1 << 8) - 1);
            if ((value & ~allowed) != 0) return false;
            int state = value & ~QuestLog.BotchedModifier;
            return (uint)state <= (uint)QuestState.Botched;
        }

        private static bool TryIdentity(string key, out ArcanumObjectId identity)
            => ArcanumObjectId.TryParsePersistent(key, out identity);

        private static bool TryNullableIdentity(string key, out ArcanumObjectId identity)
        {
            if (key == null)
            {
                identity = default;
                return true;
            }
            return TryIdentity(key, out identity);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static SessionLoadResult Failure(SessionLoadFailure failure, string message)
            => new(failure, message);
    }
}
