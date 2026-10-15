using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [TestFixture]
    [Category("ScriptCompatibility")]
    public sealed class ScriptEffectAuthorityTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private ArcanumObjectId _actor;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ScriptEffectAuthorityTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _actor = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.GetOrCreatePlayer(_actor, Sector, Vector2.one, 0x28100000u);
            _session.Characters.GetOrCreateDevelopmentPlayer(_actor);
            _session.Progression.GetOrCreateDevelopmentPlayer(_actor);
            _session.Vitality.GetOrCreateDevelopmentPlayer(_actor);
            _session.DerivedStats.GetOrCreateDevelopmentPlayer(_actor);
            _session.BindScriptEffectSource(Catalog(
                (100, "st +2, melee +1, resistfire +5%, alignment +3"),
                (101, "maxhps +10, maxfatigue +5%"),
                (102, "wp +1, critchance +2")));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void DuplicateEffectsStackAndRemoveOnlyTheFirstMatchingInstance()
        {
            int strength = _session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Strength);
            int melee = _session.Progression.GetEffectiveSkillRank(_actor, CharacterSkill.Melee);
            int resistance = _session.DerivedStats.GetResistance(_actor, CharacterResistance.Fire);
            int alignment = _session.DerivedStats.GetAlignment(_actor);

            Assert.That(_session.ScriptEffects.Add(_actor, 100, (int)ScriptEffectCause.Item), Is.True);
            Assert.That(_session.ScriptEffects.Add(_actor, 100, (int)ScriptEffectCause.Item), Is.True);
            Assert.That(_session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Strength),
                Is.EqualTo(strength + 4));
            Assert.That(_session.Progression.GetEffectiveSkillRank(_actor, CharacterSkill.Melee),
                Is.EqualTo(melee + 2));
            Assert.That(_session.DerivedStats.GetResistance(_actor, CharacterResistance.Fire),
                Is.EqualTo((resistance * 110) / 100));
            Assert.That(_session.DerivedStats.GetAlignment(_actor), Is.EqualTo(alignment + 6));

            Assert.That(_session.ScriptEffects.RemoveFirst(_actor, 100), Is.True);
            Assert.That(_session.ScriptEffects.Effects, Has.Count.EqualTo(1));
            Assert.That(_session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Strength),
                Is.EqualTo(strength + 2));
            Assert.That(_session.ScriptEffects.RemoveFirst(_actor, 100), Is.True);
            Assert.That(_session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Strength),
                Is.EqualTo(strength));
        }

        [Test]
        public void MaximumVitalityEffectsPreserveCurrentValuesAndRoundTripPersistentState()
        {
            int maximumHp = _session.Vitality.GetMaximumHitPoints(_actor);
            int currentHp = _session.Vitality.GetCurrentHitPoints(_actor);
            int maximumFatigue = _session.Vitality.GetMaximumFatigue(_actor);
            int currentFatigue = _session.Vitality.GetCurrentFatigue(_actor);

            _session.ScriptEffects.Add(_actor, 101, (int)ScriptEffectCause.Item);
            Assert.That(_session.Vitality.GetMaximumHitPoints(_actor), Is.EqualTo(maximumHp + 10));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_actor), Is.EqualTo(currentHp));
            Assert.That(_session.Vitality.GetMaximumFatigue(_actor), Is.EqualTo(maximumFatigue * 105 / 100));
            Assert.That(_session.Vitality.GetCurrentFatigue(_actor), Is.EqualTo(currentFatigue));

            var saved = _session.ScriptEffects.ExportSaveData();
            _session.ClearSelectedSector();
            Assert.That(_session.SelectSector(Sector), Is.True);
            _session.ScriptEffects.RestoreSaveData(saved);

            Assert.That(_session.ScriptEffects.Effects.Single().EffectId, Is.EqualTo(101));
            Assert.That(_session.Vitality.GetMaximumHitPoints(_actor), Is.EqualTo(maximumHp + 10));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_actor), Is.EqualTo(currentHp));
        }

        [Test]
        public void SessionSaveLoadRestoresEffectInstancesAndContributions()
        {
            int strength = _session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Strength);
            _session.ScriptEffects.Add(_actor, 100, (int)ScriptEffectCause.Item);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.ScriptEffects.RemoveFirst(_actor, 100), Is.True);

            SessionLoadResult result = _session.SaveGames.LoadJson(json);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_session.ScriptEffects.Effects.Single().EffectId, Is.EqualTo(100));
            Assert.That(_session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Strength),
                Is.EqualTo(strength + 2));
        }

        [Test]
        public void UnsupportedSymbolsStayInventoriedWhileSupportedChangesStillApply()
        {
            Assert.That(_session.ScriptEffects.Add(_actor, 102, (int)ScriptEffectCause.Item), Is.True);
            RetailScriptEffectDefinition definition = Catalog((102, "wp +1, critchance +2")).Definitions[102];

            Assert.That(definition.IsFullySupported, Is.False);
            Assert.That(definition.UnsupportedChanges, Is.EqualTo(new[] { "critchance +2" }));
            Assert.That(_session.Characters.GetEffectiveAttribute(_actor, CharacterAttribute.Willpower), Is.EqualTo(9));
        }

        [Test]
        public void RetailCatalogParsesRepresentativeShippedEffectAndNonCharacterTargetsFailClosed()
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            RetailScriptEffectCatalog catalog = RetailScriptEffectCatalog.FromMes(
                MesReader.Read(vfs.ReadAllBytes("rules/effect.mes")),
                vfs.Exists("rules/gameeffect.mes") ? MesReader.Read(vfs.ReadAllBytes("rules/gameeffect.mes")) : null);

            Assert.That(catalog.Definitions[282].SourceDefinition, Is.EqualTo("maxfatigue +5%"));
            Assert.That(catalog.Definitions[282].IsFullySupported, Is.True);
            Assert.That(_session.ScriptEffects.Add(default, 282, (int)ScriptEffectCause.Item), Is.False);
        }

        private static RetailScriptEffectCatalog Catalog(params (int id, string value)[] definitions)
            => RetailScriptEffectCatalog.FromMes(new MesFile(definitions
                .Select(value => new KeyValuePair<int, string>(value.id, value.value)).ToList()));

        private static DatVirtualFileSystem MountSourceData()
        {
            var vfs = new DatVirtualFileSystem();
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) throw new FileNotFoundException("modules/Arcanum.dat");
            vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
            }
            return vfs;
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
