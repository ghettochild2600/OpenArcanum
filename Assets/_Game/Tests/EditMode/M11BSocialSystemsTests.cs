using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Social;
using Arcanum.Runtime.World;
using Arcanum.Script;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M11BSocialSystems")]
    public sealed class M11BSocialSystemsTests
    {
        private const string Sector = "maps/test/1.sec";
        private const int MerchandisePrototype = 6021;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _npc;
        private ArcanumObjectId _ally;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(M11BSocialSystemsTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1)));
            _npc = AddNpc("G_11111111_1111_1111_1111_111111111111", 1, 16, 7, 50, 1);
            _ally = AddNpc("G_22222222_2222_2222_2222_222222222222", 2, 0, 7, 50, 1);
            _session.BindSocialSources(Reputations(), Ai());
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticGamerepParsesGlobalFactionAndMembershipRecords()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) Assert.Ignore("The local clean Arcanum module archive is unavailable.");
            using var vfs = new DatVirtualFileSystem();
            vfs.MountFile(module);
            ReputationCatalog catalog = ReputationCatalog.FromMes(
                MesReader.Read(vfs.ReadAllBytes("rules/gamerep.mes")));
            Assert.That(catalog.TryGet(new ReputationId(1000), out ReputationDefinition global), Is.True);
            Assert.That(global.Effects.Single().ReactionAdjustment, Is.EqualTo(10));
            Assert.That(global.Effects.Single().AppliesTo(99, 99), Is.True);
            Assert.That(catalog.TryGet(new ReputationId(1024), out ReputationDefinition faction), Is.True);
            Assert.That(faction.GrantsFaction(7), Is.True);
            Assert.That(faction.Effects.Single().ReactionAdjustment, Is.EqualTo(100));
        }

        [Test]
        public void ReactionBreakdownKeepsSourceDerivedPersistentAndReputationTermsSeparate()
        {
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1000));
            _session.Social.AdjustReaction(_npc, _pc.Identity, 5);
            SocialReactionBreakdown value = _session.Social.GetReactionBreakdown(_npc, _pc.Identity);
            Assert.That((value.SourceBase, value.BeautyModifier, value.RaceModifier,
                    value.PersistentModifier, value.ReputationModifier, value.EffectiveReaction),
                Is.EqualTo((50, -7, 0, 5, 10, 58)));
        }

        [Test]
        public void DynamicCharacterInputsRecomputeWithoutBakingOrDoubleApplyingReputation()
        {
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1000));
            _session.Social.SetReaction(_npc, _pc.Identity, 70);
            Assert.That(_session.Social.GetReaction(_npc, _pc.Identity), Is.EqualTo(70));
            _session.Characters.SetRace(_pc.Identity, CharacterRace.HalfOrc);
            Assert.That(_session.Social.GetReaction(_npc, _pc.Identity), Is.EqualTo(54));
            _session.Characters.SetRace(_pc.Identity, CharacterRace.Human);
            Assert.That(_session.Social.GetReaction(_npc, _pc.Identity), Is.EqualTo(70));
        }

        [TestCase(-100, ReactionDisposition.Hatred)]
        [TestCase(0, ReactionDisposition.Hatred)]
        [TestCase(1, ReactionDisposition.Dislike)]
        [TestCase(20, ReactionDisposition.Dislike)]
        [TestCase(21, ReactionDisposition.Suspicious)]
        [TestCase(40, ReactionDisposition.Suspicious)]
        [TestCase(41, ReactionDisposition.Neutral)]
        [TestCase(60, ReactionDisposition.Neutral)]
        [TestCase(61, ReactionDisposition.Courteous)]
        [TestCase(80, ReactionDisposition.Courteous)]
        [TestCase(81, ReactionDisposition.Amiable)]
        [TestCase(100, ReactionDisposition.Amiable)]
        [TestCase(101, ReactionDisposition.Love)]
        [TestCase(1000, ReactionDisposition.Love)]
        public void DispositionUsesExactUnboundedSourceThresholds(int value, ReactionDisposition expected)
            => Assert.That(SocialStateService.TranslateReaction(value), Is.EqualTo(expected));

        [Test]
        public void ReputationsAreTypedBooleanOrderedRecordsWithIdempotentRemoval()
        {
            _session.SourceTime.Advance(10);
            Assert.That(_session.Social.AddReputation(_pc.Identity, new ReputationId(1002)), Is.True);
            _session.SourceTime.Advance(10);
            Assert.That(_session.Social.AddReputation(_pc.Identity, new ReputationId(1000)), Is.True);
            Assert.That(_session.Social.AddReputation(_pc.Identity, new ReputationId(1000)), Is.False);
            Assert.That(_session.Social.GetReputations(_pc.Identity).Select(value => value.Value),
                Is.EqualTo(new[] { 1002, 1000 }));
            Assert.That(_session.Social.RemoveReputation(_pc.Identity, new ReputationId(1002)), Is.True);
            Assert.That(_session.Social.RemoveReputation(_pc.Identity, new ReputationId(1002)), Is.False);
            Assert.That(_session.Social.HasReputation(_pc.Identity, new ReputationId(1000)), Is.True);
        }

        [Test]
        public void ReputationEffectsFilterByOriginAndFactionAndStackAcrossRecords()
        {
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1000));
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1001));
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1002));
            Assert.That(_session.Social.GetReactionBreakdown(_npc, _pc.Identity).ReputationModifier,
                Is.EqualTo(45));
            Assert.That(_session.Social.GetReactionBreakdown(_ally, _pc.Identity).ReputationModifier,
                Is.EqualTo(25));
        }

        [Test]
        public void FactionMembershipAndNpcEqualityProduceSourceAllies()
        {
            Assert.That(_session.Social.AreAllies(_npc, _ally), Is.True);
            Assert.That(_session.Social.AreAllies(_pc.Identity, _npc), Is.False);
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1024));
            Assert.That(_session.Social.ReputationGrantsFaction(_pc.Identity, 7), Is.True);
            Assert.That(_session.Social.AreAllies(_pc.Identity, _npc), Is.True);
        }

        [Test]
        public void PartyMembershipOverridesRememberedHostility()
        {
            Assert.That(_session.Social.SetHostile(_npc, _pc.Identity), Is.True);
            Assert.That(_session.Social.IsRememberedHostile(_npc, _pc.Identity), Is.True);
            Assert.That(_session.Party.Join(_npc).Succeeded, Is.True);
            Assert.That(_session.Social.AreAllies(_npc, _pc.Identity), Is.True);
            Assert.That(_session.Social.IsRememberedHostile(_npc, _pc.Identity), Is.False);
        }

        [Test]
        public void SourceAiThresholdsUseEffectiveReactionAndAlignmentDifference()
        {
            Assert.That(_session.Social.IsReactionHostile(_npc, _pc.Identity), Is.False);
            _session.Social.SetReaction(_npc, _pc.Identity, 40);
            Assert.That(_session.Social.IsReactionHostile(_npc, _pc.Identity), Is.True);
            _session.Social.SetReaction(_npc, _pc.Identity, 41);
            _session.DerivedStats.SetAlignment(_pc.Identity, -500);
            _session.DerivedStats.SetAlignment(_npc, 0);
            Assert.That(_session.Social.IsAlignmentHostile(_npc, _pc.Identity), Is.True);
        }

        [Test]
        public void CombatAiConsumesRememberedHostilityButSocialDoesNotIssueCombatActions()
        {
            RegisterCombatSource(_pc.Identity, ObjectType.Pc, 0, 0);
            RegisterCombatSource(_npc, ObjectType.Npc, 0, 0);
            Assert.That(_session.Combat.AreOpponents(_npc, _pc.Identity), Is.False);
            _session.Social.SetHostile(_npc, _pc.Identity);
            Assert.That(_session.Combat.AreOpponents(_npc, _pc.Identity), Is.True);
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        [Test]
        public void TheftBoundaryRejectsLegalUndetectedAndInvalidWitnessCasesWithoutHostility()
        {
            PersistentObjectState owned = Item(10, _npc);
            PersistentObjectState legal = Item(11, _pc.Identity);
            Assert.That(_session.Social.ReportTheft(_pc.Identity, _npc, legal.Identity, _npc, true).Failure,
                Is.EqualTo(SocialCrimeFailure.LegalAction));
            Assert.That(_session.Social.ReportTheft(_pc.Identity, _npc, owned.Identity, _npc, false).Failure,
                Is.EqualTo(SocialCrimeFailure.Undetected));
            Assert.That(_session.Social.ReportTheft(_pc.Identity, _npc, owned.Identity,
                ArcanumObjectId.CreateAuthored(999), true).Failure, Is.EqualTo(SocialCrimeFailure.InvalidWitness));
            Assert.That(_session.Social.IsRememberedHostile(_npc, _pc.Identity), Is.False);
        }

        [Test]
        public void DetectedTheftCreatesDirectionalHostilityExactlyOnce()
        {
            PersistentObjectState owned = Item(12, _npc);
            SocialCrimeResult first = _session.Social.ReportTheft(
                _pc.Identity, _npc, owned.Identity, _npc, true);
            SocialCrimeResult second = _session.Social.ReportTheft(
                _pc.Identity, _npc, owned.Identity, _npc, true);
            Assert.That((first.Succeeded, first.Witness, first.HostilityChanged),
                Is.EqualTo((true, _npc, true)));
            Assert.That((second.Succeeded, second.HostilityChanged), Is.EqualTo((true, false)));
            Assert.That(_session.Social.IsRememberedHostile(_npc, _pc.Identity), Is.True);
            Assert.That(_session.Social.IsRememberedHostile(_pc.Identity, _npc), Is.False);
        }

        [Test]
        public void DialogueReactionAndReputationEffectsUseAuthoritativeSocialState()
        {
            DialogScript dialogue = new(new SortedDictionary<int, DialogLine>
            {
                [1] = new(1, "Social?", "", 0, "", 0, ""),
                [2] = new(2, "Yes", "", 1, "rp -1000", 0, "re+5 rp 1000"),
            });
            _session.BindDialogueSource(_ => StartAt(1), _ => dialogue);
            int before = _session.Social.GetReaction(_npc, _pc.Identity);
            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.AvailableResponses, Has.Count.EqualTo(1));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Completed));
            Assert.That(_session.Social.HasReputation(_pc.Identity, new ReputationId(1000)), Is.True);
            Assert.That(_session.Social.GetReaction(_npc, _pc.Identity), Is.EqualTo(before + 15));
        }

        [Test]
        public void UnsupportedDialogueTailRollsBackReactionAndReputationTogether()
        {
            DialogScript dialogue = new(new SortedDictionary<int, DialogLine>
            {
                [1] = new(1, "Social?", "", 0, "", 0, ""),
                [2] = new(2, "Broken", "", 1, "", 999, "re+5 rp 1000"),
            });
            _session.BindDialogueSource(_ => StartAt(1), _ => dialogue);
            int before = _session.Social.GetReaction(_npc, _pc.Identity);
            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.ExecutionFailed));
            Assert.That(_session.Social.GetReaction(_npc, _pc.Identity), Is.EqualTo(before));
            Assert.That(_session.Social.HasReputation(_pc.Identity, new ReputationId(1000)), Is.False);
        }

        [Test]
        public void ReputationReactionFeedsM11APriceWithoutDuplicatingPricingRules()
        {
            _session.BindEconomySource(InventorySourceCatalog.FromMes(
                MesReader.Read("{1}{General:100 100,100 6001}"), MesReader.Read("{1}{6001}")));
            var prototype = new ObjectProtoInfo(MerchandisePrototype, ObjectType.Food, 0x50000000u,
                worth: 100) { ItemComplexity = 0 };
            _session.BindPrototypeSource(number => number == MerchandisePrototype ? prototype : null);
            PersistentObjectState item = Item(13, _npc, MerchandisePrototype, 100);
            int before = _session.Economy.GetPrice(EconomyTransactionDirection.BuyFromMerchant,
                _npc, _pc.Identity, item.Identity).UnitPrice;
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1000));
            EconomyPriceBreakdown after = _session.Economy.GetPrice(EconomyTransactionDirection.BuyFromMerchant,
                _npc, _pc.Identity, item.Identity);
            Assert.That(after.Reaction, Is.EqualTo(53));
            Assert.That(after.UnitPrice, Is.LessThan(before));
        }

        [Test]
        public void SaveV1RestoresSocialStateAndObjectInputsWithoutDoubleApplyingDerivedTerms()
        {
            _session.Social.AddReputation(_pc.Identity, new ReputationId(1000));
            _session.Social.AdjustReaction(_npc, _pc.Identity, 7);
            _session.Social.SetHostile(_npc, _pc.Identity);
            int reaction = _session.Social.GetReaction(_npc, _pc.Identity);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Social.HasReputation(_session.PlayerState.Identity, new ReputationId(1000)), Is.True);
            Assert.That(_session.Social.IsRememberedHostile(_npc, _session.PlayerState.Identity), Is.True);
            Assert.That(_session.Social.GetReaction(_npc, _session.PlayerState.Identity), Is.EqualTo(reaction));
            PersistentObjectState restored = _session.States[_npc];
            Assert.That((restored.AiData, restored.Origin, restored.Faction), Is.EqualTo((1, 16, 7)));
        }

        [Test]
        public void EarlierV1WithoutSocialDomainLoadsAsEmptySocialState()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Social = null;
            SessionLoadResult result = _session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_session.Social.GetReputations(_session.PlayerState.Identity), Is.Empty);
        }

        [Test]
        public void InvalidSocialSaveFailsBeforeMutatingLiveSession()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Social.Hostilities.Add(new SocialHostilitySaveData
            {
                SourceIdentity = _npc.Key,
                TargetIdentity = _npc.Key,
            });
            SessionLoadResult result = _session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data));
            Assert.That(result.Failure, Is.EqualTo(SessionLoadFailure.InvalidCampaign));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(_pc.Identity));
        }

        private ArcanumObjectId AddNpc(string key, int sourceOrder, int origin, int faction,
            int reaction, int aiData)
        {
            ArcanumObjectId identity = Parse(key);
            var source = new ObjectInstance(ObjectType.Npc, 28001, Location(sourceOrder + 1, 1),
                0x28100000u, 0, 0, oid: GuidBytes(key), dialogNum: 1);
            PersistentObjectState state = _session.GetOrCreate(source, identity, Sector,
                source.CurrentArtId.Value, false, false, retailPriceMultiplier: 150,
                inventorySourceId: 1, aiData: aiData, origin: origin, faction: faction);
            int[] stats = Stats();
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, reaction, 0, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            _session.Bind(Sector, state, Runtime("NPC-" + sourceOrder, ObjectType.Npc,
                new Vector2Int(sourceOrder + 1, 1)));
            return identity;
        }

        private PersistentObjectState Item(uint value, ArcanumObjectId owner,
            int prototype = MerchandisePrototype, int worth = 100)
        {
            ArcanumObjectId identity = ArcanumObjectId.CreateAuthored(value);
            var source = new ObjectInstance(ObjectType.Food, prototype, null, 0x50000000u, 0, 0,
                parentOid: Bytes(owner), worth: worth);
            return _session.GetOrCreate(source, identity, Sector, source.CurrentArtId.Value, false, false,
                itemFlags: EconomyStateService.ItemFlagIdentified, sourceWorth: worth);
        }

        private void RegisterCombatSource(ArcanumObjectId identity, ObjectType type, int npcFlags,
            int willKosScript)
        {
            if (_session.Combat.TryGetActorSource(identity, out _)) return;
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, type, 28001,
                Sector, type == ObjectType.Pc ? 0 : 1, npcFlags, 0, willKosScript, new int[10]));
        }

        private WorldObject Runtime(string name, ObjectType type, Vector2Int tile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.ArtId = 0x28100000u;
            runtime.Tile = tile;
            runtime.TilePosition = tile;
            return runtime;
        }

        private static ReputationCatalog Reputations() => ReputationCatalog.FromMes(MesReader.Read(
            "{1000}{0 0 0, 10 0 0}\n" +
            "{1001}{0 0 0, 15 0 7}\n" +
            "{1002}{0 0 0, 20 16 0, -100 0 13}\n" +
            "{1024}{7 0 0, 100 0 7}"));

        private static SocialAiCatalog Ai() => SocialAiCatalog.FromMes(MesReader.Read(
            "{1}{0 0 0 0 0 0 0 0 0 0 40 500}"));

        private static ScriptFile StartAt(int line)
        {
            var result = new ScriptFile();
            result.Entries.Add(Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, line)), Nop()));
            return result;
        }

        private static ScriptCondition Cond(Sct type, ScriptAction action, ScriptAction els)
            => new() { Type = (int)type, Action = action, Els = els };

        private static ScriptAction Act(Sat type, params (Svt type, int value)[] values)
        {
            var result = new ScriptAction { Type = (int)type };
            for (int index = 0; index < values.Length && index < 8; index++)
            {
                result.OpType[index] = (byte)values[index].type;
                result.OpValue[index] = values[index].value;
            }
            return result;
        }

        private static ScriptAction Nop() => new() { Type = (int)Sat.DoNothing };

        private static int[] Stats()
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[CharacterProgressionSource.LevelSourceSlot] = 1;
            result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return result;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static ArcanumObjectId Parse(string key)
        {
            Assert.That(ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity), Is.True);
            return identity;
        }

        private static byte[] GuidBytes(string key)
        {
            string compact = key.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private static byte[] Bytes(ArcanumObjectId id)
        {
            if (id == ProductionPlayerLifecycle.DefaultPlayerIdentity)
            {
                var bytes = new byte[ArcanumObjectId.SerializedSize];
                bytes[0] = (byte)ArcanumObjectIdType.Guid;
                Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0,
                    bytes, 8, 16);
                return bytes;
            }
            if (id.Type == ArcanumObjectIdType.Guid) return GuidBytes(id.Key);
            if (id.Type == ArcanumObjectIdType.Authored)
            {
                uint value = uint.Parse(id.Key.Substring(2), System.Globalization.NumberStyles.HexNumber);
                var bytes = new byte[ArcanumObjectId.SerializedSize];
                bytes[0] = (byte)ArcanumObjectIdType.Authored;
                for (int index = 0; index < 4; index++) bytes[8 + index] = (byte)(value >> (index * 8));
                return bytes;
            }
            return null;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string path = PresentedSector;
                PresentedSector = null;
                if (path != null) _session.UnloadSector(path);
            }
        }
    }
}
