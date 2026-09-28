using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Economy
{
    public enum EconomyTransactionDirection { BuyFromMerchant, SellToMerchant }
    public enum EconomyGameDifficulty { Easy, Normal, Hard }

    public enum EconomyFailure
    {
        None, InvalidMerchant, MerchantUnavailable, InvalidCustomer, InvalidItem, InvalidQuantity,
        ItemNotOwned, ItemRejected, UnsupportedBuyScript, InsufficientFunds, CapacityFailure,
        InventoryFailure, ArithmeticOverflow, InventorySourceUnavailable,
    }

    public readonly struct EconomyTransactionRequest
    {
        public EconomyTransactionDirection Direction { get; }
        public ArcanumObjectId Merchant { get; }
        public ArcanumObjectId Customer { get; }
        public ArcanumObjectId Item { get; }
        public int Quantity { get; }
        public EconomyTransactionRequest(EconomyTransactionDirection direction, ArcanumObjectId merchant,
            ArcanumObjectId customer, ArcanumObjectId item, int quantity = 1)
        { Direction = direction; Merchant = merchant; Customer = customer; Item = item; Quantity = quantity; }
    }

    public readonly struct EconomyPriceBreakdown
    {
        public EconomyTransactionDirection Direction { get; }
        public int RawWorth { get; }
        public bool Identified { get; }
        public int EffectiveWorth { get; }
        public int RetailMultiplier { get; }
        public int HaggleRank { get; }
        public int HaggleEffectiveness { get; }
        public SkillTrainingLevel HaggleTraining { get; }
        public int Reaction { get; }
        public int ReactionPercent { get; }
        public int CurrentHitPoints { get; }
        public int MaximumHitPoints { get; }
        public int UnitPrice { get; }
        internal EconomyPriceBreakdown(EconomyTransactionDirection direction, int rawWorth, bool identified,
            int effectiveWorth, int retailMultiplier, int haggleRank, int haggleEffectiveness,
            SkillTrainingLevel haggleTraining, int reaction, int reactionPercent,
            int currentHitPoints, int maximumHitPoints, int unitPrice)
        {
            Direction = direction; RawWorth = rawWorth; Identified = identified; EffectiveWorth = effectiveWorth;
            RetailMultiplier = retailMultiplier; HaggleRank = haggleRank; HaggleEffectiveness = haggleEffectiveness;
            HaggleTraining = haggleTraining; Reaction = reaction; ReactionPercent = reactionPercent;
            CurrentHitPoints = currentHitPoints; MaximumHitPoints = maximumHitPoints; UnitPrice = unitPrice;
        }
    }

    public readonly struct EconomyTransactionResult
    {
        public EconomyFailure Failure { get; }
        public EconomyTransactionRequest Request { get; }
        public EconomyPriceBreakdown Price { get; }
        public int QuotedTotal { get; }
        public int GoldTransferred { get; }
        public int QuantityTransferred { get; }
        public string Detail { get; }
        public bool Succeeded => Failure == EconomyFailure.None;
        internal EconomyTransactionResult(EconomyFailure failure, EconomyTransactionRequest request,
            EconomyPriceBreakdown price = default, int quotedTotal = 0, int goldTransferred = 0,
            int quantityTransferred = 0, string detail = null)
        {
            Failure = failure; Request = request; Price = price; QuotedTotal = quotedTotal;
            GoldTransferred = goldTransferred; QuantityTransferred = quantityTransferred; Detail = detail;
        }
    }

    public sealed class InventorySourceDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public int MinimumGold { get; }
        public int MaximumGold { get; }
        public IReadOnlyList<InventorySourceEntry> Entries { get; }
        public bool BuysAll { get; }
        public IReadOnlyCollection<int> AcceptedBasicPrototypes { get; }
        internal InventorySourceDefinition(int id, string name, int minimumGold, int maximumGold,
            IReadOnlyList<InventorySourceEntry> entries, bool buysAll, IReadOnlyCollection<int> accepted)
        { Id = id; Name = name; MinimumGold = minimumGold; MaximumGold = maximumGold;
          Entries = entries; BuysAll = buysAll; AcceptedBasicPrototypes = accepted; }
        public bool AcceptsPrototype(int prototypeNumber)
            => BuysAll || AcceptedBasicPrototypes.Contains(prototypeNumber - 20);
    }

    public readonly struct InventorySourceEntry
    {
        public int ChancePercent { get; }
        public int BasicPrototype { get; }
        public int PrototypeNumber => checked(BasicPrototype + 20);
        internal InventorySourceEntry(int chancePercent, int basicPrototype)
        { ChancePercent = chancePercent; BasicPrototype = basicPrototype; }
    }

    public sealed class InventorySourceCatalog
    {
        private readonly Dictionary<int, InventorySourceDefinition> _definitions;
        public IReadOnlyDictionary<int, InventorySourceDefinition> Definitions => _definitions;
        private InventorySourceCatalog(Dictionary<int, InventorySourceDefinition> definitions)
            => _definitions = definitions;
        public bool TryGet(int id, out InventorySourceDefinition definition)
            => _definitions.TryGetValue(id, out definition);

        public static InventorySourceCatalog FromMes(MesFile sources, MesFile accepted)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var result = new Dictionary<int, InventorySourceDefinition>();
            foreach (KeyValuePair<int, string> row in sources.Entries)
            {
                if (row.Key <= 0 || string.IsNullOrWhiteSpace(row.Value)) continue;
                string[] nameAndBody = row.Value.Split(new[] { ':' }, 2);
                if (nameAndBody.Length != 2) continue;
                int[] values = Ints(nameAndBody[1]);
                if (values.Length < 2) continue;
                var entries = new List<InventorySourceEntry>();
                for (int index = 2; index + 1 < values.Length; index += 2)
                {
                    if (values[index] >= 0 && values[index] <= 100 && values[index + 1] >= 0)
                        entries.Add(new InventorySourceEntry(values[index], values[index + 1]));
                }
                bool all = false;
                var buy = new HashSet<int>();
                string buyRow = accepted?.Get(row.Key);
                if (!string.IsNullOrWhiteSpace(buyRow))
                {
                    all = string.Equals(buyRow.Trim(), "ALL", StringComparison.OrdinalIgnoreCase);
                    if (!all) foreach (int value in Ints(buyRow)) if (value >= 0) buy.Add(value);
                }
                result[row.Key] = new InventorySourceDefinition(row.Key, nameAndBody[0].Trim(),
                    Math.Max(0, values[0]), Math.Max(Math.Max(0, values[0]), values[1]), entries, all, buy);
            }
            return new InventorySourceCatalog(result);
        }

        private static int[] Ints(string value)
            => (value ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n', ',' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int number) ? (int?)number : null)
                .Where(number => number.HasValue).Select(number => number.Value).ToArray();
    }

    public interface IEconomyRandom { int NextInclusive(int minimumInclusive, int maximumInclusive); }

    public sealed class EconomyStateService
    {
        public const long MinimumRestockDelayMilliseconds = 43_200_000;
        public const long MaximumRestockDelayMilliseconds = 86_400_000;
        public const int ItemFlagIdentified = 0x1;
        public const int ItemFlagWontSell = 0x2;
        public const int ItemFlagPersistent = 0x2000;
        public const int NpcFlagFence = 0x4000;

        private sealed class SystemEconomyRandom : IEconomyRandom
        {
            private readonly System.Random _random = new();
            public int NextInclusive(int minimumInclusive, int maximumInclusive)
                => _random.Next(minimumInclusive, checked(maximumInclusive + 1));
        }
        private sealed class MerchantRuntime
        {
            public ArcanumObjectId Merchant;
            public ArcanumObjectId InventoryOwner;
            public int SourceId;
            public long NextRestockAt;
            public readonly HashSet<ArcanumObjectId> Generated = new();
        }

        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, MerchantRuntime> _merchants = new();
        private InventorySourceCatalog _catalog;
        private IEconomyRandom _random = new SystemEconomyRandom();
        private Func<EconomyGameDifficulty> _difficulty = () => EconomyGameDifficulty.Normal;
        private Func<ArcanumObjectId, ArcanumObjectId, ArcanumObjectId, bool> _buyObjectAcceptance;

        public EconomyStateService(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _world.SourceTime.Advanced += OnTimeAdvanced;
        }
        public void BindInventorySources(InventorySourceCatalog catalog)
            => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        public void BindDifficultyProvider(Func<EconomyGameDifficulty> provider)
            => _difficulty = provider ?? throw new ArgumentNullException(nameof(provider));
        public void BindBuyObjectAcceptance(Func<ArcanumObjectId, ArcanumObjectId, ArcanumObjectId, bool> evaluator)
            => _buyObjectAcceptance = evaluator;
        public void SetRandomSource(IEconomyRandom random)
            => _random = random ?? throw new ArgumentNullException(nameof(random));
        public void ResetRandomSource() => _random = new SystemEconomyRandom();

        public bool TryResolveMerchant(ArcanumObjectId merchantIdentity, out ArcanumObjectId inventoryOwner,
            out InventorySourceDefinition source)
        {
            inventoryOwner = default; source = null;
            if (!_world.TryGetObjectState(merchantIdentity, out PersistentObjectState merchant)
                || merchant.Type != ObjectType.Npc || _world.Party.IsMember(merchantIdentity)
                || _world.Vitality.TryGet(merchantIdentity, out _) && _world.Vitality.IsDead(merchantIdentity))
                return false;
            PersistentObjectState owner = merchant;
            if (merchant.SubstituteInventoryIdentity.IsPersistent
                && _world.TryGetObjectState(merchant.SubstituteInventoryIdentity, out PersistentObjectState substitute)
                && substitute.Type == ObjectType.Container && WithinTwentyTiles(merchant, substitute)) owner = substitute;
            if (owner.InventorySourceId <= 0 || _catalog == null || !_catalog.TryGet(owner.InventorySourceId, out source))
                return false;
            inventoryOwner = owner.Identity;
            EnsureRuntime(merchantIdentity, inventoryOwner, source.Id);
            return true;
        }

        public IReadOnlyList<PersistentObjectState> GetMerchantWares(ArcanumObjectId merchant)
        {
            if (!TryResolveMerchant(merchant, out ArcanumObjectId owner, out _))
                return Array.Empty<PersistentObjectState>();
            return _world.States.Values.Where(item => item.Type != ObjectType.Gold
                    && item.Placement.Kind == ObjectPlacementKind.Contained && item.ParentIdentity == owner)
                .OrderBy(item => item.Identity.Key, StringComparer.Ordinal).ToArray();
        }

        public EconomyTransactionResult Preview(EconomyTransactionRequest request)
        {
            if (!TryResolveMerchant(request.Merchant, out ArcanumObjectId merchantInventory,
                    out InventorySourceDefinition source)) return Fail(EconomyFailure.InvalidMerchant, request);
            if (_world.PlayerState == null || request.Customer != _world.PlayerState.Identity)
                return Fail(EconomyFailure.InvalidCustomer, request);
            if (!_world.TryGetObjectState(request.Item, out PersistentObjectState item)
                || !WorldMapSessionCoordinator.IsItemType(item.Type) || item.Type == ObjectType.Gold)
                return Fail(EconomyFailure.InvalidItem, request);
            int available = item.StackQuantity ?? 1;
            if (request.Quantity < 1 || request.Quantity > available)
                return Fail(EconomyFailure.InvalidQuantity, request);
            ArcanumObjectId expectedOwner = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? merchantInventory : request.Customer;
            if (item.ParentIdentity != expectedOwner
                || item.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped))
                return Fail(EconomyFailure.ItemNotOwned, request);
            SkillTrainingLevel training = _world.Progression.GetTrainingLevel(request.Customer, CharacterSkill.Haggle);
            EconomyFailure acceptance = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? ValidateBuy(item, merchantInventory, training) : ValidateSell(request, item, source, training);
            if (acceptance != EconomyFailure.None) return Fail(acceptance, request);
            EconomyPriceBreakdown price;
            int total;
            try { price = CalculatePrice(request.Direction, request.Merchant, request.Customer, item, training);
                  total = checked(price.UnitPrice * request.Quantity); }
            catch (OverflowException) { return Fail(EconomyFailure.ArithmeticOverflow, request); }
            if (request.Direction == EconomyTransactionDirection.BuyFromMerchant
                && _world.GetGold(request.Customer) < total)
                return Fail(EconomyFailure.InsufficientFunds, request, price, total);
            ArcanumObjectId destination = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                ? request.Customer : merchantInventory;
            WorldMapSessionCoordinator.DialogueInventorySnapshot snapshot = _world.CaptureDialogueInventorySnapshot();
            InventoryTransferResult transfer = TransferQuantity(item, request.Quantity, destination);
            _world.RestoreDialogueInventorySnapshot(snapshot);
            if (!transfer.Succeeded)
                return Fail(transfer.Code is InventoryResultCode.NoRoom or InventoryResultCode.TooHeavy
                    ? EconomyFailure.CapacityFailure : EconomyFailure.InventoryFailure,
                    request, price, total, transfer.Code.ToString());
            return new EconomyTransactionResult(EconomyFailure.None, request, price, total);
        }

        public EconomyTransactionResult Execute(EconomyTransactionRequest request)
        {
            EconomyTransactionResult preview = Preview(request);
            if (!preview.Succeeded) return preview;
            TryResolveMerchant(request.Merchant, out ArcanumObjectId merchantInventory, out _);
            _world.TryGetObjectState(request.Item, out PersistentObjectState item);
            WorldMapSessionCoordinator.DialogueInventorySnapshot snapshot = _world.CaptureDialogueInventorySnapshot();
            try
            {
                NormalizeStoreGold(merchantInventory, request.Merchant);
                ArcanumObjectId destination = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                    ? request.Customer : merchantInventory;
                InventoryTransferResult transfer = TransferQuantity(item, request.Quantity, destination);
                if (!transfer.Succeeded) throw new InvalidOperationException(transfer.Code.ToString());
                int payment = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                    ? preview.QuotedTotal : Math.Min(preview.QuotedTotal, _world.GetGold(request.Merchant));
                if (payment > 0)
                {
                    ArcanumObjectId payer = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                        ? request.Customer : request.Merchant;
                    ArcanumObjectId recipient = request.Direction == EconomyTransactionDirection.BuyFromMerchant
                        ? request.Merchant : request.Customer;
                    if (!_world.TryTransferGold(payer, recipient, payment, out string failure))
                        throw new InvalidOperationException(failure);
                }
                if (_merchants.TryGetValue(request.Merchant, out MerchantRuntime runtime))
                    runtime.Generated.RemoveWhere(identity => !_world.TryGetObjectState(identity,
                            out PersistentObjectState generated)
                        || generated.ParentIdentity != merchantInventory);
                return new EconomyTransactionResult(EconomyFailure.None, request, preview.Price,
                    preview.QuotedTotal, payment, request.Quantity);
            }
            catch (Exception ex)
            {
                _world.RestoreDialogueInventorySnapshot(snapshot);
                return Fail(EconomyFailure.InventoryFailure, request, preview.Price, preview.QuotedTotal, ex.Message);
            }
        }

        public EconomyPriceBreakdown GetPrice(EconomyTransactionDirection direction,
            ArcanumObjectId merchant, ArcanumObjectId customer, ArcanumObjectId item)
        {
            if (!_world.TryGetObjectState(item, out PersistentObjectState state))
                throw new KeyNotFoundException($"No item {item}.");
            SkillTrainingLevel training = _world.Progression.GetTrainingLevel(customer, CharacterSkill.Haggle);
            return CalculatePrice(direction, merchant, customer, state, training);
        }

        public bool RestockNow(ArcanumObjectId merchantIdentity, out string failure)
        {
            failure = null;
            if (!TryResolveMerchant(merchantIdentity, out ArcanumObjectId owner, out InventorySourceDefinition source))
            { failure = "Merchant inventory source is unavailable."; return false; }
            MerchantRuntime runtime = EnsureRuntime(merchantIdentity, owner, source.Id);
            WorldMapSessionCoordinator.DialogueInventorySnapshot snapshot = _world.CaptureDialogueInventorySnapshot();
            try
            {
                foreach (PersistentObjectState stocked in _world.States.Values
                             .Where(value => value.Placement.Kind == ObjectPlacementKind.Contained
                                             && value.ParentIdentity == owner
                                             && (value.ItemFlags & ItemFlagPersistent) == 0)
                             .OrderBy(value => value.Identity.Key).ToArray())
                    _world.RemoveEconomyGeneratedObject(stocked.Identity, owner);
                runtime.Generated.Clear();
                if (source.MaximumGold > 0)
                {
                    int amount = _random.NextInclusive(source.MinimumGold, source.MaximumGold);
                    if (amount > 0)
                    {
                        HashSet<ArcanumObjectId> before = GoldIdentities(owner);
                        _world.AddGold(owner, amount);
                        foreach (ArcanumObjectId identity in GoldIdentities(owner))
                            if (!before.Contains(identity)) runtime.Generated.Add(identity);
                    }
                }
                foreach (InventorySourceEntry entry in source.Entries)
                {
                    if (_random.NextInclusive(1, 100) > entry.ChancePercent) continue;
                    ItemCreationResult created = _world.CreateItem(entry.PrototypeNumber,
                        ObjectPlacement.ContainedBy(owner));
                    if (!created.Succeeded) throw new InvalidOperationException(created.Code.ToString());
                    runtime.Generated.Add(created.State.Identity);
                }
                runtime.NextRestockAt = checked(_world.SourceTime.ElapsedMilliseconds
                    + _random.NextInclusive((int)MinimumRestockDelayMilliseconds,
                        (int)MaximumRestockDelayMilliseconds));
                return true;
            }
            catch (Exception ex)
            { _world.RestoreDialogueInventorySnapshot(snapshot); failure = ex.Message; return false; }
        }

        public EconomySaveData ExportSaveData()
            => new()
            {
                Merchants = _merchants.Values.OrderBy(value => value.Merchant.Key, StringComparer.Ordinal)
                    .Select(value => new EconomyMerchantSaveData
                    {
                        MerchantIdentity = value.Merchant.Key, InventoryOwnerIdentity = value.InventoryOwner.Key,
                        InventorySourceId = value.SourceId, NextRestockAtMilliseconds = value.NextRestockAt,
                        GeneratedIdentities = value.Generated.OrderBy(id => id.Key, StringComparer.Ordinal)
                            .Select(id => id.Key).ToList(),
                    }).ToList(),
            };

        internal void RestoreSaveData(EconomySaveData data)
        {
            if (data?.Merchants == null) return;
            foreach (EconomyMerchantSaveData value in data.Merchants)
            {
                ArcanumObjectId.TryParsePersistent(value.MerchantIdentity, out ArcanumObjectId merchant);
                ArcanumObjectId.TryParsePersistent(value.InventoryOwnerIdentity, out ArcanumObjectId owner);
                var runtime = new MerchantRuntime { Merchant = merchant, InventoryOwner = owner,
                    SourceId = value.InventorySourceId, NextRestockAt = value.NextRestockAtMilliseconds };
                foreach (string key in value.GeneratedIdentities)
                    if (ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity)) runtime.Generated.Add(identity);
                _merchants.Add(merchant, runtime);
            }
        }

        private EconomyPriceBreakdown CalculatePrice(EconomyTransactionDirection direction,
            ArcanumObjectId merchantIdentity, ArcanumObjectId customer, PersistentObjectState item,
            SkillTrainingLevel training)
        {
            _world.TryGetObjectState(merchantIdentity, out PersistentObjectState merchant);
            int rawWorth = item.SourceWorth;
            bool identified = (item.ItemFlags & ItemFlagIdentified) != 0
                              || (_world.ResolvePrototype(item.PrototypeNumber)?.ItemComplexity ?? 0) <= 0;
            int worth = identified ? Math.Max(2, rawWorth) : 300;
            int rank = _world.Progression.GetEffectiveSkillRank(customer, CharacterSkill.Haggle);
            int haggle = checked(4 * rank + 10);
            if (_world.Characters.GetEffectiveAttribute(customer, CharacterAttribute.Intelligence) >= 20) haggle += 10;
            haggle = _difficulty() switch
            { EconomyGameDifficulty.Easy => haggle * 3 / 2, EconomyGameDifficulty.Hard => haggle * 3 / 4, _ => haggle };
            haggle = Math.Max(0, Math.Min(95, haggle));
            int multiplier = merchant?.RetailPriceMultiplier ?? 0;
            int reaction = _world.Social.GetReaction(merchantIdentity, customer);
            int reactionPercent = ReactionPercent(reaction);
            int maximumHp = Math.Max(0, item.MaximumHitPoints);
            int currentHp = Math.Max(0, maximumHp - item.HitPointDamage);
            long price;
            if (direction == EconomyTransactionDirection.SellToMerchant)
            {
                long adjustedWorth = worth;
                if (multiplier > 100) adjustedWorth = adjustedWorth * (3 * (100 - multiplier) / 8 + 100) / 100;
                price = adjustedWorth * (haggle / 2 + 50) / 100;
                if (maximumHp > 0) price = price * currentHp / maximumHp;
                if (price < 0) price = 0;
                if (price == 1) price = 2;
            }
            else
            {
                price = worth + (long)worth * multiplier * (100 - haggle) / 10000;
                price = price * reactionPercent / 100;
                if (price <= worth) price = worth + 1L;
                if (price is 1 or 2) price = 3;
            }
            int unitPrice = checked((int)price);
            return new EconomyPriceBreakdown(direction, rawWorth, identified, worth, multiplier, rank, haggle,
                training, reaction, reactionPercent, currentHp, maximumHp, unitPrice);
        }

        private EconomyFailure ValidateBuy(PersistentObjectState item, ArcanumObjectId owner,
            SkillTrainingLevel training)
        {
            if (item.Placement.Kind == ObjectPlacementKind.Equipped && training < SkillTrainingLevel.Master)
                return EconomyFailure.ItemRejected;
            if ((item.ItemFlags & ItemFlagWontSell) != 0 && training < SkillTrainingLevel.Master)
                return EconomyFailure.ItemRejected;
            return item.ParentIdentity == owner ? EconomyFailure.None : EconomyFailure.ItemNotOwned;
        }

        private EconomyFailure ValidateSell(EconomyTransactionRequest request, PersistentObjectState item,
            InventorySourceDefinition source, SkillTrainingLevel training)
        {
            if (item.SourceWorth == 0 || item.Placement.Kind == ObjectPlacementKind.Equipped)
                return EconomyFailure.ItemRejected;
            if ((item.ItemFlags & 0x8000) != 0
                && (!_world.TryGetObjectState(request.Merchant, out PersistentObjectState merchant)
                    || (merchant.NpcFlags & NpcFlagFence) == 0)) return EconomyFailure.ItemRejected;
            if (training < SkillTrainingLevel.Expert)
            {
                if (_world.TryGetObjectState(request.Merchant, out PersistentObjectState merchantState)
                    && merchantState.BuyObjectScriptNum != 0)
                {
                    if (_buyObjectAcceptance == null) return EconomyFailure.UnsupportedBuyScript;
                    if (!_buyObjectAcceptance(request.Merchant, request.Customer, request.Item))
                        return EconomyFailure.ItemRejected;
                }
                if (!source.AcceptsPrototype(item.PrototypeNumber)) return EconomyFailure.ItemRejected;
            }
            return EconomyFailure.None;
        }

        private InventoryTransferResult TransferQuantity(PersistentObjectState item, int quantity,
            ArcanumObjectId destination)
        {
            if (!item.StackQuantity.HasValue || quantity == item.StackQuantity.Value)
                return _world.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(destination));
            StackSplitResult split = _world.SplitStack(item.Identity, quantity);
            if (!split.Succeeded)
                return new InventoryTransferResult(split.Code is StackResultCode.NoRoom ? InventoryResultCode.NoRoom
                    : split.Code is StackResultCode.TooHeavy ? InventoryResultCode.TooHeavy
                    : InventoryResultCode.InvalidDestination, item.Identity, item.Placement,
                    ObjectPlacement.ContainedBy(destination));
            return _world.TransferItem(split.CreatedState.Identity, split.CreatedState.Placement,
                ObjectPlacement.ContainedBy(destination));
        }

        private void NormalizeStoreGold(ArcanumObjectId inventoryOwner, ArcanumObjectId merchant)
        {
            if (inventoryOwner == merchant) return;
            int amount = _world.GetGold(inventoryOwner);
            if (amount > 0 && !_world.TryTransferGold(inventoryOwner, merchant, amount, out string failure))
                throw new InvalidOperationException(failure);
            if (_merchants.TryGetValue(merchant, out MerchantRuntime runtime))
                runtime.Generated.RemoveWhere(identity => !_world.TryGetObjectState(identity, out PersistentObjectState state)
                                                          || state.ParentIdentity != inventoryOwner);
        }

        private MerchantRuntime EnsureRuntime(ArcanumObjectId merchant, ArcanumObjectId owner, int sourceId)
        {
            if (_merchants.TryGetValue(merchant, out MerchantRuntime existing)) return existing;
            var runtime = new MerchantRuntime { Merchant = merchant, InventoryOwner = owner, SourceId = sourceId,
                NextRestockAt = checked(_world.SourceTime.ElapsedMilliseconds
                    + _random.NextInclusive((int)MinimumRestockDelayMilliseconds,
                        (int)MaximumRestockDelayMilliseconds)) };
            _merchants.Add(merchant, runtime);
            return runtime;
        }

        private void OnTimeAdvanced(SourceTimeAdvance advance)
        {
            foreach (MerchantRuntime merchant in _merchants.Values
                         .Where(value => value.NextRestockAt > 0 && value.NextRestockAt <= advance.TotalMilliseconds)
                         .OrderBy(value => value.Merchant.Key, StringComparer.Ordinal).ToArray())
            {
                if (_world.Party.IsMember(merchant.Merchant)
                    || _world.Vitality.TryGet(merchant.Merchant, out _) && _world.Vitality.IsDead(merchant.Merchant))
                { merchant.NextRestockAt = checked(advance.TotalMilliseconds + MinimumRestockDelayMilliseconds); continue; }
                RestockNow(merchant.Merchant, out _);
            }
        }

        private HashSet<ArcanumObjectId> GoldIdentities(ArcanumObjectId owner)
            => _world.States.Values.Where(item => item.Type == ObjectType.Gold
                    && item.Placement.Kind == ObjectPlacementKind.Contained && item.ParentIdentity == owner)
                .Select(item => item.Identity).ToHashSet();
        private static int ReactionPercent(int reaction)
        { if (reaction < 0) return 200; reaction = Math.Min(100, reaction);
          return reaction < 50 ? 2 * (100 - reaction) : 120 - 2 * reaction / 5; }
        private static bool WithinTwentyTiles(PersistentObjectState left, PersistentObjectState right)
        {
            if (left.Placement.Kind != ObjectPlacementKind.World || right.Placement.Kind != ObjectPlacementKind.World
                || !string.Equals(left.Placement.Sector, right.Placement.Sector, StringComparison.Ordinal)) return false;
            return Vector2.Distance(left.TilePosition, right.TilePosition) <= 20f;
        }
        private static EconomyTransactionResult Fail(EconomyFailure failure, EconomyTransactionRequest request,
            EconomyPriceBreakdown price = default, int total = 0, string detail = null)
            => new(failure, request, price, total, detail: detail);
    }
}
