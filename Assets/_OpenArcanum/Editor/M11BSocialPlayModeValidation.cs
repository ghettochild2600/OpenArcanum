using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Social;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M11BSocialPlayModeValidation
{
    private const string MerchantSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string MerchantKey = "G_C626B82F_5190_2C40_995A_00BCB987F7A5";
    private static readonly ArcanumObjectId Merchant = Parse(MerchantKey);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M11B/Run Physical PlayMode Validation", false, 4)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M11B harness.");
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
                "authentic Tarant merchant resolves source multiplier 200 and inventory source 7");
            baseline = session.SaveGames.SerializeCurrentSession();

            SocialReactionBreakdown sourceReaction = session.Social.GetReactionBreakdown(Merchant, pc);
            Check(sourceReaction.EffectiveReaction == sourceReaction.SourceBase
                  + sourceReaction.BeautyModifier + sourceReaction.RaceModifier
                  + sourceReaction.PersistentModifier + sourceReaction.ReputationModifier,
                "authentic NPC reaction is the exact source-derived component sum");

            int adjusted = session.Social.AdjustReaction(Merchant, pc, 5);
            SocialReactionBreakdown dialogueReaction = session.Social.GetReactionBreakdown(Merchant, pc);
            Check(dialogueReaction.PersistentModifier == sourceReaction.PersistentModifier + 5
                  && adjusted == sourceReaction.EffectiveReaction + 5,
                "production dialogue/social reaction operation commits exactly once");

            session.Economy.SetRandomSource(new DeterministicSourceRandom());
            Check(session.Economy.RestockNow(Merchant, out string restockFailure),
                "authentic merchant restocks for price proof: " + restockFailure);
            session.AddGold(pc, 1000000);
            PersistentObjectState ware = session.Economy.GetMerchantWares(Merchant)
                .OrderByDescending(value => value.SourceWorth)
                .First(value => session.Economy.Preview(new EconomyTransactionRequest(
                    EconomyTransactionDirection.BuyFromMerchant, Merchant, pc, value.Identity)).Succeeded);
            EconomyTransactionRequest request = new(EconomyTransactionDirection.BuyFromMerchant,
                Merchant, pc, ware.Identity);
            EconomyTransactionResult beforeReputation = session.Economy.Preview(request);

            var globalReputation = new ReputationId(1000);
            Check(session.Social.AddReputation(pc, globalReputation),
                "authentic global reputation 1000 is acquired once");
            SocialReactionBreakdown reputedReaction = session.Social.GetReactionBreakdown(Merchant, pc);
            EconomyTransactionResult afterReputation = session.Economy.Preview(request);
            Check(reputedReaction.ReputationModifier == sourceReaction.ReputationModifier + 10
                  && reputedReaction.EffectiveReaction == dialogueReaction.EffectiveReaction + 10,
                "authentic reputation 1000 contributes its exact global +10 reaction");
            Check(afterReputation.Succeeded
                  && afterReputation.Price.Reaction == reputedReaction.EffectiveReaction
                  && afterReputation.Price.Reaction == beforeReputation.Price.Reaction + 10
                  && afterReputation.Price.UnitPrice <= beforeReputation.Price.UnitPrice,
                "M11A merchant pricing consumes the authoritative M11B reaction");

            var factionReputation = new ReputationId(1024);
            Check(session.Social.AddReputation(pc, factionReputation)
                  && session.Social.ReputationGrantsFaction(pc, 7),
                "authentic reputation 1024 grants source faction 7 membership");
            Check(session.Social.RemoveReputation(pc, factionReputation),
                "faction proof removes only its temporary reputation");

            session.Social.SetHostile(Merchant, pc, false);
            Check(session.Social.SetHostile(Merchant, pc)
                  && session.Social.IsRememberedHostile(Merchant, pc)
                  && session.Combat.AreOpponents(Merchant, pc)
                  && !session.Combat.IsActive,
                "remembered social hostility feeds combat opponent authority without starting combat");

            session.Social.SetHostile(Merchant, pc, false);
            ItemCreationResult theftItem = session.CreateItem(ware.PrototypeNumber,
                ObjectPlacement.ContainedBy(Merchant));
            Check(theftItem.Succeeded && theftItem.State.ParentIdentity == Merchant,
                "authentic merchandise prototype creates a directly owned theft fixture");
            SocialCrimeResult theft = session.Social.ReportTheft(pc, Merchant,
                theftItem.State.Identity, Merchant, true);
            Check(theft.Succeeded && theft.Type == SocialCrimeType.Theft
                  && theft.Witness == Merchant && theft.HostilityChanged
                  && session.Social.IsRememberedHostile(Merchant, pc)
                  && session.Combat.AreOpponents(Merchant, pc)
                  && !session.Combat.IsActive,
                "detected theft records witness hostility through the bounded production boundary");

            int committedReaction = session.Social.GetReaction(Merchant, pc);
            int committedPrice = session.Economy.Preview(request).Price.UnitPrice;
            int committedAi = merchant.AiData;
            int committedOrigin = merchant.Origin;
            int committedFaction = merchant.Faction;
            foreach (GraphicsMode mode in new[]
                     { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Social.GetReaction(Merchant, pc) == committedReaction
                      && session.Social.HasReputation(pc, globalReputation)
                      && session.Social.IsRememberedHostile(Merchant, pc)
                      && session.Economy.Preview(request).Price.UnitPrice == committedPrice,
                    $"{mode} presentation rebuild preserves social and economy authority");
            }

            string save = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(save).Succeeded, "social V1 save reload succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.TryGetObjectState(Merchant, out merchant)
                  && merchant.AiData == committedAi && merchant.Origin == committedOrigin
                  && merchant.Faction == committedFaction
                  && session.Social.GetReaction(Merchant, pc) == committedReaction
                  && session.Social.HasReputation(pc, globalReputation)
                  && session.Social.IsRememberedHostile(Merchant, pc)
                  && session.Combat.AreOpponents(Merchant, pc)
                  && !session.Combat.IsActive,
                "V1 restores reaction, reputation, faction inputs, and hostility without transient combat");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "validation cleanup restores the authoritative baseline");
            yield return null;
            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M11B PHYSICAL VALIDATION PASS: "
                      + $"npc={Merchant.Key}; reaction={sourceReaction.EffectiveReaction}->{committedReaction}; "
                      + $"price={beforeReputation.Price.UnitPrice}->{afterReputation.Price.UnitPrice}; "
                      + "dialogueSocial=+5; reputation1000=+10; faction=source-grant; "
                      + "hostility=combat-authoritative; theft=detected-witness-boundary; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + "saveV1=social+object-inputs-restored+no-transient-combat; "
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
            throw new InvalidOperationException("Invalid M11B validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M11B validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class DeterministicSourceRandom : IEconomyRandom
    {
        public int NextInclusive(int minimumInclusive, int maximumInclusive)
            => minimumInclusive == 1 && maximumInclusive == 100 ? maximumInclusive : minimumInclusive;
    }
}
