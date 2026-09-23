using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8FCriticalValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string BearSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string SkeletonSector = "maps/arcanum1-024-fixed/59726889458.sec";
    private const int OnfKos = 0x00000100;
    private const int OcfAnimal = 0x00008000;
    private const int BearPrototype = 28422;
    private static readonly int[] BearStats =
        { 7, 4, 5, 17, 4, 5, 5, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 5, 0, 0, 0, 20, 1, 0 };
    private static readonly int[] BearDamage = { 3, 6, 0, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly ArcanumObjectId Bear = Parse("G_9B807B01_A142_4949_80CE_5A085F3BEEB1");
    private static readonly ArcanumObjectId Bow = Parse("G_1575DBCA_4990_C243_8184_524D51F7D533");
    private static readonly ArcanumObjectId Arrows = Parse("G_FBFA4631_D97D_D740_9636_F131B2FD9F7B");
    private static readonly ArcanumObjectId Skeleton = Parse("G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848");
    private static readonly ArcanumObjectId Master = ArcanumObjectId.CreateGuid(
        Guid.Parse("f8f8f8f8-f8f8-f8f8-f8f8-f8f8f8f8f8f8"));
    private static readonly ArcanumObjectId ArmoredTarget = ArcanumObjectId.CreateGuid(
        Guid.Parse("f7f7f7f7-f7f7-f7f7-f7f7-f7f7f7f7f7f7"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M8F/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8F harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);

            Check(session.SelectSector(BearSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds");
            WorldObject pcRuntime = lifecycle.Presentation;
            PersistentObjectState bear = Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime),
                "Polar Bear Cub has one production presentation");
            MoveActor(session, loader, pc, pcRuntime, FindClearMeleeTile(loader.NavigationMap, bearRuntime.Tile));
            Check(InteractionRangeRules.Distance(pcRuntime.Tile, bearRuntime.Tile) == 1,
                "production PC physically occupies unarmed melee range");
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "production combat starts");
            AdvanceTo(session, pc);

            int attackCost = CombatStateService.UnarmedAttackActionPointCost;
            int bearHp = session.Vitality.GetCurrentHitPoints(Bear);
            CombatAttackResult fifty = CriticalPcAttack(session, pc, Bear, 100, 100, 1);
            Check(fifty.Succeeded && fifty.Outcome == CombatAttackOutcome.CriticalSuccess
                  && fifty.CriticalEffect == CombatCriticalEffect.BonusDamage50
                  && fifty.MitigatedHitPointDamage == ExpectedCriticalDamage(session, Bear,
                      fifty.RawHitPointDamage, 3, 2)
                  && fifty.ActionPointCost == attackCost && fifty.ActionPointsSpent == attackCost
                  && session.Vitality.GetCurrentHitPoints(Bear) == bearHp - fifty.MitigatedHitPointDamage,
                "production PC +50% critical applies after resistance through M4B at normal M8B AP cost");
            RestoreDamage(session, Bear, bearHp);

            NextPcTurn(session, pc);
            CombatAttackResult hundred = CriticalPcAttack(session, pc, Bear, 100, 30);
            Check(hundred.CriticalEffect == CombatCriticalEffect.BonusDamage100
                  && hundred.MitigatedHitPointDamage == ExpectedCriticalDamage(session, Bear,
                      hundred.RawHitPointDamage, 2, 1),
                "inclusive second threshold produces the bounded +100% damage result");
            RestoreDamage(session, Bear, bearHp);

            NextPcTurn(session, pc);
            CombatAttackResult twoHundred = CriticalPcAttack(session, pc, Bear, 10);
            Check(twoHundred.CriticalEffect == CombatCriticalEffect.BonusDamage200
                  && twoHundred.MitigatedHitPointDamage == ExpectedCriticalDamage(session, Bear,
                      twoHundred.RawHitPointDamage, 3, 1),
                "inclusive first threshold produces the bounded +200% damage result");
            RestoreDamage(session, Bear, bearHp);

            ArcanumObjectId authority = session.Combat.CurrentParticipant;
            int round = session.Combat.RoundNumber;
            int ap = session.Combat.CurrentActionPoints;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Combat.IsActive && session.Combat.CurrentParticipant == authority
                      && session.Combat.RoundNumber == round && session.Combat.CurrentActionPoints == ap
                      && session.Vitality.GetCurrentHitPoints(Bear) == bearHp,
                    $"{mode} presentation rebuild preserves combat authority and vitality");
            }

            session.Combat.EndCurrentTurn(pc);
            AdvanceTo(session, Bear);
            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            session.Combat.SetRandomSource(new SequenceRandom(100, 8, 51, 5));
            CombatAttackResult failure = session.Combat.Attack(Bear, pc);
            Check(failure.Succeeded && failure.Outcome == CombatAttackOutcome.CriticalFailure
                  && failure.CriticalEffect == CombatCriticalEffect.SelfHit
                  && failure.EffectTargetIdentity == Bear
                  && session.Vitality.GetCurrentHitPoints(pc) == pcHp
                  && failure.MitigatedHitPointDamage > 0
                  && session.Vitality.GetCurrentHitPoints(Bear)
                     == bearHp - failure.MitigatedHitPointDamage
                  && failure.ActionPointCost == attackCost && failure.ActionPointsSpent == attackCost
                  && session.Combat.CurrentParticipant == pc,
                "bear ordinary miss with secondary roll 51 self-hits, spends normal AP, and advances once");
            RestoreDamage(session, Bear, bearHp);

            session.Combat.EndCurrentTurn(pc);
            AdvanceTo(session, Bear);
            AssertUnsupportedRollback(session, Bear, pc, new SequenceRandom(100, 8, 50), arrows,
                "deferred injury/equipment critical-failure branch");
            AssertUnsupportedRollback(session, Bear, pc, new SequenceRandom(1, 2), arrows,
                "unsupported NPC-to-PC critical-success branch");

            Check(session.Combat.RemoveParticipant(Bear).Succeeded && session.Combat.EndCombat(pc).Succeeded,
                "first combat ends after hostile resolution");
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "authentic bow equips for ranged rollback proof");
            MoveActor(session, loader, pc, pcRuntime,
                FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8));
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "ranged rollback combat starts");
            AdvanceTo(session, pc);
            AssertUnsupportedRollback(session, pc, Bear, new SequenceRandom(100, 1), arrows,
                "unsupported ranged equipment critical-failure branch", CombatAttackMode.BasicRanged);
            Check(session.Combat.RemoveParticipant(Bear).Succeeded && session.Combat.EndCombat(pc).Succeeded,
                "ranged rollback combat ends without authority residue");
            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "PC returns to unarmed state");

            WorldObject masterRuntime = RegisterMaster(session, loader, pcRuntime.Tile,
                FindClearMeleeTile(loader.NavigationMap, pcRuntime.Tile));
            WorldObject armoredRuntime = RegisterArmoredTarget(session, loader, masterRuntime.Tile,
                FindClearMeleeTile(loader.NavigationMap, masterRuntime.Tile));
            Check(session.Progression.GetTrainingLevel(Master, CharacterSkill.Melee)
                  == SkillTrainingLevel.Master, "validation actor resolves source-derived Master Melee");
            Check(session.Combat.StartCombat(pc, Master).Succeeded, "Master Melee combat starts");
            Check(session.Combat.RegisterParticipant(ArmoredTarget).Succeeded,
                "high-armor validation target joins the physical combat");
            AdvanceTo(session, Master);
            int masterHp = session.Vitality.GetCurrentHitPoints(Master);
            int armoredHp = session.Vitality.GetCurrentHitPoints(ArmoredTarget);
            session.Combat.SetRandomSource(new SequenceRandom(100, 1));
            CombatAttackResult suppressed = session.Combat.Attack(Master, ArmoredTarget);
            Check(suppressed.Succeeded && suppressed.Outcome == CombatAttackOutcome.Miss
                  && suppressed.CriticalChance == 0 && suppressed.CriticalEffect == CombatCriticalEffect.None
                  && session.Vitality.GetCurrentHitPoints(Master) == masterHp
                  && session.Vitality.GetCurrentHitPoints(ArmoredTarget) == armoredHp,
                "Master Melee suppresses critical failure under an otherwise qualifying miss: "
                + $"failure={suppressed.Failure}, outcome={suppressed.Outcome}, "
                + $"criticalChance={suppressed.CriticalChance}, effect={suppressed.CriticalEffect}, "
                + $"masterHp={masterHp}->{session.Vitality.GetCurrentHitPoints(Master)}, "
                + $"targetHp={armoredHp}->{session.Vitality.GetCurrentHitPoints(ArmoredTarget)}");
            Check(session.Combat.RemoveParticipant(Master).Succeeded
                  && session.Combat.RemoveParticipant(ArmoredTarget).Succeeded
                  && session.Combat.EndCombat(pc).Succeeded,
                "Master suppression combat ends");
            Object.Destroy(masterRuntime.gameObject);
            Object.Destroy(armoredRuntime.gameObject);

            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(SkeletonSector), "authentic Greater Skeleton sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds for lethal proof");
            pc = session.PlayerState.Identity;
            pcRuntime = lifecycle.Presentation;
            PersistentObjectState corpse = Require(session, Skeleton, ObjectType.Npc, 28460);
            Check(corpse.Off, "Greater Skeleton starts behind its source encounter gate");
            typeof(PersistentObjectState).GetProperty(nameof(PersistentObjectState.Off))?.SetValue(corpse, false);
            session.ClearSelectedSector();
            yield return null;
            Check(session.SelectSector(SkeletonSector), "activated Greater Skeleton sector reloads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds for lethal proof");
            pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Skeleton, out WorldObject skeletonRuntime),
                "Greater Skeleton has one production presentation");
            MoveActor(session, loader, pc, pcRuntime,
                FindClearMeleeTile(loader.NavigationMap, skeletonRuntime.Tile));
            int xpBefore = session.Progression.GetExperience(pc);
            int deathTransitions = 0;
            session.Vitality.Changed += change =>
            {
                if (change.Identity == Skeleton && change.PreviousHitPoints > 0 && change.CurrentHitPoints <= 0)
                    deathTransitions++;
            };
            session.Vitality.ApplyHitPointDamage(Skeleton,
                session.Vitality.GetCurrentHitPoints(Skeleton) - 1);
            Check(session.Combat.StartCombat(pc, Skeleton).Succeeded, "lethal critical combat starts");
            AdvanceTo(session, pc);
            CombatAttackResult lethal = CriticalPcAttack(session, pc, Skeleton, 10);
            int xpAfter = session.Progression.GetExperience(pc);
            Check(lethal.Succeeded && lethal.Outcome == CombatAttackOutcome.CriticalSuccess
                  && session.Vitality.IsDead(Skeleton) && skeletonRuntime.IsDead
                  && corpse.DeathConsequencesProcessed && deathTransitions == 1
                  && xpAfter == xpBefore + 88
                  && session.DeathConsequences.Process(pc, Skeleton).Failure
                     == DeathConsequenceFailure.AlreadyProcessed
                  && session.Progression.GetExperience(pc) == xpAfter,
                "lethal critical commits death, corpse, 88 XP, and processed marker exactly once");
            Check(session.Combat.EndCombat(pc).Succeeded, "post-death combat ends explicitly");

            Check(session.SelectSector(BearSector), "combat restart sector loads after committed death");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds after death");
            pcRuntime = lifecycle.Presentation;
            bear = Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(session.TryGetLoadedObject(Bear, out bearRuntime), "Polar Bear Cub reloads for restart/save proof");
            MoveActor(session, loader, pc, pcRuntime, FindClearMeleeTile(loader.NavigationMap, bearRuntime.Tile));
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "combat restarts after committed consequence");
            AdvanceTo(session, pc);
            bearHp = session.Vitality.GetCurrentHitPoints(Bear);
            CombatAttackResult committedCritical = CriticalPcAttack(session, pc, Bear, 100, 100, 1);
            int committedBearHp = session.Vitality.GetCurrentHitPoints(Bear);
            Check(committedCritical.Outcome == CombatAttackOutcome.CriticalSuccess
                  && session.Vitality.IsDead(Skeleton) && corpse.DeathConsequencesProcessed
                  && session.Progression.GetExperience(pc) == xpAfter,
                "combat restart leaves committed death/world authority intact");

            string json = session.SaveGames.SerializeCurrentSession();
            Check(json.Contains("\"version\": 1"), "active-combat save remains format V1");
            Check(session.SaveGames.LoadJson(json).Succeeded, "active-combat V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            corpse = Require(session, Skeleton, ObjectType.Npc, 28460);
            bear = Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(session.Combat.Lifecycle == CombatLifecycle.Inactive && !session.Combat.IsActive
                  && session.Combat.Participants.Count == 0 && session.Combat.CurrentActionPoints == 0,
                "V1 load restores no stale attack, critical transaction, participants, or AP");
            Check(session.Vitality.GetCurrentHitPoints(Bear) == committedBearHp
                  && session.Vitality.IsDead(Skeleton) && corpse.DeathConsequencesProcessed
                  && session.Progression.GetExperience(pc) == xpAfter
                  && session.DeathConsequences.Process(pc, Skeleton).Failure
                     == DeathConsequenceFailure.AlreadyProcessed,
                "V1 load preserves committed vitality, death, world consequence, and exact-once reward");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M8F PLAYMODE VALIDATION PASS: PC-unarmed+PolarBearCub; criticalSuccess=+50/+100/+200; "
                      + "damageOrder=resistanceThenMultiplier; criticalFailure=selfHit; masterMelee=suppressed; "
                      + "unsupported=injury+equipment+NPCtoPC failClosed; AP/ammo/vitality/turn rollback=clean; "
                      + "lethal=death+corpse+88XP+processedExactlyOnce; graphics=Original->Enhanced->Original; "
                      + "combatRestart=authoritative; saveV1=noTransientCriticalOrAttack; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static CombatAttackResult CriticalPcAttack(WorldMapSessionCoordinator session,
        ArcanumObjectId pc, ArcanumObjectId target, params int[] effectRolls)
    {
        CombatHitChance chance = session.Combat.GetBasicMeleeHitChance(pc, target);
        var values = new List<int> { 1, 1 };
        if (chance.DodgeChance > 0) values.Add(100);
        values.Add(4);
        values.Add(4);
        values.AddRange(effectRolls);
        session.Combat.SetRandomSource(new SequenceRandom(values.ToArray()));
        return session.Combat.Attack(pc, target);
    }

    private static int ExpectedCriticalDamage(WorldMapSessionCoordinator session, ArcanumObjectId target,
        int raw, int numerator, int denominator)
    {
        int resistance = session.DerivedStats.GetResistance(target, CharacterResistance.Normal);
        int resisted = Math.Max(0, raw - raw * Math.Max(0, Math.Min(100, resistance)) / 100);
        return resisted * numerator / denominator;
    }

    private static void AssertUnsupportedRollback(WorldMapSessionCoordinator session,
        ArcanumObjectId actor, ArcanumObjectId target, ICombatRandom random, PersistentObjectState ammo,
        string label, CombatAttackMode mode = CombatAttackMode.BasicMelee)
    {
        int actorHp = session.Vitality.GetCurrentHitPoints(actor);
        int targetHp = session.Vitality.GetCurrentHitPoints(target);
        int ap = session.Combat.CurrentActionPoints;
        int? quantity = ammo.StackQuantity;
        ArcanumObjectId turn = session.Combat.CurrentParticipant;
        session.Combat.SetRandomSource(random);
        CombatAttackResult result = session.Combat.Attack(actor, target, mode);
        Check(result.Failure == CombatFailure.UnsupportedCriticalEffect
              && session.Vitality.GetCurrentHitPoints(actor) == actorHp
              && session.Vitality.GetCurrentHitPoints(target) == targetHp
              && session.Combat.CurrentActionPoints == ap && session.Combat.CurrentParticipant == turn
              && ammo.StackQuantity == quantity,
            $"{label} fails closed before AP, ammo, vitality, or turn mutation");
    }

    private static WorldObject RegisterMaster(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, Vector2Int pcTile, Vector2Int tile)
    {
        int[] stats = (int[])BearStats.Clone();
        stats[1] = 20;
        stats[CharacterProgressionSource.LevelSourceSlot] = 30;
        var source = new ObjectInstance(ObjectType.Npc, BearPrototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(Master));
        PersistentObjectState state = session.GetOrCreate(source, BearSector, source.CurrentArtId.Value,
            false, false);
        session.Characters.GetOrCreateSourceCharacter(Master, ObjectType.Npc, BearPrototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(Master, ObjectType.Npc, BearPrototype,
            CharacterProgressionSource.Resolve(stats, null, null, null, null, null, OcfAnimal));
        session.DerivedStats.GetOrCreateSourceCharacter(Master, ObjectType.Npc, BearPrototype,
            CharacterDerivedSource.Resolve(stats, null, null, 0, new int[5], null, null, 50,
                OnfKos, OcfAnimal));
        session.Vitality.GetOrCreateSourceCharacter(Master, ObjectType.Npc, BearPrototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject("M8F Master Melee validation actor");
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        session.Bind(BearSector, state, runtime);
        session.Combat.RegisterActorSource(new CombatActorSource(Master, ObjectType.Npc,
            BearPrototype, BearSector, 10, OnfKos, OcfAnimal, 0, BearDamage));
        loader.NavigationMap.Register(runtime, 0);
        Check(InteractionRangeRules.Distance(pcTile, tile) == 1,
            "Master Melee validation actor is in melee range");
        return runtime;
    }

    private static WorldObject RegisterArmoredTarget(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, Vector2Int masterTile, Vector2Int tile)
    {
        int[] stats = (int[])BearStats.Clone();
        var source = new ObjectInstance(ObjectType.Npc, BearPrototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(ArmoredTarget));
        PersistentObjectState state = session.GetOrCreate(source, BearSector, source.CurrentArtId.Value,
            false, false);
        session.Characters.GetOrCreateSourceCharacter(ArmoredTarget, ObjectType.Npc, BearPrototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(ArmoredTarget, ObjectType.Npc, BearPrototype,
            CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
        session.DerivedStats.GetOrCreateSourceCharacter(ArmoredTarget, ObjectType.Npc, BearPrototype,
            new CharacterDerivedSource(100, new int[5], 0, 0, 0));
        session.Vitality.GetOrCreateSourceCharacter(ArmoredTarget, ObjectType.Npc, BearPrototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject("M8F armored Master-suppression target");
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        session.Bind(BearSector, state, runtime);
        session.Combat.RegisterActorSource(new CombatActorSource(ArmoredTarget, ObjectType.Npc,
            BearPrototype, BearSector, 10, 0, 0, 0, BearDamage));
        loader.NavigationMap.Register(runtime, 0);
        Check(InteractionRangeRules.Distance(masterTile, tile) == 1,
            "high-armor validation target is in Master melee range");
        return runtime;
    }

    private static void NextPcTurn(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.EndCurrentTurn(pc).Succeeded, "PC turn ends between threshold proofs");
        AdvanceTo(session, pc);
    }

    private static void AdvanceTo(WorldMapSessionCoordinator session, ArcanumObjectId identity)
    {
        int remaining = session.Combat.Participants.Count;
        while (session.Combat.CurrentParticipant != identity && remaining-- > 0)
            Check(session.Combat.EndCurrentTurn(session.Combat.CurrentParticipant).Succeeded,
                "source-order combat advances deterministically");
        Check(session.Combat.CurrentParticipant == identity, "requested validation actor receives a turn");
    }

    private static void RestoreDamage(WorldMapSessionCoordinator session, ArcanumObjectId identity, int expected)
    {
        int current = session.Vitality.GetCurrentHitPoints(identity);
        if (current < expected) session.Vitality.RestoreHitPoints(identity, expected - current);
        Check(session.Vitality.GetCurrentHitPoints(identity) == expected,
            "validation restores only the damage applied by its previous proof");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "source equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(owner)).Succeeded,
                "authentic item transfers through M3 authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement accepts the validation tile");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        loader.NavigationMap.SetControlledObject(runtime);
    }

    private static Vector2Int FindClearMeleeTile(SectorNavigationMap map, Vector2Int target)
    {
        for (int y = Math.Max(0, target.y - 1); y <= Math.Min(63, target.y + 1); y++)
        for (int x = Math.Max(0, target.x - 1); x <= Math.Min(63, target.x + 1); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == 1 && map.IsWalkable(candidate))
                return candidate;
        }
        throw new InvalidOperationException("M8F validation FAIL: no clear melee tile.");
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
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M8F validation FAIL: no clear ranged tile.");
    }

    private static PersistentObjectState Require(WorldMapSessionCoordinator session,
        ArcanumObjectId identity, ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state),
            $"exact ObjectID {identity} resolves");
        Check(state.Type == type && state.PrototypeNumber == prototype,
            $"{identity} retains expected {type}/{prototype}");
        return state;
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null, "production TestTerrain composition remains available");
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "deterministic sample is in source range");
            return value;
        }
    }

    private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

    private static byte[] GuidBytes(ArcanumObjectId identity)
    {
        string compact = identity.ToString().Substring(2).Replace("_", string.Empty);
        var bytes = new byte[ArcanumObjectId.SerializedSize];
        bytes[0] = (byte)ArcanumObjectIdType.Guid;
        for (int index = 0; index < 16; index++)
            bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
        return bytes;
    }

    private static ArcanumObjectId Parse(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8F validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
