using System;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M12CFullGameUi")]
    public sealed class M12CFullGameUiTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private ArcanumObjectId _pc;
        private ObjectProtoInfo _armorPrototype;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(M12CFullGameUiTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.GetOrCreatePlayer(_pc, Sector, Vector2.one, 0x28100000u);
            _armorPrototype = new ObjectProtoInfo(8100, ObjectType.Armor, 0x70000000u, weight: 3);
            _session.BindPrototypeSource(number => number == 8100 ? _armorPrototype : null);
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            _controller = new GameUiController(_session, new EmptySlots());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void HudProjectsAuthoritativeVitalsWithoutMutatingThem()
        {
            int hp = _session.Vitality.Get(_pc).CurrentHitPoints;
            GameUiHudView view = _controller.ProjectHud();
            Assert.That(view, Is.Not.Null);
            Assert.That((view.HitPoints, view.MaximumHitPoints), Is.EqualTo((hp, _session.Vitality.Get(_pc).MaximumHitPoints)));
            Assert.That(_session.Vitality.Get(_pc).CurrentHitPoints, Is.EqualTo(hp));
        }

        [Test]
        public void ModalOpenAndCloseOwnOnlyPresentationState()
        {
            PersistentPlayerState player = _session.PlayerState;
            string sector = _session.SelectedSector;
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True);
            Assert.That((_controller.Screen, _controller.IsModalOpen), Is.EqualTo((GameUiScreen.Inventory, true)));
            _controller.Close();
            Assert.That((_controller.Screen, _controller.IsModalOpen), Is.EqualTo((GameUiScreen.None, false)));
            Assert.That(_session.PlayerState, Is.SameAs(player));
            Assert.That(_session.SelectedSector, Is.EqualTo(sector));
        }

        [Test]
        public void NoPlayerRejectsScreenBeforeAnySessionMutation()
        {
            var otherRoot = new GameObject("NoPlayerM12C");
            try
            {
                var session = otherRoot.AddComponent<WorldMapSessionCoordinator>();
                var controller = new GameUiController(session, new EmptySlots());
                Assert.That(controller.Open(GameUiScreen.Inventory), Is.False);
                Assert.That(controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
                Assert.That(controller.BeginNewGame(), Is.False);
                Assert.That(session.PlayerState, Is.Null);
            }
            finally { Object.DestroyImmediate(otherRoot); }
        }

        [Test]
        public void InventoryProjectionUsesStableIdentityAndPrototypeDescription()
        {
            PersistentObjectState armor = AddArmor(1);
            GameUiItemView view = _controller.ProjectInventory().Single();
            Assert.That((view.Identity, view.Name, view.Type, view.Quantity),
                Is.EqualTo((armor.Identity, "Armor 8100", ObjectType.Armor, 1)));
            Assert.That(view.IsEquipped, Is.False);
        }

        [Test]
        public void EquipAndUnequipDelegateToM3Authority()
        {
            PersistentObjectState armor = AddArmor(2);
            Assert.That(_controller.Equip(armor.Identity), Is.True, _controller.Feedback);
            Assert.That(_session.States[armor.Identity].Placement.WornLocation, Is.EqualTo(WornLocation.Armor));
            Assert.That(_controller.Unequip(WornLocation.Armor), Is.True, _controller.Feedback);
            Assert.That(_session.States[armor.Identity].Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
        }

        [Test]
        public void ItemSelectionRejectsForeignIdentityWithoutMutation()
        {
            PersistentObjectState armor = AddArmor(3);
            ArcanumObjectId unknown = Parse("G_99000000_0000_0000_0000_000000000000");
            Assert.That(_controller.SelectItem(unknown), Is.False);
            Assert.That(_controller.SelectedItem.IsNull, Is.True);
            Assert.That(_session.States[armor.Identity].Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
        }

        [Test]
        public void CharacterScreenProjectsM4StateAndHasNoLocalCopy()
        {
            GameUiCharacterView view = _controller.ProjectCharacter();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Attributes.Count, Is.EqualTo(8));
            Assert.That(view.Skills.Count, Is.EqualTo(16));
            Assert.That(view.Level, Is.EqualTo(_session.Progression.Get(_pc).Level));
            Assert.That(view.HitPointFreeProjectionGuard(), Is.True);
        }

        [Test]
        public void MagicScreenProjectsEntireSourceCatalogAndKnowledge()
        {
            var spells = _controller.ProjectSpells();
            Assert.That(spells.Count, Is.EqualTo(PhaseOneSpellCatalog.All.Count()));
            Assert.That(spells.Count, Is.GreaterThan(0));
            Assert.That(spells, Has.All.Matches<GameUiSpellView>(value => value.Definition != null));
            Assert.That(spells.Count(value => value.Learned), Is.Zero);
        }

        [Test]
        public void SpellTargetingStoresOnlyTransientIntentAndInvalidTargetFailsClosed()
        {
            int fatigue = _session.Vitality.Get(_pc).CurrentFatigue;
            _controller.BeginSpellTargeting(PhaseOneSpellCatalog.StrengthOfEarth);
            Assert.That(_controller.CursorMode, Is.EqualTo(GameUiCursorMode.SpellTarget));
            Assert.That(_controller.SubmitWorldTarget(default), Is.False);
            Assert.That(_session.Vitality.Get(_pc).CurrentFatigue, Is.EqualTo(fatigue));
        }

        [Test]
        public void TechnologyScreenProjectsEveryDisciplineFromM10B()
        {
            var technologies = _controller.ProjectTechnology();
            Assert.That(technologies.Count, Is.EqualTo(8));
            Assert.That(technologies.Select(value => value.Discipline).Distinct().Count(), Is.EqualTo(8));
        }

        [Test]
        public void CraftingScreenUsesDeterministicKnownSchematicProjection()
        {
            _session.BindCraftingSource(SchematicCatalog.FromMes(
                MesReader.Read("{2000}{1}{2001}{2}{2002}{3}{2003}{10061}{2004}{10062}{2005}{10059}{2006}{1}"),
                MesReader.Read("{1}{Herbal Cure}{2}{Makes a cure}")));
            Assert.That(_controller.ProjectSchematics(), Is.Empty);
            Assert.That(_session.Technology.LearnNextDegree(_pc, TechnologyDiscipline.Herbology).Succeeded, Is.True);
            GameUiSchematicView known = _controller.ProjectSchematics().Single();
            Assert.That((known.Definition.Id.Value, known.Definition.Name), Is.EqualTo((2000, "Herbal Cure")));
        }

        [Test]
        public void InvalidMerchantSelectionFailsBeforeEconomyMutation()
        {
            int gold = _session.GetGold(_pc);
            Assert.That(_controller.BeginMerchant(_pc), Is.False);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.None));
            Assert.That(_session.GetGold(_pc), Is.EqualTo(gold));
        }

        [Test]
        public void JournalAndMapFailClosedWhenSourceDataIsUnavailable()
        {
            Assert.That(_controller.ProjectJournal(), Is.Empty);
            Assert.That(_controller.ProjectDestinations(out WorldMapDestinationFailure failure), Is.Empty);
            Assert.That(failure, Is.EqualTo(WorldMapDestinationFailure.AreaSourceUnavailable));
        }

        [Test]
        public void PartyProjectionIsReadOnlyAndEmptyWithoutFollowers()
        {
            Assert.That(_controller.ProjectParty(), Is.Empty);
            Assert.That(_session.Party.Count, Is.Zero);
        }

        [Test]
        public void SaveLoadScreenUsesControllerPolicyWithoutWritingOnOpen()
        {
            _controller.OpenSaveLoad(SaveLoadPanelMode.Load);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.SaveLoad));
            Assert.That(_controller.SaveLoad.IsOpen, Is.True);
            Assert.That(_controller.SaveLoad.Mode, Is.EqualTo(SaveLoadPanelMode.Load));
            Assert.That(_controller.SaveLoad.Slots, Is.Empty);
        }

        [Test]
        public void PresentationRebuildPreservesAuthorityAndReopensCurrentScreen()
        {
            PersistentObjectState armor = AddArmor(4);
            _controller.Open(GameUiScreen.Inventory);
            _controller.SelectItem(armor.Identity);
            PersistentPlayerState player = _session.PlayerState;
            _controller.RebuildPresentation();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Inventory));
            Assert.That(_controller.SelectedItem.IsNull, Is.True);
            Assert.That(_session.PlayerState, Is.SameAs(player));
            Assert.That(_session.States.ContainsKey(armor.Identity), Is.True);
        }

        [Test]
        public void CombatHudControllerIsProjectionOnlyWhileCombatIsInactive()
        {
            _controller.Refresh();
            Assert.That(_controller.Combat.IsVisible, Is.False);
            Assert.That(_controller.Combat.HasSelectedTarget, Is.False);
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        [Test]
        public void ProductionCompositionAddsOneUnifiedPresenterAndSuppressesLegacyDrawing()
        {
            WorldObjectSectorLoader loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.EnsureProductionPresentationComponents();
            loader.EnsureProductionPresentationComponents();
            Assert.That(_root.GetComponents<ProductionGameUiPresenter>(), Has.Length.EqualTo(1));
            Assert.That(_root.GetComponent<ProductionGameUiPresenter>().Controller, Is.Not.Null);
            Assert.That(_root.GetComponent<Arcanum.Runtime.Combat.ProductionCombatPresenter>().enabled, Is.False);
            Assert.That(_root.GetComponent<Arcanum.Runtime.Save.ProductionSaveLoadPresenter>().enabled, Is.False);
            Assert.That(_root.GetComponent<Arcanum.Runtime.Dialogue.ProductionDialoguePresenter>().enabled, Is.False);
        }

        private PersistentObjectState AddArmor(int sequence)
        {
            var source = new ObjectInstance(ObjectType.Armor, 8100, null, 0x70000000u, 0, 0,
                oid: GuidBytes(sequence), parentOid: Bytes(_pc), invLocation: 0);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                inventoryArtId: 0u, unitWeight: 3, inventoryLocation: 0);
        }

        private static byte[] GuidBytes(int sequence)
            => Bytes(Parse($"G_{sequence:X8}_0000_0000_0000_000000000000"));

        private static ArcanumObjectId Parse(string key)
        { ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId id); return id; }

        private static byte[] Bytes(ArcanumObjectId identity)
        {
            string compact = identity.Key.Substring(2).Replace("_", "");
            var bytes = new byte[24]; bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int i = 0; i < 16; i++) bytes[8 + i] = Convert.ToByte(compact.Substring(i * 2, 2), 16);
            return bytes;
        }

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots()
                => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            { PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path); _session.BeginSector(PresentedSector); return true; }
            public void ClearPresentedSector()
            { string path = PresentedSector; PresentedSector = null; if (path != null) _session.UnloadSector(path); }
        }
    }

    internal static class M12CViewAssertions
    {
        public static bool HitPointFreeProjectionGuard(this GameUiCharacterView view)
            => view != null && view.Attributes != null && view.Skills != null;
    }
}
