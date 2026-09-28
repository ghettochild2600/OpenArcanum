using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M11AEconomyPlayModeValidation
{
    private const string MerchantSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string MerchantKey = "G_C626B82F_5190_2C40_995A_00BCB987F7A5";
    private static readonly ArcanumObjectId Merchant = Parse(MerchantKey);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M11A/Run Physical PlayMode Validation #&5")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M11A harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        WorldMapSessionCoordinator session = loader.Session;
        string baseline = null;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(MerchantSector), "authentic merchant sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            Check(session.TryGetObjectState(Merchant, out PersistentObjectState merchant)
                  && merchant.Type == ObjectType.Npc
                  && merchant.RetailPriceMultiplier == 200
                  && merchant.InventorySourceId == 7,
                "authentic source merchant resolves multiplier 200 and inventory source 7");
            bool hasStore = session.TryGetObjectState(merchant.SubstituteInventoryIdentity,
                out PersistentObjectState store);
            bool resolvedMerchant = session.Economy.TryResolveMerchant(Merchant,
                out ArcanumObjectId owner, out InventorySourceDefinition source);
            Check(resolvedMerchant && owner == Merchant && source.Id == 7,
                "production economy resolves the authentic merchant inventory; "
                + $"party={session.Party.IsMember(Merchant)}; dead={session.Vitality.TryGet(Merchant, out _) && session.Vitality.IsDead(Merchant)}; "
                + $"store={hasStore}; storeType={store?.Type}; storeSource={store?.InventorySourceId}; "
                + $"merchantTile={merchant.TilePosition}; storeTile={store?.TilePosition}");
            baseline = session.SaveGames.SerializeCurrentSession();

            var random = new CountingSourceRandom();
            session.Economy.SetRandomSource(random);
            Check(session.Economy.RestockNow(Merchant, out string restockFailure),
                "authentic inventory source restocks: " + restockFailure);
            Check(session.Economy.GetMerchantWares(Merchant).Count > 0,
                "authentic inventory source creates sale stock");
            EconomyMerchantSaveData scheduled = session.Economy.ExportSaveData().Merchants
                .Single(value => value.MerchantIdentity == Merchant.Key);
            Check(scheduled.NextRestockAtMilliseconds - session.SourceTime.ElapsedMilliseconds
                  == EconomyStateService.MinimumRestockDelayMilliseconds,
                "restock schedules the source minimum of twelve hours under deterministic RNG");

            session.AddGold(pc, 1000000);
            PersistentObjectState ware = session.Economy.GetMerchantWares(Merchant)
                .First(value => session.Economy.Preview(new EconomyTransactionRequest(
                    EconomyTransactionDirection.BuyFromMerchant, Merchant, pc, value.Identity)).Succeeded);
            EconomyTransactionRequest buyRequest = new(EconomyTransactionDirection.BuyFromMerchant,
                Merchant, pc, ware.Identity);
            EconomyTransactionResult quote = session.Economy.Preview(buyRequest);
            var existingPcItems = session.States.Values.Where(value => value.ParentIdentity == pc)
                .Select(value => value.Identity).ToHashSet();
            int pcGold = session.GetGold(pc);
            int merchantGold = session.GetGold(Merchant);
            EconomyTransactionResult bought = session.Economy.Execute(buyRequest);
            PersistentObjectState purchasedWare = session.States.Values.FirstOrDefault(value =>
                value.ParentIdentity == pc && value.PrototypeNumber == ware.PrototypeNumber
                && !existingPcItems.Contains(value.Identity));
            Check(bought.Succeeded && bought.GoldTransferred == quote.QuotedTotal
                  && purchasedWare?.ParentIdentity == pc
                  && session.GetGold(pc) == pcGold - quote.QuotedTotal
                  && session.GetGold(Merchant) == merchantGold + quote.QuotedTotal,
                "production buy transfers exact quoted gold and item through M3 authority; "
                + $"failure={bought.Failure}; detail={bought.Detail}; quoted={quote.QuotedTotal}; "
                + $"transferred={bought.GoldTransferred}; owner={purchasedWare?.ParentIdentity}; "
                + $"pcGold={pcGold}->{session.GetGold(pc)}; merchantGold={merchantGold}->{session.GetGold(Merchant)}");

            PersistentObjectState sellable = null;
            foreach (int basicPrototype in source.AcceptedBasicPrototypes)
            {
                ItemCreationResult candidate = session.CreateItem(basicPrototype + 20,
                    ObjectPlacement.ContainedBy(pc));
                if (candidate.Succeeded && candidate.State.SourceWorth > 0
                    && !candidate.State.StackQuantity.HasValue)
                { sellable = candidate.State; break; }
            }
            Check(sellable != null,
                "authentic inventor buy list supplies a production sell fixture");
            EconomyTransactionRequest sellRequest = new(EconomyTransactionDirection.SellToMerchant,
                Merchant, pc, sellable.Identity);
            EconomyTransactionResult sold = session.Economy.Execute(sellRequest);
            session.TryGetObjectState(sellable.Identity, out PersistentObjectState soldState);
            Check(sold.Succeeded && soldState?.ParentIdentity == Merchant
                  && sold.GoldTransferred <= sold.QuotedTotal,
                "production sell transfers the item and source-clamped merchant payout; "
                + $"failure={sold.Failure}; detail={sold.Detail}; proto={sellable.PrototypeNumber}; "
                + $"accepted={source.AcceptsPrototype(sellable.PrototypeNumber)}");

            EconomyTransactionRequest repurchaseRequest = new(EconomyTransactionDirection.BuyFromMerchant,
                Merchant, pc, sellable.Identity);
            EconomyTransactionResult repurchaseQuote = session.Economy.Preview(repurchaseRequest);
            Check(repurchaseQuote.Succeeded, "sold production item is available for repurchase");

            int allPcGold = session.GetGold(pc);
            Check(session.TryTransferGold(pc, Merchant, allPcGold, out string transferFailure),
                "validation can empty the customer purse: " + transferFailure);
            int emptyPcGold = session.GetGold(pc);
            int fundedMerchantGold = session.GetGold(Merchant);
            EconomyTransactionResult rejected = session.Economy.Execute(repurchaseRequest);
            session.TryGetObjectState(sellable.Identity, out PersistentObjectState rejectedState);
            Check(rejected.Failure == EconomyFailure.InsufficientFunds
                  && rejectedState?.ParentIdentity == Merchant
                  && session.GetGold(pc) == emptyPcGold
                  && session.GetGold(Merchant) == fundedMerchantGold,
                "insufficient funds rejects before item or gold mutation");

            session.AddGold(pc, repurchaseQuote.QuotedTotal + 100);
            EconomyTransactionResult committed = session.Economy.Execute(repurchaseRequest);
            session.TryGetObjectState(sellable.Identity, out PersistentObjectState committedState);
            Check(committed.Succeeded && committedState?.ParentIdentity == pc,
                "representative committed purchase is authoritative before rebuild/save");
            int committedPcGold = session.GetGold(pc);
            int committedMerchantGold = session.GetGold(Merchant);
            long committedSchedule = session.Economy.ExportSaveData().Merchants
                .Single(value => value.MerchantIdentity == Merchant.Key).NextRestockAtMilliseconds;

            foreach (GraphicsMode mode in new[]
                     { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.TryGetObjectState(sellable.Identity, out PersistentObjectState rebuiltItem)
                      && rebuiltItem.ParentIdentity == pc && session.GetGold(pc) == committedPcGold
                      && session.GetGold(Merchant) == committedMerchantGold
                      && session.Economy.ExportSaveData().Merchants.Single(value =>
                          value.MerchantIdentity == Merchant.Key).NextRestockAtMilliseconds
                      == committedSchedule,
                    $"{mode} presentation rebuild preserves economy authority");
            }

            string save = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(save).Succeeded, "economy V1 save reload succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.TryGetObjectState(sellable.Identity, out PersistentObjectState restoredWare)
                  && restoredWare.ParentIdentity == pc
                  && session.GetGold(pc) == committedPcGold
                  && session.GetGold(Merchant) == committedMerchantGold
                  && session.Economy.ExportSaveData().Merchants.Single(value =>
                      value.MerchantIdentity == Merchant.Key).NextRestockAtMilliseconds
                  == committedSchedule,
                "V1 restores committed item, gold, and restock schedule without a pending transaction");

            random = new CountingSourceRandom();
            session.Economy.SetRandomSource(random);
            EconomyMerchantSaveData beforeDue = session.Economy.ExportSaveData().Merchants
                .Single(value => value.MerchantIdentity == Merchant.Key);
            int delta = checked((int)(beforeDue.NextRestockAtMilliseconds
                                      - session.SourceTime.ElapsedMilliseconds));
            Check(delta > 0, "restock deadline remains in the future after load");
            session.SourceTime.Advance(delta);
            int callsAtBoundary = random.Calls;
            EconomyMerchantSaveData afterDue = session.Economy.ExportSaveData().Merchants
                .Single(value => value.MerchantIdentity == Merchant.Key);
            Check(callsAtBoundary > 0 && afterDue.NextRestockAtMilliseconds
                  > session.SourceTime.ElapsedMilliseconds,
                "time authority triggers exactly one due restock and advances its deadline");
            session.SourceTime.Advance(1);
            Check(random.Calls == callsAtBoundary,
                "a later time tick does not duplicate the completed restock");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "validation cleanup restores the authoritative baseline");
            yield return null;
            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M11A PHYSICAL VALIDATION PASS: authenticMerchant=source7+multiplier200; "
                      + "restock=stock+12h+exactlyOnce; trade=buy+sell+exactGold+insufficientFundsRollback; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + "saveV1=item+gold+schedule-restored+no-pending-transaction; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Economy.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (baseline != null && session.PlayerState != null && session.HasSelectedSector)
                session.SaveGames.LoadJson(baseline);
            _running = false;
        }
    }

    private static void Refresh(out WorldObjectSectorLoader loader,
        out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null,
            "production TestTerrain composition remains available");
    }

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid M11A validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M11A validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class CountingSourceRandom : IEconomyRandom
    {
        internal int Calls { get; private set; }
        public int NextInclusive(int minimumInclusive, int maximumInclusive)
        {
            Calls++;
            return minimumInclusive == 1 && maximumInclusive == 100
                ? maximumInclusive : minimumInclusive;
        }
    }
}
