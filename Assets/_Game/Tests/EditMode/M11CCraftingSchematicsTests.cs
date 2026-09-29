using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M11CCraftingSchematics")]
    public sealed class M11CCraftingSchematicsTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private readonly Dictionary<int, ObjectProtoInfo> _prototypes = new();

        [SetUp]
        public void SetUp()
        {
            _prototypes.Clear();
            _root = new GameObject(nameof(M11CCraftingSchematicsTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPrototypeSource(number => _prototypes.TryGetValue(number, out ObjectProtoInfo value) ? value : null);
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            BindRecipe(4020, 10084, 15116, 15169);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void CatalogParsesSevenRowDefinitionAndPadsAliasesExactly()
        {
            SchematicCatalog catalog = Catalog(4450, "11", "12", "99", "15108", "8075 8083 8088",
                "8229 8230 8231", "1");
            Assert.That(catalog.TryGet(new SchematicId(4450), out SchematicDefinition value), Is.True);
            Assert.That((value.Name, value.Description, value.ArtNumber, value.ProductQuantity, value.Acquisition),
                Is.EqualTo(("Electro-Armor", "Armor description", 99, 1, SchematicAcquisition.Found)));
            Assert.That(value.ComponentOneAliases, Is.EqualTo(new[] { 15108, 15108, 15108 }));
            Assert.That(value.ComponentTwoAliases, Is.EqualTo(new[] { 8075, 8083, 8088 }));
            Assert.That(value.ProductAliases, Is.EqualTo(new[] { 8229, 8230, 8231 }));
        }

        [Test]
        public void MalformedCatalogFailsClosed()
        {
            Assert.Throws<FormatException>(() => Catalog(4020, "1", "2", "3", "10084", "15116", "15169", "0"));
            Assert.Throws<FormatException>(() => Catalog(4020, "1", "2", "3", "", "15116", "15169", "1"));
        }

        [Test]
        public void BuiltInKnowledgeIsAProjectionOfCurrentEffectiveTechnologyDegree()
        {
            _session.BindCraftingSource(Catalog(2000, "1", "2", "3", "10061", "10062", "10059", "5"));
            Assert.That(_session.Crafting.Knows(_pc.Identity, new SchematicId(2000)), Is.False);
            Assert.That(_session.Technology.LearnNextDegree(_pc.Identity, TechnologyDiscipline.Herbology).Succeeded, Is.True);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity, TechnologyDiscipline.Herbology), Is.EqualTo(10));
            Assert.That(_session.Crafting.Knows(_pc.Identity, new SchematicId(2000)), Is.True);
            Assert.That(_session.Crafting.ExportSaveData().Characters, Is.Empty);
        }

        [Test]
        public void FirstFoundLearningConsumesOwnedBlueprintAndPersistsOrderedKnowledge()
        {
            PersistentObjectState blueprint = Blueprint(1, 4020);
            SchematicLearningResult result = _session.Crafting.LearnFoundSchematic(_pc.Identity, blueprint.Identity);
            Assert.That((result.Succeeded, result.ItemConsumed, result.Schematic.Value), Is.EqualTo((true, true, 4020)));
            Assert.That(_session.IsObjectRemoved(blueprint.Identity), Is.True);
            Assert.That(_session.Crafting.GetFoundSchematics(_pc.Identity).Select(value => value.Value), Is.EqualTo(new[] { 4020 }));
        }

        [Test]
        public void DuplicateFoundLearningLeavesSecondBlueprintUntouched()
        {
            Assert.That(_session.Crafting.LearnFoundSchematic(_pc.Identity, Blueprint(2, 4020).Identity).Succeeded, Is.True);
            PersistentObjectState duplicate = Blueprint(3, 4020);
            SchematicLearningResult result = _session.Crafting.LearnFoundSchematic(_pc.Identity, duplicate.Identity);
            Assert.That(result.Failure, Is.EqualTo(SchematicLearningFailure.AlreadyKnown));
            Assert.That(_session.TryGetObjectState(duplicate.Identity, out _), Is.True);
        }

        [Test]
        public void InvalidWrittenKindOwnershipAndUnknownRecipeRejectWithoutMutation()
        {
            PersistentObjectState ordinary = Item(4, 10084);
            Assert.That(_session.Crafting.LearnFoundSchematic(_pc.Identity, ordinary.Identity).Failure,
                Is.EqualTo(SchematicLearningFailure.NotASchematic));
            PersistentObjectState unknown = Blueprint(5, 4990);
            Assert.That(_session.Crafting.LearnFoundSchematic(_pc.Identity, unknown.Identity).Failure,
                Is.EqualTo(SchematicLearningFailure.UnknownSchematic));
            Assert.That(_session.TryGetObjectState(ordinary.Identity, out _), Is.True);
            Assert.That(_session.TryGetObjectState(unknown.Identity, out _), Is.True);
        }

        [Test]
        public void AuthenticFoundRecipeConsumesTwoInputsAndCreatesOrdinaryProduct()
        {
            Learn4020();
            PersistentObjectState first = Item(6, 10084);
            PersistentObjectState second = Item(7, 15116);
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(4020)));
            Assert.That((result.Succeeded, result.ProductPrototype, result.ProductQuantity), Is.EqualTo((true, 15169, 1)));
            Assert.That(_session.IsObjectRemoved(first.Identity), Is.True);
            Assert.That(_session.IsObjectRemoved(second.Identity), Is.True);
            Assert.That(result.Products, Has.Count.EqualTo(1));
            Assert.That(_session.States[result.Products[0]].PrototypeNumber, Is.EqualTo(15169));
            Assert.That(_session.States[result.Products[0]].ParentIdentity, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void MissingSecondComponentRejectsBeforeFirstComponentMutation()
        {
            Learn4020();
            PersistentObjectState first = Item(8, 10084);
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(4020)));
            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.MissingComponentTwo));
            Assert.That(_session.TryGetObjectState(first.Identity, out _), Is.True);
            Assert.That(_session.States.Values.Any(value => value.PrototypeNumber == 15169), Is.False);
        }

        [Test]
        public void EquippedAndNestedItemsAreNotCraftingComponents()
        {
            Learn4020();
            PersistentObjectState equipped = Item(9, 10084, equipped: true);
            PersistentObjectState container = Item(10, 19000, type: ObjectType.Container);
            PersistentObjectState nested = Item(11, 15116, owner: container.Identity);
            CraftingResult result = _session.Crafting.Preview(new CraftingRequest(_pc.Identity, new SchematicId(4020)));
            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.MissingComponentOne));
            Assert.That(_session.TryGetObjectState(equipped.Identity, out _), Is.True);
            Assert.That(_session.TryGetObjectState(nested.Identity, out _), Is.True);
        }

        [Test]
        public void CandidateSelectionUsesInventoryLocationThenStableIdentity()
        {
            Learn4020();
            PersistentObjectState late = Item(12, 10084, inventoryLocation: 20);
            PersistentObjectState early = Item(13, 10084, inventoryLocation: 2);
            Item(14, 15116);
            CraftingResult result = _session.Crafting.Preview(new CraftingRequest(_pc.Identity, new SchematicId(4020)));
            Assert.That(result.ComponentOne.Item, Is.EqualTo(early.Identity));
            Assert.That(result.ComponentOne.Item, Is.Not.EqualTo(late.Identity));
        }

        [Test]
        public void ComponentExpertiseUsesM10BEffectiveLevelAndRejectsTransactionally()
        {
            Learn4020();
            _prototypes[10084].ItemComplexity = -10;
            _prototypes[10084].ItemDiscipline = (int)TechnologyDiscipline.Herbology;
            PersistentObjectState first = Item(15, 10084);
            PersistentObjectState second = Item(16, 15116);
            CraftingRequest request = new(_pc.Identity, new SchematicId(4020));
            Assert.That(_session.Crafting.Execute(request).Failure, Is.EqualTo(CraftingFailure.InsufficientExpertise));
            Assert.That(_session.TryGetObjectState(first.Identity, out _), Is.True);
            Assert.That(_session.TryGetObjectState(second.Identity, out _), Is.True);
            Assert.That(_session.Technology.LearnNextDegree(_pc.Identity, TechnologyDiscipline.Herbology).Succeeded, Is.True);
            Assert.That(_session.Crafting.Execute(request).Succeeded, Is.True);
        }

        [Test]
        public void AlternativeComponentSelectsCorrespondingProductVariant()
        {
            BindRecipe(4450, 15108, 8075, 8229, "8075 8083 8088", "8229 8230 8231");
            Learn(20, 4450);
            Item(21, 15108); Item(22, 8083);
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(4450)));
            Assert.That((result.ComponentTwo.AliasIndex, result.ProductPrototype), Is.EqualTo((1, 8230)));
        }

        [Test]
        public void ProductQuantityCreatesExactNumberOfItems()
        {
            BindRecipe(4030, 10084, 10119, 10116, quantity: 3);
            Learn(23, 4030); Item(24, 10084); Item(25, 10119);
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(4030)));
            Assert.That(result.Products.Count, Is.EqualTo(3));
            Assert.That(result.Products.Select(id => _session.States[id].PrototypeNumber), Is.All.EqualTo(10116));
        }

        [Test]
        public void AmmoComponentConsumesOneUnitPerSlotFromSameStack()
        {
            BindRecipe(5420, 7040, 7040, 16000);
            Learn(26, 5420);
            PersistentObjectState ammo = Item(27, 7040, ObjectType.Ammo, quantity: 3);
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(5420)));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(ammo.StackQuantity, Is.EqualTo(1));
        }

        [Test]
        public void ProductCreationFailureRollsBackInputsOutputsTombstonesAndAllocator()
        {
            Learn4020();
            PersistentObjectState first = Item(28, 10084);
            PersistentObjectState second = Item(29, 15116);
            _prototypes[15169] = Proto(15169, ObjectType.Food, weight: 10001);
            ulong allocator = _session.NextDynamicIdentity;
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(4020)));
            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.InventoryFailure));
            Assert.That(_session.TryGetObjectState(first.Identity, out _), Is.True);
            Assert.That(_session.TryGetObjectState(second.Identity, out _), Is.True);
            Assert.That(_session.NextDynamicIdentity, Is.EqualTo(allocator));
            Assert.That(_session.States.Values.Any(value => value.PrototypeNumber == 15169), Is.False);
        }

        [Test]
        public void SaveV1RestoresFoundKnowledgeWrittenFieldsAndNoTransientCraft()
        {
            Learn4020();
            PersistentObjectState secondBlueprint = Blueprint(30, 4020);
            Item(31, 10084); Item(32, 15116);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Crafting.Knows(_pc.Identity, new SchematicId(4020)), Is.True);
            Assert.That(_session.TryGetObjectState(secondBlueprint.Identity, out PersistentObjectState restored), Is.True);
            Assert.That((restored.WrittenSubtype, restored.WrittenStartLine), Is.EqualTo((5, 4020)));
            Assert.That(_session.States.Values.Count(value => value.PrototypeNumber == 15169), Is.Zero);
            Assert.That(_session.Crafting.Execute(new CraftingRequest(_pc.Identity, new SchematicId(4020))).Succeeded, Is.True);
        }

        [Test]
        public void EarlierV1WithoutCraftingDomainLoadsAsEmptyKnowledge()
        {
            Learn4020();
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Crafting = null;
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Succeeded, Is.True);
            Assert.That(_session.Crafting.GetFoundSchematics(_pc.Identity), Is.Empty);
        }

        [Test]
        public void UnknownFoundSchematicInSaveFailsBeforeSessionMutation()
        {
            SessionSaveData data = _session.SaveGames.CaptureData();
            data.Crafting = new CraftingSaveData
            {
                Characters = new List<CraftingCharacterSaveData>
                {
                    new() { Identity = _pc.Identity.Key, FoundSchematicIds = new List<int> { 4990 } },
                },
            };
            string sector = _session.SelectedSector;
            Assert.That(_session.SaveGames.LoadJson(_session.SaveGames.SerializeData(data)).Failure,
                Is.EqualTo(SessionLoadFailure.InvalidCharacter));
            Assert.That(_session.SelectedSector, Is.EqualTo(sector));
            Assert.That(_session.Crafting.GetFoundSchematics(_pc.Identity), Is.Empty);
        }

        private void Learn4020() => Learn(40, 4020);
        private void Learn(int sequence, int recipe)
            => Assert.That(_session.Crafting.LearnFoundSchematic(_pc.Identity, Blueprint(sequence, recipe).Identity).Succeeded, Is.True);

        private void BindRecipe(int id, int first, int second, int product, string secondAliases = null,
            string productAliases = null, int quantity = 1)
        {
            _session.BindCraftingSource(Catalog(id, "1", "2", "3", first.ToString(),
                secondAliases ?? second.ToString(), productAliases ?? product.ToString(), quantity.ToString()));
            AddProto(first); AddProto(second); AddProto(product);
            if (secondAliases != null) foreach (int value in secondAliases.Split(' ').Select(int.Parse)) AddProto(value);
            if (productAliases != null) foreach (int value in productAliases.Split(' ').Select(int.Parse)) AddProto(value);
        }

        private static SchematicCatalog Catalog(int id, params string[] values)
        {
            string rules = string.Concat(values.Select((value, index) => $"{{{id + index}}}{{{value}}}"));
            return SchematicCatalog.FromMes(MesReader.Read(rules),
                MesReader.Read("{1}{Electro-Armor}{2}{Armor description}{11}{Electro-Armor}{12}{Armor description}"));
        }

        private void AddProto(int number)
        {
            if (!_prototypes.ContainsKey(number)) _prototypes.Add(number, Proto(number));
        }
        private static ObjectProtoInfo Proto(int number, ObjectType type = ObjectType.Food, int weight = 1)
            => new(number, type, 0x50000000u, weight: weight) { ItemComplexity = 0 };

        private PersistentObjectState Blueprint(int sequence, int recipe)
        {
            int prototype = 14000 + sequence;
            _prototypes[prototype] = new ObjectProtoInfo(prototype, ObjectType.Written, 0x50000000u)
                { WrittenSubtype = 5, TextStartLine = recipe };
            return Item(sequence, prototype, ObjectType.Written, writtenSubtype: 5, writtenStartLine: recipe);
        }

        private PersistentObjectState Item(int sequence, int prototype, ObjectType type = ObjectType.Food,
            ArcanumObjectId owner = default, bool equipped = false, int? quantity = null,
            int inventoryLocation = 0, int? writtenSubtype = null, int? writtenStartLine = null)
        {
            AddProto(prototype);
            if (type == ObjectType.Ammo)
                _prototypes[prototype] = new ObjectProtoInfo(prototype, type, 0x50000000u, weight: 4)
                    { AmmoQuantity = quantity ?? 1, ItemComplexity = 0 };
            if (type == ObjectType.Container) _prototypes[prototype] = Proto(prototype, type);
            ArcanumObjectId parent = owner.IsPersistent ? owner : _pc.Identity;
            var source = new ObjectInstance(type, prototype, null, 0x50000000u, 0, 0,
                oid: GuidBytes(sequence), parentOid: Bytes(parent),
                invLocation: equipped ? (int)WornLocation.Weapon : inventoryLocation);
            source.AmmoQuantity = type == ObjectType.Ammo ? quantity : null;
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                stackQuantity: type == ObjectType.Ammo ? quantity : null, inventoryLocation: inventoryLocation,
                writtenSubtype: writtenSubtype, writtenStartLine: writtenStartLine);
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
}
