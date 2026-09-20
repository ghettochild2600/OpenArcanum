using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Arcanum.Formats.Database;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M7CAreaDiscovery")]
    public sealed class M7CAreaDiscoveryTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/96502547529.sec";
        private const string OtherSector = "maps/arcanum1-024-fixed/96502547530.sec";
        private const int DialogueNumber = 1497;
        private static readonly AreaId KnaTha = new(58);
        private static readonly ArcanumObjectId Clarissa = ParseIdentity(
            "G_48830599_2627_9E4B_9996_FEB1835EDCDD");

        private static DatVirtualFileSystem _vfs;
        private static ScriptDatabase _scripts;
        private static DialogScript _dialogue;
        private static AreaList _areas;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private Owner _owner;

        [OneTimeSetUp]
        public void LoadAuthenticResources()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) Assert.Ignore("The local clean Arcanum module archive is unavailable.");
            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) _vfs.MountFile(path);
            }
            _scripts = ScriptDatabase.Load(_vfs);
            _dialogue = DialogLocator.Load(_vfs, DialogueNumber);
            _areas = AreaList.FromMes(MesReader.Read(_vfs.ReadAllBytes("mes/gamearea.mes")));
        }

        [OneTimeTearDown] public void ReleaseResources() => _vfs?.Dispose();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M7CAreaDiscoveryTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _owner = new Owner(_session);
            _session.RegisterObjectOwner(_owner);
            _session.BindAreaSource(_areas);
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc));

            var source = new ObjectInstance(ObjectType.Npc, 17229, Location(3, 1), 0x28100000u, 0, 0,
                oid: GuidBytes("G_48830599_2627_9E4B_9996_FEB1835EDCDD"), dialogNum: DialogueNumber);
            PersistentObjectState npc = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
            AddNpcCharacter(npc);
            _session.Bind(Sector, npc, Runtime("Clarissa Shalmo", ObjectType.Npc));
            _session.BindDialogueSource(_scripts.Get, number => number == DialogueNumber ? _dialogue : null);

            Assert.That(_session.Progression.IncreaseSkill(_pc.Identity, CharacterSkill.Throwing),
                Is.EqualTo(SkillIncreaseResult.Success));
            Assert.That(_session.Progression.SetTrainingLevel(_pc.Identity, CharacterSkill.Throwing,
                SkillTrainingLevel.Apprentice), Is.EqualTo(TrainingAssignmentResult.Success));
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticMetadataUsesStableTypedIdentityAndNoDefaultKnownArea()
        {
            Assert.That(_areas.Count, Is.EqualTo(82));
            Assert.That(_areas.TryGet(KnaTha, out Area area), Is.True);
            Assert.That(area.Id, Is.EqualTo(58));
            Assert.That(area.Name, Is.EqualTo("K'na Tha"));
            Assert.That(area.TileX, Is.EqualTo(91902));
            Assert.That(area.TileY, Is.EqualTo(39305));
            Assert.That(area.RadiusTiles, Is.EqualTo(-1));
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.False);
            Assert.That(_session.Campaign.CanSelectWorldArea(KnaTha), Is.False);
        }

        [Test]
        public void DiscoveryIsIdempotentAndFutureSelectionQuerySharesAuthority()
        {
            int notifications = 0;
            _session.Campaign.AreaDiscovered += _ => notifications++;
            Assert.That(_session.Campaign.TryDiscoverArea(KnaTha, out bool first,
                out CampaignStateFailure firstFailure), Is.True);
            Assert.That(first, Is.True);
            Assert.That(firstFailure, Is.EqualTo(CampaignStateFailure.None));
            Assert.That(_session.Campaign.TryDiscoverArea(KnaTha, out bool second,
                out CampaignStateFailure secondFailure), Is.True);
            Assert.That(second, Is.False);
            Assert.That(secondFailure, Is.EqualTo(CampaignStateFailure.None));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(_session.Campaign.CanSelectWorldArea(KnaTha), Is.True);
            Assert.That(_session.Campaign.KnownAreas, Is.EqualTo(new[] { KnaTha }));
        }

        [TestCase(0, CampaignStateFailure.InvalidArea)]
        [TestCase(999, CampaignStateFailure.InvalidArea)]
        public void InvalidDiscoveryFailsWithoutMutation(int value, CampaignStateFailure expected)
        {
            Assert.That(_session.Campaign.TryDiscoverArea(new AreaId(value), out bool changed,
                out CampaignStateFailure failure), Is.False);
            Assert.That(changed, Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
        }

        [Test]
        public void UnboundCampaignRejectsAreaMutation()
        {
            var campaign = new CampaignStateService();
            Assert.That(campaign.TryDiscoverArea(KnaTha, out _, out CampaignStateFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(CampaignStateFailure.AreaSourceUnavailable));
        }

        [Test]
        public void AuthenticClarissaDialogueDiscoversOnlyAtSourceMmResponse()
        {
            Assert.That(_scripts.Get(DialogueNumber), Is.Not.Null);
            Assert.That(_dialogue.TryGet(93, out DialogLine marker), Is.True);
            Assert.That(marker.Effect, Is.EqualTo("mm58"));
            Assert.That(_session.Dialogue.Start(_pc.Identity, Clarissa), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(1));

            foreach (int line in new[] { 2, 44, 55, 70, 77, 81, 97, 88 })
            {
                Choose(line);
                Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.False,
                    $"area changed before source response {line}");
            }
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(92));
            Choose(93);
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(123));
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.True);
            Assert.That(_session.Campaign.GetPcQuestState(1097), Is.EqualTo(2));
        }

        [Test]
        public void UnsupportedTailAfterMmFailsBeforeAnyAreaMutation()
        {
            var lines = new SortedDictionary<int, DialogLine>
            {
                [1] = new DialogLine(1, "Map?", "Map?", 0, "", 0, ""),
                [2] = new DialogLine(2, "Mark it.", "", 5, "", 0, "mm58, co"),
            };
            _session.BindDialogueSource(_scripts.Get, number => number == DialogueNumber
                ? new DialogScript(lines) : null);
            Assert.That(_session.Dialogue.Start(_pc.Identity, Clarissa), Is.EqualTo(DialogueStartStatus.Started));
            LogAssert.Expect(LogType.Warning, new Regex(
                "OpenArcanum dialogue compatibility:.*UnsupportedEffect.*dialogue=1497"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.False);
        }

        [Test]
        public void KnownStateSurvivesSectorChangesWithoutBecomingPresentationOwned()
        {
            CampaignStateService campaign = _session.Campaign;
            campaign.DiscoverArea(KnaTha);
            Assert.That(_session.SelectSector(OtherSector), Is.True);
            Assert.That(_session.SelectSector(Sector), Is.True);
            Assert.That(_session.Campaign, Is.SameAs(campaign));
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.True);
            Assert.That(_session.Campaign.KnownAreas.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnteringUnrelatedSectorsDoesNotInventDiscovery()
        {
            Assert.That(_session.SelectSector(OtherSector), Is.True);
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
            Assert.That(_session.SelectSector(Sector), Is.True);
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
        }

        [Test]
        public void V1RoundTripRestoresKnownStateAndKeepsSchemaVersion()
        {
            _session.Campaign.DiscoverArea(KnaTha);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"version\": 1"));
            Assert.That(json, Does.Contain("\"knownAreas\""));
            _session.ResetAuthoritativeSession();
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.False);
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.True);
        }

        [Test]
        public void OlderV1WithoutKnownAreasLoadsAsSourceCorrectEmptySet()
        {
            _session.Campaign.DiscoverArea(KnaTha);
            string json = _session.SaveGames.SerializeCurrentSession();
            string legacy = Regex.Replace(json, @",\s*""knownAreas""\s*:\s*\[[^\]]*\]", string.Empty);
            Assert.That(legacy, Does.Not.Contain("knownAreas"));
            Assert.That(_session.SaveGames.LoadJson(legacy).Succeeded, Is.True);
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(999)]
        public void InvalidSavedAreaFailsTransactionally(int value)
        {
            _session.Campaign.DiscoverArea(KnaTha);
            CampaignStateService before = _session.Campaign;
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Campaign.KnownAreas = new List<int> { value };
            SessionLoadResult result = _session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data));
            Assert.That(result.Failure, Is.EqualTo(SessionLoadFailure.InvalidCampaign));
            Assert.That(_session.Campaign, Is.SameAs(before));
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.True);
        }

        [Test]
        public void DuplicateOrNullSavedAreaCollectionFailsTransactionally()
        {
            _session.Campaign.DiscoverArea(KnaTha);
            CampaignStateService before = _session.Campaign;
            SessionSaveData duplicate = _session.SaveGames.CaptureData();
            duplicate.Campaign.KnownAreas = new List<int> { 58, 58 };
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(duplicate)).Failure,
                Is.EqualTo(SessionLoadFailure.InvalidCampaign));
            SessionSaveData missing = _session.SaveGames.CaptureData();
            missing.Campaign.KnownAreas = null;
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(missing)).Failure,
                Is.EqualTo(SessionLoadFailure.InvalidCampaign));
            Assert.That(_session.Campaign, Is.SameAs(before));
            Assert.That(_session.Campaign.IsAreaKnown(KnaTha), Is.True);
        }

        private void Choose(int line)
        {
            int index = _session.Dialogue.AvailableResponses.ToList().FindIndex(value => value.Num == line);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                $"response {line} unavailable at node {_session.Dialogue.CurrentLine}; available: "
                + string.Join(",", _session.Dialogue.AvailableResponses.Select(value => value.Num)));
            Assert.That(_session.Dialogue.SelectResponse(index), Is.EqualTo(DialogueChoiceStatus.Advanced));
        }

        private void AddNpcCharacter(PersistentObjectState npc)
        {
            var stats = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) stats[index] = 8;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Female;
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.HalfElf;
            _session.Characters.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                stats, null);
            _session.Progression.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                new CharacterProgressionSource(1, 0, 0, new int[12], new int[4]));
            _session.DerivedStats.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                new CharacterDerivedSource(0, new int[5], 0, 0, 0, 50));
            _session.Vitality.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                CharacterVitalitySource.Resolve(null, stats, null, 0, null, 0, null, 0,
                    null, 0, null, 0, null, 0));
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static ArcanumObjectId ParseIdentity(string value)
        {
            if (!ArcanumObjectId.TryParsePersistent(value, out ArcanumObjectId identity))
                throw new InvalidOperationException("Invalid test ObjectID: " + value);
            return identity;
        }

        private static byte[] GuidBytes(string value)
        {
            string compact = value.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
