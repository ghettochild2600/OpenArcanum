using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M9ACombatAiValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string SkeletonSector = "maps/arcanum1-024-fixed/59726889458.sec";
    private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
    private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private static readonly ArcanumObjectId Bow = Parse(BowKey);
    private static readonly ArcanumObjectId Arrows = Parse(ArrowKey);
    private static readonly ArcanumObjectId Bear = Parse(BearKey);
    private static readonly ArcanumObjectId Skeleton = Parse("G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848");
    private static readonly ArcanumObjectId Sword = Parse("G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91");
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M9A Phase 1/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M9A harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    [MenuItem("OpenArcanum/M9A Phase 2/Run Physical PlayMode Validation")]
    private static void RunPhase2()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M9A harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(ValidatePhase2(loader));
    }

    private static IEnumerator ValidatePhase2(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        WorldMapSessionCoordinator session = loader.Session;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(SkeletonSector), "authentic Greater Skeleton sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle, out ProductionCombatAiDriver driver);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC binds in the skeleton sector");
            PersistentObjectState skeleton = Require(session, Skeleton, ObjectType.Npc, 28460);
            PersistentObjectState sword = Require(session, Sword, ObjectType.Weapon, 6050);
            Check(skeleton.Off, "authentic Greater Skeleton begins behind its source encounter gate");
            typeof(PersistentObjectState).GetProperty(nameof(PersistentObjectState.Off))?.SetValue(skeleton, false);
            session.ClearSelectedSector();
            yield return null;
            Check(session.SelectSector(SkeletonSector), "activated Greater Skeleton sector reloads");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC rebinds in the skeleton sector");
            driver.enabled = false;
            ArcanumObjectId pc = session.PlayerState.Identity;
            WorldObject pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Skeleton, out WorldObject skeletonRuntime),
                "authentic Greater Skeleton presentation resolves");
            Check(sword.Placement == ObjectPlacement.EquippedBy(Skeleton, WornLocation.Weapon)
                  && sword.WeaponData?.Skill == WeaponSkill.Melee,
                "authentic sword is source-equipped and belongs to the supported melee family");

            MoveActor(session, loader, pc, pcRuntime,
                FindMeleeTile(loader.NavigationMap, skeletonRuntime.Tile), true);
            Check(session.Combat.StartCombat(pc, Skeleton, CombatMode.TurnBased).Succeeded,
                "authentic sword encounter starts in turn-based mode");
            PrepareActorTurn(session, Skeleton);
            int apBefore = session.Combat.CurrentActionPoints;
            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            session.Combat.SetRandomSource(new SequenceRandom(100, 100,
                sword.WeaponData.DamageMin[(int)DamageType.Normal],
                sword.WeaponData.DamageMin[(int)DamageType.Fatigue]));
            CombatAiDecision swordAttack = driver.Controller.DecideAndSubmit(Skeleton);
            Check(swordAttack.AttackResult?.Succeeded == true
                  && swordAttack.AttackResult.Value.WeaponIdentity == Sword
                  && swordAttack.AttackResult.Value.Request.Mode == CombatAttackMode.BasicMelee
                  && swordAttack.AttackResult.Value.ActionPointsSpent == sword.WeaponData.AttackActionPointCost
                  && session.Combat.CurrentActionPoints
                     == apBefore - sword.WeaponData.AttackActionPointCost
                  && session.Vitality.GetCurrentHitPoints(pc)
                     == pcHp - swordAttack.AttackResult.Value.MitigatedHitPointDamage,
                "Greater Skeleton autonomously uses its authentic sword through M8 AP/damage authority");
            Check(!driver.Controller.LastWeaponSelection.Changed
                  && driver.Controller.LastWeaponSelection.SelectedWeapon == Sword,
                "combat-entry source selection retains the already-best authentic sword");
            EndCombat(session, pc);

            Check(session.Combat.StartCombat(pc, Skeleton, CombatMode.RealTime).Succeeded,
                "authentic sword encounter starts in real-time mode");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100,
                sword.WeaponData.DamageMin[(int)DamageType.Normal],
                sword.WeaponData.DamageMin[(int)DamageType.Fatigue]));
            CombatAiDecision readySword = driver.Controller.DecideAndSubmit(Skeleton);
            CombatAiDecision busySword = driver.Controller.DecideAndSubmit(Skeleton);
            Check(readySword.Scheduled && busySword.Action == CombatAiActionKind.Busy,
                "authentic sword schedules only while READY and remains BUSY through source ART timing");
            Check(session.Combat.TryGetRealTimeActorState(Skeleton, out CombatRealTimeActorState swordTiming),
                "M8H exposes the authentic sword pending action");
            int swordEffect = (int)(swordTiming.PendingAction.EffectAtMilliseconds
                                    - session.Combat.ElapsedCombatTimeMilliseconds);
            Check(swordEffect >= 0 && session.Combat.AdvanceRealTime(swordEffect).Succeeded,
                "authentic sword reaches its authored action frame");
            CombatAttackResult swordResolved = session.Combat.LastAttackResult.GetValueOrDefault();
            Check(swordResolved.Succeeded && swordResolved.WeaponIdentity == Sword,
                "real-time sword effect resolves through the production melee transaction");
            Check(session.Combat.TryGetRealTimeActorState(Skeleton, out swordTiming),
                "sword actor remains scheduler-owned after its effect");
            int swordReady = (int)(swordTiming.PendingAction.ReadyAtMilliseconds
                                   - session.Combat.ElapsedCombatTimeMilliseconds);
            Check(swordReady >= 0 && session.Combat.AdvanceRealTime(swordReady).Succeeded,
                "authentic sword reaches its authored recovery boundary");
            EndCombat(session, pc);

            Check(session.SelectSector(EquipmentSector), "authentic Bow/ammo sector loads for fallback proof");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC binds in the equipment sector");
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            StackSplitResult split = session.SplitStack(Arrows, 1);
            Check(split.Succeeded && split.CreatedState?.StackQuantity == 1,
                "inventory authority splits one authentic arrow for exact depletion");

            Check(session.SelectSector(CombatSector), "authentic Polar Bear Cub sector loads for Bow fallback");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC binds in the bear sector");
            driver.enabled = false;
            pc = session.PlayerState.Identity;
            pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime),
                "authentic Polar Bear Cub production instance resolves");
            MoveOwnedItem(session, bow, Bear);
            MoveOwnedItem(session, split.CreatedState, Bear);
            Vector2Int rangedTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, bearRuntime.Tile, true);
            MoveActor(session, loader, Bear, bearRuntime, rangedTile, false);
            session.Combat.BindRealTimeTimingSource(new DeterministicBowTiming());
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.RealTime).Succeeded,
                "one-arrow ranged selection encounter starts");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAiDecision selectedBow = driver.Controller.DecideAndSubmit(Bear);
            CombatAiDecision bowBusy = driver.Controller.DecideAndSubmit(Bear);
            Check(selectedBow.Scheduled && selectedBow.AttackMode == CombatAttackMode.BasicRanged
                  && bowBusy.Action == CombatAiActionKind.Busy
                  && session.TryGetEquippedItem(Bear, WornLocation.Weapon, out PersistentObjectState equippedBow)
                  && equippedBow.Identity == Bow,
                "usable authentic Bow+arrow is selected through equipment authority without bypassing BUSY");
            Check(session.Combat.AdvanceRealTime(50).Succeeded,
                "selected Bow reaches deterministic source-shaped effect timing");
            CombatAttackResult bowResolved = session.Combat.LastAttackResult.GetValueOrDefault();
            Check(bowResolved.Succeeded && bowResolved.WeaponIdentity == Bow
                  && bowResolved.AmmoQuantityBefore == 1 && bowResolved.AmmoQuantityAfter == 0,
                "production ranged transaction consumes the final compatible arrow exactly once");
            Check(session.Combat.AdvanceRealTime(50).Succeeded,
                "Bow actor returns to READY before fallback selection");
            CombatAiDecision fallback = driver.Controller.DecideAndSubmit(Bear);
            Check(fallback.Failure != CombatFailure.NoAmmo
                  && fallback.Action == CombatAiActionKind.Move
                  && !session.TryGetEquippedItem(Bear, WornLocation.Weapon, out _)
                  && driver.Controller.LastWeaponSelection.UsesUnarmedFallback
                  && driver.Controller.LastWeaponSelection.Changed,
                "depleted Bow falls back to unarmed before another shot and submits ordinary approach movement");
            EndCombat(session, pc);
            session.Combat.BindRealTimeTimingSource(loader);

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M9A PHASE 2 PLAYMODE VALIDATION PASS: authenticMelee=GreaterSkeleton+sword+productionAP+sourceART; "
                      + "rangedSelection=authenticBow+oneArrow+equipmentAuthority; fallback=depletion->unarmed+noNoAmmoRetry; "
                      + "realTime=READY+BUSY+effect+recovery; targetScoring=deterministicProductionServiceFixture; "
                      + "knownFixtureLimit=PolarBearUsesDeterministicBowTiming; warnings=0; errors=0.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        WorldMapSessionCoordinator session = loader.Session;
        string baseline = null;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "authentic Bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle, out ProductionCombatAiDriver driver);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC binds in the equipment sector");
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);

            Check(session.SelectSector(CombatSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC binds in the combat sector");
            ArcanumObjectId pc = session.PlayerState.Identity;
            WorldObject pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime),
                "authentic Polar Bear Cub production instance resolves");
            MoveOwnedItem(session, bow, Bear);
            MoveOwnedItem(session, arrows, Bear);
            baseline = session.SaveGames.SerializeCurrentSession();
            driver.enabled = false;

            Vector2Int meleeTile = FindMeleeTile(loader.NavigationMap, bearRuntime.Tile);
            MoveActor(session, loader, pc, pcRuntime, meleeTile, true);
            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
                "turn-based production combat starts for autonomous melee");
            int bearMaximumAp = session.Combat.MaximumActionPoints;
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            driver.enabled = true;
            yield return null;
            CombatAttackResult melee = session.Combat.LastAttackResult.GetValueOrDefault();
            Check(melee.Succeeded && melee.Request.Attacker == Bear
                  && melee.Request.Target == pc && melee.Request.Mode == CombatAttackMode.BasicMelee
                  && melee.ActionPointsSpent == CombatStateService.UnarmedAttackActionPointCost
                  && session.Combat.CurrentParticipant != Bear
                  && session.Vitality.GetCurrentHitPoints(pc) == pcHp,
                "production driver autonomously submits the legal bear melee miss, spends M8 AP, and advances turn "
                + $"[succeeded={melee.Succeeded}; attacker={melee.Request.Attacker}; target={melee.Request.Target}; "
                + $"mode={melee.Request.Mode}; spent={melee.ActionPointsSpent}; turn={session.Combat.CurrentParticipant}; "
                + $"pcHp={session.Vitality.GetCurrentHitPoints(pc)}/{pcHp}]");
            Check(bearMaximumAp >= melee.ActionPointsSpent,
                "turn-based AI is bounded by the authoritative AP budget");
            driver.enabled = false;
            EndCombat(session, pc);

            Vector2Int pursuitTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 4, 6);
            MoveActor(session, loader, pc, pcRuntime, pursuitTile, true);
            Vector2Int pursuitStart = bearRuntime.Tile;
            pcHp = session.Vitality.GetCurrentHitPoints(pc);
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
                "turn-based production combat starts for pursuit");
            PrepareBearTurn(session);
            CombatAiTurnResult pursuit = driver.Controller.RunCurrentTurn();
            Check(pursuit.Moves > 0 && bearRuntime.Tile != pursuitStart
                  && InteractionRangeRules.Distance(bearRuntime.Tile, pcRuntime.Tile)
                     < InteractionRangeRules.Distance(pursuitStart, pcRuntime.Tile)
                  && session.Vitality.GetCurrentHitPoints(pc) == pcHp
                  && session.Combat.CurrentParticipant == pc,
                "out-of-range bear repeatedly re-evaluates through authoritative one-step movement and yields at AP limit");
            EndCombat(session, pc);

            Check(session.EquipItem(Bear, Bow, WornLocation.Weapon).Succeeded,
                "authentic Bow equips on the authentic hostile through equipment authority");
            FindBlockedPair(loader.NavigationMap, out Vector2Int blockedSource, out Vector2Int blockedTarget);
            MoveActor(session, loader, Bear, bearRuntime, blockedSource, false);
            MoveActor(session, loader, pc, pcRuntime, blockedTarget, true);
            int ammoBeforeBlocked = arrows.StackQuantity.Value;
            pcHp = session.Vitality.GetCurrentHitPoints(pc);
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
                "hard-blocked Bow encounter starts");
            PrepareBearTurn(session);
            CombatAiDecision blocked = driver.Controller.DecideAndSubmit(Bear);
            Check(blocked.Action == CombatAiActionKind.Move && blocked.MoveResult?.Succeeded == true
                  && arrows.StackQuantity == ammoBeforeBlocked
                  && session.Vitality.GetCurrentHitPoints(pc) == pcHp,
                "hard line-of-fire causes approach movement with zero ammo or vitality transaction");
            EndCombat(session, pc);

            Vector2Int clearSource = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, bearRuntime.Tile, true);
            MoveActor(session, loader, Bear, bearRuntime, clearSource, false);
            int ammoBeforeShot = arrows.StackQuantity.Value;
            session.Combat.BindRealTimeTimingSource(new DeterministicBowTiming());
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.RealTime).Succeeded,
                "clear real-time Bow encounter starts without turn-based AP overdraw");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAiDecision bowShot = driver.Controller.DecideAndSubmit(Bear);
            Check(bowShot.Action == CombatAiActionKind.Attack && bowShot.Scheduled,
                "clear Bow action schedules through M8H authority "
                + $"[action={bowShot.Action}; failure={bowShot.Failure}; scheduled={bowShot.Scheduled}]");
            Check(session.Combat.AdvanceRealTime(50).Succeeded,
                "clear Bow action reaches its source-timed effect");
            CombatAttackResult resolvedBow = session.Combat.LastAttackResult.GetValueOrDefault();
            Check(resolvedBow.Succeeded
                  && resolvedBow.Request.Mode == CombatAttackMode.BasicRanged
                  && arrows.StackQuantity == ammoBeforeShot - 1,
                "clear Bow line autonomously submits through M8 and consumes exactly one authentic arrow "
                + $"[action={bowShot.Action}; failure={bowShot.Failure}; "
                + $"result={resolvedBow.Failure}; mode={resolvedBow.Request.Mode}; "
                + $"ammo={arrows.StackQuantity}/{ammoBeforeShot}]");
            Check(session.Combat.AdvanceRealTime(50).Succeeded,
                "clear Bow actor reaches source-timed recovery before teardown");
            EndCombat(session, pc);

            session.Combat.BindRealTimeTimingSource(loader);
            Check(session.UnequipItem(Bear, WornLocation.Weapon).Succeeded,
                "authentic bear returns to its authored unarmed real-time animation path");

            Check(session.Combat.StartCombat(pc, Bear, CombatMode.RealTime).Succeeded,
                "real-time production combat starts");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100, 100, 100));
            CombatAiDecision ready = driver.Controller.DecideAndSubmit(Bear);
            CombatAiDecision busy = driver.Controller.DecideAndSubmit(Bear);
            Check(ready.Scheduled && busy.Action == CombatAiActionKind.Busy
                  && busy.Failure == CombatFailure.ActorBusy,
                "real-time AI schedules once when READY and cannot act again while BUSY");
            Check(session.Combat.TryGetRealTimeActorState(Bear, out CombatRealTimeActorState pending),
                "real-time authority exposes the bear pending action");
            int untilReady = (int)(pending.PendingAction.ReadyAtMilliseconds
                                   - session.Combat.ElapsedCombatTimeMilliseconds);
            Check(untilReady >= 0 && session.Combat.AdvanceRealTime(untilReady).Succeeded,
                "source-timed real-time action reaches its exact recovery boundary");
            CombatAiDecision readyAgain = driver.Controller.DecideAndSubmit(Bear);
            Check(readyAgain.Scheduled,
                "real-time AI re-evaluates only after M8H reports READY again");

            string activeJson = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(activeJson).Succeeded,
                "Save V1 reload succeeds during an AI-owned pending action");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            driver.enabled = false;
            driver.Controller.Refresh();
            Check(!session.Combat.IsActive && driver.Controller.TrackedActorCount == 0
                  && driver.Controller.LastDecision.Action == CombatAiActionKind.None,
                "Save V1 restores no stale AI intent, target, or scheduled combat action");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "authoritative pre-validation state restores for defeat gating");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            driver.enabled = false;
            pc = session.PlayerState.Identity;
            pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Bear, out bearRuntime),
                "authentic bear rebinds after normalization");
            MoveActor(session, loader, pc, pcRuntime, FindMeleeTile(loader.NavigationMap, bearRuntime.Tile), true);
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
                "defeat-gating encounter starts");
            PrepareBearTurn(session);
            session.Vitality.ApplyFatigueDamage(Bear, session.Vitality.GetCurrentFatigue(Bear));
            CombatAiDecision unconscious = driver.Controller.DecideAndSubmit(Bear);
            Check(unconscious.Failure == CombatFailure.ParticipantUnavailable
                  && !session.Combat.IsActive,
                "unconscious hostile cannot act and cannot keep combat alive");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "authoritative baseline restores for target invalidation");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            driver.enabled = false;
            pc = session.PlayerState.Identity;
            pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Bear, out bearRuntime), "bear rebinds for target invalidation");
            MoveActor(session, loader, pc, pcRuntime, FindMeleeTile(loader.NavigationMap, bearRuntime.Tile), true);
            Check(session.Combat.StartCombat(pc, Bear, CombatMode.TurnBased).Succeeded,
                "target-invalidation encounter starts");
            PrepareBearTurn(session);
            session.Vitality.ApplyHitPointDamage(pc, session.Vitality.GetCurrentHitPoints(pc));
            driver.Controller.Refresh();
            Check(!session.Combat.IsActive && driver.Controller.TrackedActorCount == 0,
                "dead target invalidates AI focus and opposition resolution terminates combat");

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "physical validation restores committed world/vitality baseline");
            yield return null;
            Refresh(out loader, out lifecycle, out driver);
            Check(!session.Combat.IsActive && !session.Vitality.IsDead(Bear)
                  && !session.Vitality.IsDead(session.PlayerState.Identity),
                "physical cleanup leaves no combat or defeat residue");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M9A PHASE 1 PLAYMODE VALIDATION PASS: fixtures=production PC+Polar Bear Cub+authentic Bow+arrows; "
                      + "turnBased=autonomousMelee+AP+turnAdvance+pursuit+reevaluation; "
                      + "bow=clearShot+oneAmmo+hardBlockedApproach+zeroBlockedTransaction; "
                      + "realTime=READY+BUSY+sourceRecovery; defeat=unconsciousSuppressed+deadTargetInvalidated+combatTerminated; "
                      + "saveV1=AIIntentAndPendingActionNormalized; warnings=0; errors=0.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void EndCombat(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        if (!session.Combat.IsActive) return;
        ArcanumObjectId[] hostiles = session.Combat.Participants
            .Where(value => value.ObjectType == ObjectType.Npc)
            .Select(value => value.Identity)
            .ToArray();
        foreach (ArcanumObjectId hostile in hostiles)
            Check(session.Combat.RemoveParticipant(hostile).Succeeded,
                "each hostile leaves through roster authority before combat exit");
        Check(session.Combat.EndCombat(pc).Succeeded, "combat exits without transient residue");
    }

    private static void PrepareBearTurn(WorldMapSessionCoordinator session)
    {
        ArcanumObjectId[] otherHostiles = session.Combat.Participants
            .Where(value => value.ObjectType == ObjectType.Npc && value.Identity != Bear)
            .Select(value => value.Identity)
            .ToArray();
        foreach (ArcanumObjectId hostile in otherHostiles)
            Check(session.Combat.RemoveParticipant(hostile).Succeeded,
                "unrelated authentic hostile leaves the bounded proof roster");
        while (session.Combat.CurrentParticipant != Bear)
            Check(session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant).Succeeded,
                "authoritative turn progression reaches the bear fixture");
    }

    private static void PrepareActorTurn(WorldMapSessionCoordinator session, ArcanumObjectId actor)
    {
        ArcanumObjectId[] otherHostiles = session.Combat.Participants
            .Where(value => value.ObjectType == ObjectType.Npc && value.Identity != actor)
            .Select(value => value.Identity)
            .ToArray();
        foreach (ArcanumObjectId hostile in otherHostiles)
            Check(session.Combat.RemoveParticipant(hostile).Succeeded,
                "unrelated authentic hostile leaves the bounded proof roster");
        while (session.Combat.CurrentParticipant != actor)
            Check(session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant).Succeeded,
                "authoritative turn progression reaches the selected NPC fixture");
    }

    private static Vector2Int FindMeleeTile(SectorNavigationMap map, Vector2Int target)
    {
        foreach (Vector2Int delta in IsoProjection.DirDelta)
        {
            Vector2Int candidate = target + delta;
            if (map.IsWalkable(candidate)) return candidate;
        }
        throw new InvalidOperationException("M9A validation FAIL: no melee tile.");
    }

    private static Vector2Int FindClearRangedTile(SectorNavigationMap map, Vector2Int target,
        int minimumDistance, int maximumDistance)
    {
        for (int distance = minimumDistance; distance <= maximumDistance; distance++)
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && !map.GetProjectileTraversal(candidate, target).IsBlocked) return candidate;
        }
        throw new InvalidOperationException("M9A validation FAIL: no clear ranged tile.");
    }

    private static void FindBlockedPair(SectorNavigationMap map,
        out Vector2Int source, out Vector2Int target)
    {
        var pathfinder = new DeterministicTilePathfinder();
        var route = new System.Collections.Generic.List<Vector2Int>();
        for (int targetY = 0; targetY < 64; targetY++)
        for (int targetX = 0; targetX < 64; targetX++)
        {
            var candidateTarget = new Vector2Int(targetX, targetY);
            if (!map.IsWalkable(candidateTarget)) continue;
            for (int distance = 2; distance <= 15; distance++)
            for (int sourceY = Math.Max(0, targetY - distance); sourceY <= Math.Min(63, targetY + distance); sourceY++)
            for (int sourceX = Math.Max(0, targetX - distance); sourceX <= Math.Min(63, targetX + distance); sourceX++)
            {
                var candidateSource = new Vector2Int(sourceX, sourceY);
                if (InteractionRangeRules.Distance(candidateSource, candidateTarget) != distance
                    || !map.IsWalkable(candidateSource)
                    || !map.GetProjectileTraversal(candidateSource, candidateTarget).IsBlocked) continue;
                foreach (Vector2Int delta in IsoProjection.DirDelta)
                {
                    Vector2Int destination = candidateTarget + delta;
                    route.Clear();
                    if (map.IsWalkable(destination)
                        && pathfinder.TryFindPath(map, candidateSource, destination, route)
                        && route.Count > 0)
                    {
                        source = candidateSource;
                        target = candidateTarget;
                        return;
                    }
                }
            }
        }
        throw new InvalidOperationException("M9A validation FAIL: no authentic blocked reachable pair.");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.Placement.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(owner)).Succeeded, "authentic item transfers through authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile, bool controlled)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement accepts validation placement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        if (controlled) loader.NavigationMap.SetControlledObject(runtime);
    }

    private static PersistentObjectState Require(WorldMapSessionCoordinator session,
        ArcanumObjectId identity, ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state)
              && state.Type == type && state.PrototypeNumber == prototype,
            $"authentic {type} prototype {prototype} resolves by ObjectID");
        return state;
    }

    private static void Refresh(out WorldObjectSectorLoader loader,
        out ProductionPlayerLifecycle lifecycle, out ProductionCombatAiDriver driver)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        driver = Object.FindFirstObjectByType<ProductionCombatAiDriver>();
        Check(loader != null && lifecycle != null && driver != null && driver.Controller != null,
            "production TestTerrain composition includes one bound combat AI driver");
    }

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M9A validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly System.Collections.Generic.Queue<int> _values;
        public SequenceRandom(params int[] values)
            => _values = new System.Collections.Generic.Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "RNG sample is inside source bounds");
            return value;
        }
    }

    private sealed class DeterministicBowTiming : ICombatRealTimeTimingSource
    {
        public bool TryGetTiming(CombatRealTimeTimingRequest request, out CombatRealTimeTiming timing)
        {
            timing = new CombatRealTimeTiming(50, 100, 50, 1, 2);
            return true;
        }
    }
}
