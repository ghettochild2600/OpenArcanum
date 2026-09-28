using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M11AEconomyVendor")]
    public sealed class M11AEconomyVendorTests
    {
        private const string Sector = "maps/test/1.sec";
        private const int MerchandisePrototype = 6021;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _merchant;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(M11AEconomyVendorTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _merchant = AddMerchant("G_11111111_1111_1111_1111_111111111111", 1, 150);
            _session.BindEconomySource(Catalog("General:100 100,100 6001", "6001"));
            BindPrototypes();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void InventorySourceMesParsesAuthenticGoldChanceAndBuyPrototypeShape()
        {
            InventorySourceCatalog catalog = Catalog("Smith:500,1000 75,6001 10,6002", "6001 6003");
            Assert.That(catalog.TryGet(1, out InventorySourceDefinition source), Is.True);
            Assert.That((source.Name, source.MinimumGold, source.MaximumGold, source.Entries.Count),
                Is.EqualTo(("Smith", 500, 1000, 2)));
            Assert.That((source.Entries[0].ChancePercent, source.Entries[0].PrototypeNumber),
                Is.EqualTo((75, 6021)));
            Assert.That(source.AcceptsPrototype(6021), Is.True);
            Assert.That(source.AcceptsPrototype(6022), Is.False);
        }

        [Test]
        public void BuyPriceUsesExactWorthMarkupHaggleAndReactionArithmetic()
        {
            PersistentObjectState item = Item(2, _merchant, ObjectType.Food, MerchandisePrototype, 100);
            EconomyPriceBreakdown price = _session.Economy.GetPrice(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, item.Identity);
            Assert.That((price.EffectiveWorth, price.RetailMultiplier, price.HaggleEffectiveness,
                    price.Reaction, price.ReactionPercent, price.UnitPrice),
                Is.EqualTo((100, 150, 10, 43, 114, 267)));
        }

        [Test]
        public void SellPriceUsesMarkupDiscountHaggleAndConditionInSourceOrder()
        {
            PersistentObjectState item = Item(3, _pc.Identity, ObjectType.Food, MerchandisePrototype,
                100, maximumHp: 20, hpDamage: 10);
            EconomyPriceBreakdown price = _session.Economy.GetPrice(
                EconomyTransactionDirection.SellToMerchant, _merchant, _pc.Identity, item.Identity);
            Assert.That((price.CurrentHitPoints, price.MaximumHitPoints, price.UnitPrice), Is.EqualTo((10, 20, 22)));
        }

        [Test]
        public void UnidentifiedItemUsesFixedThreeHundredWorthAndFloorRulesRemainInspectable()
        {
            PersistentObjectState item = Item(4, _merchant, ObjectType.Food, 6022, 1, itemFlags: 0);
            EconomyPriceBreakdown price = _session.Economy.GetPrice(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, item.Identity);
            Assert.That(price.Identified, Is.False);
            Assert.That(price.EffectiveWorth, Is.EqualTo(300));
            Assert.That(price.UnitPrice, Is.EqualTo(803));
        }

        [Test]
        public void BuyTransfersItemAndExactGoldThroughM3Authority()
        {
            PersistentObjectState item = Item(5, _merchant, ObjectType.Food, MerchandisePrototype, 100);
            _session.AddGold(_pc.Identity, 500);
            EconomyTransactionResult result = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, item.Identity));
            Assert.That(result.Succeeded, Is.True);
            Assert.That((result.QuotedTotal, result.GoldTransferred), Is.EqualTo((267, 267)));
            Assert.That(item.ParentIdentity, Is.EqualTo(_pc.Identity));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(233));
            Assert.That(_session.GetGold(_merchant), Is.EqualTo(267));
        }

        [Test]
        public void InsufficientFundsRejectBeforeItemOrGoldMutation()
        {
            PersistentObjectState item = Item(6, _merchant, ObjectType.Food, MerchandisePrototype, 100);
            _session.AddGold(_pc.Identity, 10);
            EconomyTransactionResult result = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, item.Identity));
            Assert.That(result.Failure, Is.EqualTo(EconomyFailure.InsufficientFunds));
            Assert.That(item.ParentIdentity, Is.EqualTo(_merchant));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(10));
            Assert.That(_session.GetGold(_merchant), Is.Zero);
        }

        [Test]
        public void InvalidMerchantAndQuantityRejectBeforeMutation()
        {
            PersistentObjectState item = Item(40, _merchant, ObjectType.Ammo,
                MerchandisePrototype, 100, quantity: 5);
            _session.AddGold(_pc.Identity, 1000);
            Assert.That(_session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _pc.Identity, _pc.Identity,
                item.Identity)).Failure, Is.EqualTo(EconomyFailure.InvalidMerchant));
            Assert.That(_session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity,
                item.Identity, 0)).Failure, Is.EqualTo(EconomyFailure.InvalidQuantity));
            Assert.That((item.ParentIdentity, item.StackQuantity, _session.GetGold(_pc.Identity)),
                Is.EqualTo((_merchant, (int?)5, 1000)));
        }

        [Test]
        public void CapacityFailureRejectsBuyWithoutItemOrGoldMutation()
        {
            PersistentObjectState item = Item(41, _merchant, ObjectType.Food,
                MerchandisePrototype, 100, unitWeight: 10001);
            _session.AddGold(_pc.Identity, 1000);
            EconomyTransactionResult result = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, item.Identity));
            Assert.That(result.Failure, Is.EqualTo(EconomyFailure.CapacityFailure));
            Assert.That(item.ParentIdentity, Is.EqualTo(_merchant));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(1000));
            Assert.That(_session.GetGold(_merchant), Is.Zero);
        }

        [Test]
        public void SellTransfersItemAndClampsPayoutToMerchantPurseIncludingZero()
        {
            PersistentObjectState item = Item(7, _pc.Identity, ObjectType.Food, MerchandisePrototype, 100);
            EconomyTransactionResult zero = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.SellToMerchant, _merchant, _pc.Identity, item.Identity));
            Assert.That(zero.Succeeded, Is.True);
            Assert.That((zero.QuotedTotal, zero.GoldTransferred), Is.EqualTo((45, 0)));
            Assert.That(item.ParentIdentity, Is.EqualTo(_merchant));

            PersistentObjectState second = Item(8, _pc.Identity, ObjectType.Food, MerchandisePrototype, 100);
            _session.AddGold(_merchant, 12);
            EconomyTransactionResult clamped = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.SellToMerchant, _merchant, _pc.Identity, second.Identity));
            Assert.That((clamped.QuotedTotal, clamped.GoldTransferred), Is.EqualTo((45, 12)));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(12));
        }

        [Test]
        public void UnsupportedBuyScriptFailsClosedAndExpertBypassesSourceVetoes()
        {
            _merchant = AddMerchant("G_22222222_2222_2222_2222_222222222222", 1, 150, buyScript: 77);
            PersistentObjectState item = Item(9, _pc.Identity, ObjectType.Food, 6023, 100);
            EconomyTransactionRequest request = new(EconomyTransactionDirection.SellToMerchant,
                _merchant, _pc.Identity, item.Identity);
            Assert.That(_session.Economy.Preview(request).Failure, Is.EqualTo(EconomyFailure.UnsupportedBuyScript));
            SetHaggleTraining(SkillTrainingLevel.Expert);
            Assert.That(_session.Economy.Preview(request).Succeeded, Is.True);
        }

        [Test]
        public void MasterBypassesWontSellWhileOrdinaryBuyerCannot()
        {
            PersistentObjectState item = Item(10, _merchant, ObjectType.Food, MerchandisePrototype, 100,
                itemFlags: EconomyStateService.ItemFlagIdentified | EconomyStateService.ItemFlagWontSell);
            var request = new EconomyTransactionRequest(EconomyTransactionDirection.BuyFromMerchant,
                _merchant, _pc.Identity, item.Identity);
            Assert.That(_session.Economy.Preview(request).Failure, Is.EqualTo(EconomyFailure.ItemRejected));
            SetHaggleTraining(SkillTrainingLevel.Master);
            Assert.That(_session.Economy.Preview(request).Failure, Is.EqualTo(EconomyFailure.InsufficientFunds));
        }

        [Test]
        public void StolenGoodsRequireFenceFlag()
        {
            PersistentObjectState item = Item(11, _pc.Identity, ObjectType.Food, MerchandisePrototype, 100,
                itemFlags: EconomyStateService.ItemFlagIdentified | 0x8000);
            var request = new EconomyTransactionRequest(EconomyTransactionDirection.SellToMerchant,
                _merchant, _pc.Identity, item.Identity);
            Assert.That(_session.Economy.Preview(request).Failure, Is.EqualTo(EconomyFailure.ItemRejected));
            ArcanumObjectId fence = AddMerchant("G_33333333_3333_3333_3333_333333333333", 1, 150,
                npcFlags: EconomyStateService.NpcFlagFence);
            request = new EconomyTransactionRequest(EconomyTransactionDirection.SellToMerchant,
                fence, _pc.Identity, item.Identity);
            Assert.That(_session.Economy.Preview(request).Succeeded, Is.True);
        }

        [Test]
        public void PartialStackPurchasePreservesQuantityAndTransfersRequestedAmount()
        {
            PersistentObjectState ammo = Item(12, _merchant, ObjectType.Ammo, MerchandisePrototype, 10,
                quantity: 10);
            _session.AddGold(_pc.Identity, 1000);
            EconomyTransactionResult result = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, ammo.Identity, 4));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.QuotedTotal, Is.EqualTo(result.Price.UnitPrice * 4));
            Assert.That(result.GoldTransferred, Is.EqualTo(result.QuotedTotal));
            Assert.That(ammo.StackQuantity, Is.EqualTo(6));
            Assert.That(_session.States.Values.Single(value => value.Type == ObjectType.Ammo
                && value.ParentIdentity == _pc.Identity).StackQuantity, Is.EqualTo(4));
        }

        [Test]
        public void PartialStackSalePreservesQuantityAndTransfersExactAffordableGold()
        {
            PersistentObjectState ammo = Item(42, _pc.Identity, ObjectType.Ammo,
                MerchandisePrototype, 10, quantity: 10);
            _session.AddGold(_merchant, 1000);
            EconomyTransactionResult result = _session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.SellToMerchant, _merchant, _pc.Identity, ammo.Identity, 4));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.QuotedTotal, Is.EqualTo(result.Price.UnitPrice * 4));
            Assert.That(result.GoldTransferred, Is.EqualTo(result.QuotedTotal));
            Assert.That(ammo.StackQuantity, Is.EqualTo(6));
            Assert.That(_session.States.Values.Single(value => value.Type == ObjectType.Ammo
                && value.ParentIdentity == _merchant).StackQuantity, Is.EqualTo(4));
            Assert.That(_session.GetGold(_pc.Identity), Is.EqualTo(result.QuotedTotal));
        }

        [Test]
        public void HaggleTrainingImprovesBuyAndSellPricesThroughM4State()
        {
            PersistentObjectState buyItem = Item(43, _merchant, ObjectType.Food,
                MerchandisePrototype, 100);
            PersistentObjectState sellItem = Item(44, _pc.Identity, ObjectType.Food,
                MerchandisePrototype, 100);
            int ordinaryBuy = _session.Economy.GetPrice(EconomyTransactionDirection.BuyFromMerchant,
                _merchant, _pc.Identity, buyItem.Identity).UnitPrice;
            int ordinarySell = _session.Economy.GetPrice(EconomyTransactionDirection.SellToMerchant,
                _merchant, _pc.Identity, sellItem.Identity).UnitPrice;
            SetHaggleTraining(SkillTrainingLevel.Master);
            EconomyPriceBreakdown trainedBuy = _session.Economy.GetPrice(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, buyItem.Identity);
            EconomyPriceBreakdown trainedSell = _session.Economy.GetPrice(
                EconomyTransactionDirection.SellToMerchant, _merchant, _pc.Identity, sellItem.Identity);
            Assert.That(trainedBuy.HaggleTraining, Is.EqualTo(SkillTrainingLevel.Master));
            Assert.That(trainedBuy.UnitPrice, Is.LessThan(ordinaryBuy));
            Assert.That(trainedSell.UnitPrice, Is.GreaterThan(ordinarySell));
        }

        [Test]
        public void NearbySubstituteInventoryIsAuthoritativeAndDistantOneFailsClosed()
        {
            ArcanumObjectId container = AddContainer(20, new Vector2(4, 1), 1);
            ArcanumObjectId shopkeeper = AddMerchant("G_44444444_4444_4444_4444_444444444444", 0, 150,
                substitute: container);
            PersistentObjectState ware = Item(13, container, ObjectType.Food, MerchandisePrototype, 100);
            Assert.That(_session.Economy.TryResolveMerchant(shopkeeper, out ArcanumObjectId owner, out _), Is.True);
            Assert.That(owner, Is.EqualTo(container));
            Assert.That(_session.Economy.GetMerchantWares(shopkeeper).Single().Identity, Is.EqualTo(ware.Identity));

            ArcanumObjectId distant = AddContainer(21, new Vector2(50, 50), 1);
            ArcanumObjectId invalid = AddMerchant("G_55555555_5555_5555_5555_555555555555", 0, 150,
                substitute: distant);
            Assert.That(_session.Economy.TryResolveMerchant(invalid, out _, out _), Is.False);
        }

        [Test]
        public void DeterministicRestockCreatesSourceStockAndSchedulesTwelveToTwentyFourHours()
        {
            _session.Economy.SetRandomSource(new FixedRandom(100));
            Assert.That(_session.Economy.RestockNow(_merchant, out string failure), Is.True, failure);
            Assert.That(_session.Economy.GetMerchantWares(_merchant).Any(value => value.PrototypeNumber == MerchandisePrototype), Is.True);
            EconomyMerchantSaveData state = _session.Economy.ExportSaveData().Merchants.Single();
            Assert.That(state.NextRestockAtMilliseconds,
                Is.InRange(EconomyStateService.MinimumRestockDelayMilliseconds,
                    EconomyStateService.MaximumRestockDelayMilliseconds));
            Assert.That(state.GeneratedIdentities, Is.Not.Empty);
        }

        [Test]
        public void RestockReplacesNonPersistentStockAndPreservesPersistentStock()
        {
            PersistentObjectState replaced = Item(30, _merchant, ObjectType.Food,
                MerchandisePrototype, 100);
            PersistentObjectState preserved = Item(31, _merchant, ObjectType.Food,
                MerchandisePrototype, 100, itemFlags: EconomyStateService.ItemFlagIdentified
                    | EconomyStateService.ItemFlagPersistent);
            _session.Economy.SetRandomSource(new FixedRandom(100));
            Assert.That(_session.Economy.RestockNow(_merchant, out string failure), Is.True, failure);
            Assert.That(_session.TryGetObjectState(replaced.Identity, out _), Is.False);
            Assert.That(_session.TryGetObjectState(preserved.Identity, out _), Is.True);
            Assert.That(_session.Economy.GetMerchantWares(_merchant)
                .Any(value => value.PrototypeNumber == MerchandisePrototype), Is.True);
        }

        [Test]
        public void SubstitutePurseNormalizationDoesNotPersistStaleGeneratedIdentities()
        {
            ArcanumObjectId container = AddContainer(32, new Vector2(4, 1), 1);
            ArcanumObjectId shopkeeper = AddMerchant(
                "G_66666666_6666_6666_6666_666666666666", 0, 150, substitute: container);
            _session.Economy.SetRandomSource(new FixedRandom(100));
            Assert.That(_session.Economy.RestockNow(shopkeeper, out string failure), Is.True, failure);
            PersistentObjectState ware = _session.Economy.GetMerchantWares(shopkeeper)
                .Single(value => value.PrototypeNumber == MerchandisePrototype);
            _session.AddGold(_pc.Identity, 1000);
            Assert.That(_session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, shopkeeper, _pc.Identity,
                ware.Identity)).Succeeded, Is.True);
            EconomyMerchantSaveData runtime = _session.Economy.ExportSaveData().Merchants
                .Single(value => value.MerchantIdentity == shopkeeper.Key);
            Assert.That(runtime.GeneratedIdentities.All(key =>
                ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity)
                && _session.TryGetObjectState(identity, out PersistentObjectState state)
                && state.ParentIdentity == container), Is.True);
            SessionLoadResult loaded = _session.SaveGames.LoadJson(
                _session.SaveGames.SerializeCurrentSession());
            Assert.That(loaded.Succeeded, Is.True, loaded.Message);
        }

        [Test]
        public void SaveV1RestoresCommittedTradeAndEconomyScheduleWithoutPendingTransaction()
        {
            PersistentObjectState item = Item(14, _merchant, ObjectType.Food, MerchandisePrototype, 100);
            _session.AddGold(_pc.Identity, 500);
            Assert.That(_session.Economy.Execute(new EconomyTransactionRequest(
                EconomyTransactionDirection.BuyFromMerchant, _merchant, _pc.Identity, item.Identity)).Succeeded, Is.True);
            _session.Economy.SetRandomSource(new FixedRandom(100));
            Assert.That(_session.Economy.RestockNow(_merchant, out _), Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.TryGetObjectState(item.Identity, out PersistentObjectState restored), Is.True);
            Assert.That(restored.ParentIdentity, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Economy.ExportSaveData().Merchants.Single().NextRestockAtMilliseconds,
                Is.GreaterThan(0));
        }

        private void BindPrototypes()
        {
            var gold = new ObjectProtoInfo(9056, ObjectType.Gold, 0x50000000u) { GoldQuantity = 1 };
            var identified = new ObjectProtoInfo(MerchandisePrototype, ObjectType.Ammo, 0x50000000u,
                worth: 100) { AmmoQuantity = 1, ItemComplexity = 0 };
            var unidentified = new ObjectProtoInfo(6022, ObjectType.Food, 0x50000000u,
                worth: 1) { ItemComplexity = 10 };
            var rejected = new ObjectProtoInfo(6023, ObjectType.Food, 0x50000000u,
                worth: 100) { ItemComplexity = 0 };
            _session.BindPrototypeSource(number => number == 9056 ? gold : number == MerchandisePrototype ? identified
                : number == 6022 ? unidentified : number == 6023 ? rejected : null);
        }

        private ArcanumObjectId AddMerchant(string key, int sourceId, int multiplier, int npcFlags = 0,
            int buyScript = 0, ArcanumObjectId substitute = default)
        {
            ArcanumObjectId identity = Parse(key);
            var source = new ObjectInstance(ObjectType.Npc, 28001, Location(2, 1), 0x28100000u,
                0, 0, oid: GuidBytes(key));
            _session.GetOrCreate(source, identity, Sector, source.CurrentArtId.Value, false, false,
                retailPriceMultiplier: multiplier, inventorySourceId: sourceId,
                substituteInventoryIdentity: substitute, npcFlags: npcFlags, buyObjectScriptNum: buyScript);
            int[] stats = Stats();
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, 0, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 28001,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0, null, 0, null, 0, null, 0));
            return identity;
        }

        private ArcanumObjectId AddContainer(ulong value, Vector2 tile, int sourceId)
        {
            ArcanumObjectId identity = ArcanumObjectId.CreateAuthored((uint)value);
            var source = new ObjectInstance(ObjectType.Container, 3001, Location((int)tile.x, (int)tile.y),
                0xA0000000u, 0, 0);
            _session.GetOrCreate(source, identity, Sector, source.CurrentArtId.Value, false, false,
                inventorySourceId: sourceId);
            return identity;
        }

        private PersistentObjectState Item(ulong value, ArcanumObjectId owner, ObjectType type, int prototype,
            int worth, int quantity = 0, int itemFlags = EconomyStateService.ItemFlagIdentified,
            int maximumHp = 0, int hpDamage = 0, int unitWeight = 0)
        {
            ArcanumObjectId identity = ArcanumObjectId.CreateAuthored((uint)(100 + value));
            var source = new ObjectInstance(type, prototype, null, 0x50000000u, 0, 0,
                parentOid: Bytes(owner), worth: worth);
            return _session.GetOrCreate(source, identity, Sector, source.CurrentArtId.Value, false, false,
                itemFlags: itemFlags, stackQuantity: quantity > 0 ? quantity : null, sourceWorth: worth,
                maximumHitPoints: maximumHp, hitPointDamage: hpDamage, unitWeight: unitWeight);
        }

        private void SetHaggleTraining(SkillTrainingLevel training)
        {
            _session.Characters.SetEffectModifier(_pc.Identity, "M11A-WP", CharacterAttribute.Willpower, 20);
            int[] points = new int[CharacterSkillRules.SkillCount];
            var levels = new SkillTrainingLevel[CharacterSkillRules.SkillCount];
            points[(int)CharacterSkill.Haggle] = 5;
            levels[(int)CharacterSkill.Haggle] = training;
            _session.Progression.Get(_pc.Identity).RestoreSkills(points, levels);
        }

        private static InventorySourceCatalog Catalog(string source, string buy)
            => InventorySourceCatalog.FromMes(MesReader.Read("{1}{" + source + "}"),
                MesReader.Read("{1}{" + buy + "}"));
        private static int[] Stats()
        { var s = new int[CharacterAttributeSet.SourceStatArrayCount]; for (int i = 0; i < CharacterAttributeSet.Count; i++) s[i] = 8;
          s[CharacterProgressionSource.LevelSourceSlot] = 1; s[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
          s[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human; return s; }
        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);
        private static ArcanumObjectId Parse(string key)
        { ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId id); return id; }
        private static byte[] GuidBytes(string key)
        { string c = key.Substring(2).Replace("_", ""); var b = new byte[24]; b[0] = (byte)ArcanumObjectIdType.Guid;
          for (int i = 0; i < 16; i++) b[8 + i] = Convert.ToByte(c.Substring(i * 2, 2), 16); return b; }
        private static byte[] Bytes(ArcanumObjectId id)
        {
            if (id == ProductionPlayerLifecycle.DefaultPlayerIdentity)
            { var b = new byte[24]; b[0] = (byte)ArcanumObjectIdType.Guid;
              Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0, b, 8, 16); return b; }
            if (id.Type == ArcanumObjectIdType.Guid) return GuidBytes(id.Key);
            if (id.Type == ArcanumObjectIdType.Authored)
            {
                uint value = uint.Parse(id.Key.Substring(2), System.Globalization.NumberStyles.HexNumber);
                var bytes = new byte[ArcanumObjectId.SerializedSize];
                bytes[0] = (byte)ArcanumObjectIdType.Authored;
                for (int index = 0; index < 4; index++) bytes[8 + index] = (byte)(value >> (index * 8));
                return bytes;
            }
            if (id.TryGetSessionDynamicSequence(out ulong sequence))
            {
                var bytes = new byte[ArcanumObjectId.SerializedSize];
                bytes[0] = (byte)ArcanumObjectIdType.SessionDynamic;
                for (int index = 0; index < 8; index++) bytes[8 + index] = (byte)(sequence >> (index * 8));
                return bytes;
            }
            return null;
        }

        private sealed class FixedRandom : IEconomyRandom
        {
            private readonly int _percent;
            public FixedRandom(int percent) => _percent = percent;
            public int NextInclusive(int minimumInclusive, int maximumInclusive)
            {
                if (minimumInclusive == 1 && maximumInclusive == 100) return _percent;
                return minimumInclusive;
            }
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
