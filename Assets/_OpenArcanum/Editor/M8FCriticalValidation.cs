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
    private const string CoverSector = "maps/arcanum1-024-fixed/101535712980.sec";
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
    private static readonly ArcanumObjectId BowMaster = ArcanumObjectId.CreateGuid(
        Guid.Parse("f6f6f6f6-f6f6-f6f6-f6f6-f6f6f6f6f6f6"));
    private static readonly ArcanumObjectId BowExpert = ArcanumObjectId.CreateGuid(
        Guid.Parse("f4f4f4f4-f4f4-f4f4-f4f4-f4f4f4f4f4f4"));
    private static readonly ArcanumObjectId CoverTarget = ArcanumObjectId.CreateGuid(
        Guid.Parse("f5f5f5f5-f5f5-f5f5-f5f5-f5f5f5f5f5f5"));
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

    [MenuItem("OpenArcanum/M8G Phase 2/Run Physical PlayMode Validation")]
    private static void RunStructuredAttackValidation()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8G Phase 2 harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(ValidateStructuredAttacks(loader));
    }

    [MenuItem("OpenArcanum/M8G Phase 3/Run Physical PlayMode Validation")]
    private static void RunCoverMasterValidation()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8G Phase 3 harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(ValidateCoverAndBowMaster(loader));
    }

    [MenuItem("OpenArcanum/M8G Phase 4/Run Physical PlayMode Validation")]
    private static void RunBowMultiImpactCriticalDodgeValidation()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8G Phase 4 harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(ValidateBowMultiImpactAndCriticalDodge(loader));
    }

    private static IEnumerator ValidateBowMultiImpactAndCriticalDodge(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "Phase 4 authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "Phase 4 production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "Phase 4 authentic bow equips through production inventory authority");

            Check(session.SelectSector(BearSector), "Phase 4 authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "Phase 4 production PC rebinds");
            WorldObject pcRuntime = lifecycle.Presentation;
            Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime),
                "Phase 4 Polar Bear Cub has one production presentation");
            Vector2Int rangedTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 5, 8);
            MoveActor(session, loader, pc, pcRuntime, rangedTile);

            Check(session.Progression.GetTrainingLevel(pc, CharacterSkill.Bow) < SkillTrainingLevel.Expert,
                "production PC begins below the two-impact Bow threshold");
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "ordinary Bow proof combat starts");
            AdvanceTo(session, pc);
            int ordinaryAp = session.Combat.CurrentActionPoints;
            int ordinaryAmmo = arrows.StackQuantity.Value;
            int bearHp = session.Vitality.GetCurrentHitPoints(Bear);
            session.Combat.SetRandomSource(new SequenceRandom(1, 100, 3, 2));
            CombatAttackResult ordinary = session.Combat.Attack(new CombatAttackRequest(
                pc, Bear, CombatAttackMode.BasicRanged));
            Check(ordinary.Succeeded && ordinary.ImpactCount == 1
                  && ordinary.ActionPointsSpent == ordinary.ActionPointCost
                  && session.Combat.CurrentActionPoints == ordinaryAp - ordinary.ActionPointCost
                  && arrows.StackQuantity == ordinaryAmmo - 1
                  && ordinary.Impacts[0].TargetIdentity == Bear,
                "below-Expert authentic Bow command produces one impact for one AP/ammo transaction");
            EndStructuredCombat(session, pc);
            RestoreDamage(session, Bear, bearHp);

            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "authentic bow unequips before the source-derived Expert transfer");
            MoveActor(session, loader, pc, pcRuntime,
                FindClearMeleeTile(loader.NavigationMap, bearRuntime.Tile));
            WorldObject expertRuntime = RegisterBowExpert(session, loader, rangedTile);
            MoveOwnedItem(session, bow, BowExpert);
            MoveOwnedItem(session, arrows, BowExpert);
            Check(session.Progression.GetTrainingLevel(BowExpert, CharacterSkill.Bow)
                  == SkillTrainingLevel.Expert,
                "validation actor resolves the exact source Expert Bow threshold");
            Check(session.EquipItem(BowExpert, Bow, WornLocation.Weapon).Succeeded,
                "source-derived Bow Expert equips the authentic production bow");
            Check(session.Combat.StartCombat(pc, BowExpert).Succeeded, "Expert Bow proof combat starts");
            if (session.Combat.Participants.All(value => value.Identity != Bear))
                Check(session.Combat.RegisterParticipant(Bear).Succeeded,
                    "authentic Polar Bear Cub joins the Expert Bow proof");
            AdvanceTo(session, BowExpert);
            int expertAp = session.Combat.CurrentActionPoints;
            int expertAmmo = arrows.StackQuantity.Value;
            session.Combat.SetRandomSource(new SequenceRandom(1, 100, 3, 2, 7, 5));
            CombatAttackResult expert = session.Combat.Attack(new CombatAttackRequest(
                BowExpert, Bear, CombatAttackMode.BasicRanged, CombatCalledLocation.Arm));
            CombatAttackModifier expertRange = expert.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.PerceptionRange);
            CombatAttackModifier expertLocation = expert.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.CalledLocation);
            Check(expert.Succeeded && expert.ImpactCount == 2
                  && expert.Impacts.All(value => value.TargetIdentity == Bear
                      && value.ModifierLedger == expert.ModifierLedger)
                  && expert.Impacts.Select(value => value.RawHitPointDamage).SequenceEqual(new[] { 3, 7 })
                  && expertRange.Applied && !expertRange.Suppressed
                  && expertLocation.Applied && expertLocation.Value == -30
                  && expert.ActionPointsSpent == expert.ActionPointCost
                  && session.Combat.CurrentActionPoints == expertAp - expert.ActionPointCost
                  && arrows.StackQuantity == expertAmmo - 1,
                "Expert Bow command produces two ordered impacts sharing range/called-location authority "
                + "while spending one AP cost and one arrow");
            EndStructuredCombat(session, pc);
            RestoreDamage(session, Bear, bearHp);
            Object.Destroy(expertRuntime.gameObject);

            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "Phase 4 Critical-Dodge bow/ammo sector reloads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "Phase 4 Critical-Dodge production PC binds");
            pc = session.PlayerState.Identity;
            bow = Require(session, Bow, ObjectType.Weapon, 6055);
            arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "Phase 4 Critical-Dodge authentic bow equips");
            Check(session.SelectSector(BearSector), "Phase 4 Critical-Dodge production sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "Phase 4 Critical-Dodge production PC rebinds");
            pcRuntime = lifecycle.Presentation;
            Check(session.TryGetLoadedObject(Bear, out bearRuntime),
                "Phase 4 Critical-Dodge authentic sector presentation is ready");
            Vector2Int dodgeTargetTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 2, 4);
            WorldObject dodgeRuntime = RegisterBowMaster(session, loader, dodgeTargetTile, true);
            Vector2Int dodgeSource = FindClearRangedTile(loader.NavigationMap, dodgeTargetTile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, dodgeSource);
            Check(session.Combat.StartCombat(pc, BowMaster).Succeeded, "Critical-Dodge proof combat starts");
            AdvanceTo(session, pc);
            int dodgeAp = session.Combat.CurrentActionPoints;
            int dodgeAmmo = arrows.StackQuantity.Value;
            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            session.Combat.SetRandomSource(new SequenceRandom(1, 100, 1, 1, 100, 51, 3, 2));
            CombatAttackResult criticalDodge = session.Combat.Attack(new CombatAttackRequest(
                pc, BowMaster, CombatAttackMode.BasicRanged));
            Check(criticalDodge.Succeeded && criticalDodge.Dodged && criticalDodge.CriticalDodge
                  && criticalDodge.DodgeCriticalRoll == 1
                  && criticalDodge.CriticalDodgeThreshold == 100
                  && criticalDodge.CriticalDodgeThresholdRoll == 100
                  && criticalDodge.Outcome == CombatAttackOutcome.CriticalFailure
                  && criticalDodge.EffectTargetIdentity == pc
                  && criticalDodge.ImpactCount == 1 && criticalDodge.Impacts[0].TargetIdentity == pc
                  && session.Vitality.GetCurrentHitPoints(pc) == pcHp - 3
                  && session.Combat.CurrentActionPoints == dodgeAp - criticalDodge.ActionPointCost
                  && arrows.StackQuantity == dodgeAmmo - 1
                  && session.Combat.CurrentParticipant == pc,
                "defender Master Dodge critical reclassifies an ordinary hit through the supported "
                + "critical-failure self-hit path and preserves the attacker's remaining-AP turn");
            Object.Destroy(dodgeRuntime.gameObject);

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M8G PHASE 4 PLAYMODE VALIDATION PASS: ordinaryBow=singleImpact; "
                      + "expertBow=twoOrderedImpacts+oneAPCost+oneArrow+sharedRangeCalledLedger; "
                      + "criticalDodge=MasterThreshold100+CriticalFailureSelfHit+normalTransaction; "
                      + "fixtures=authenticBow+arrows+PolarBearCub+productionPC; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static IEnumerator ValidateCoverAndBowMaster(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "Phase 3 authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "Phase 3 production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "authentic bow equips through production inventory authority");

            Check(session.SelectSector(BearSector), "Phase 3 authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "Phase 3 production PC rebinds");
            WorldObject pcRuntime = lifecycle.Presentation;
            Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime),
                "Phase 3 Polar Bear Cub has one production presentation");

            Vector2Int clearSource = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, clearSource);
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "clear Bow-shot combat starts");
            AdvanceTo(session, pc);
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult clear = session.Combat.Attack(new CombatAttackRequest(
                pc, Bear, CombatAttackMode.BasicRanged));
            CombatAttackModifier clearCover = clear.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.Cover);
            Check(clear.Succeeded && clearCover.Value == 0 && !clearCover.Applied,
                "clear authentic Bow shot is legal and records no cover contribution");
            EndStructuredCombat(session, pc);

            FindBlockedPair(loader.NavigationMap, out Vector2Int blockedSource,
                out Vector2Int blockedTarget);
            MoveActor(session, loader, Bear, bearRuntime, blockedTarget);
            MoveActor(session, loader, pc, pcRuntime, blockedSource);
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "hard-blocked Bow-shot combat starts");
            AdvanceTo(session, pc);
            int blockedAp = session.Combat.CurrentActionPoints;
            int blockedAmmo = arrows.StackQuantity.Value;
            int blockedHp = session.Vitality.GetCurrentHitPoints(Bear);
            ArcanumObjectId blockedTurn = session.Combat.CurrentParticipant;
            CombatAttackResult blocked = session.Combat.Attack(new CombatAttackRequest(
                pc, Bear, CombatAttackMode.BasicRanged));
            Check(blocked.Failure == CombatFailure.LineOfFireBlocked
                  && session.Combat.CurrentActionPoints == blockedAp
                  && arrows.StackQuantity == blockedAmmo
                  && session.Vitality.GetCurrentHitPoints(Bear) == blockedHp
                  && session.Combat.CurrentParticipant == blockedTurn,
                "authentic hard line-of-fire rejects before AP, ammo, vitality, or turn mutation");
            EndStructuredCombat(session, pc);

            ArcanumObjectId coveredTargetIdentity = Bear;
            WorldObject coveredTargetRuntime = bearRuntime;
            if (!TryFindCoverPair(loader, out WorldObject coverFixture, out Vector2Int coverSource,
                    out Vector2Int coverTarget, out int expectedCover))
            {
                Check(session.SelectSector(CoverSector),
                    "Phase 3 audited authentic cover sector loads");
                yield return null;
                Refresh(out loader, out lifecycle);
                if (lifecycle.Presentation == null)
                    Check(lifecycle.SpawnAndBind(), "Phase 3 production PC rebinds in cover sector");
                pcRuntime = lifecycle.Presentation;
                Check(TryFindCoverPair(loader, out coverFixture, out coverSource,
                        out coverTarget, out expectedCover),
                    "audited source-flagged sector exposes traversable numeric cover");
                coveredTargetIdentity = CoverTarget;
                coveredTargetRuntime = RegisterCoverTarget(session, loader, coverTarget);
            }
            MoveActor(session, loader, coveredTargetIdentity, coveredTargetRuntime, coverTarget);
            MoveActor(session, loader, pc, pcRuntime, coverSource);
            Check(session.Combat.StartCombat(pc, coveredTargetIdentity).Succeeded,
                "numeric-cover Bow-shot combat starts");
            AdvanceTo(session, pc);
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult covered = session.Combat.Attack(new CombatAttackRequest(
                pc, coveredTargetIdentity, CombatAttackMode.BasicRanged));
            CombatAttackModifier cover = covered.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.Cover);
            Check(covered.Succeeded && cover.Value == -expectedCover && cover.Applied
                  && cover.SourceValue == expectedCover
                  && covered.ModifierLedger.Entries.Count(value =>
                      value.Reason == CombatAttackModifierReason.Cover) == 1
                  && covered.FinalEffectiveAttackValue == covered.Chance.AttackChance,
                $"authentic {coverFixture.Type}/{coverFixture.PrototypeNumber} cover contributes "
                + $"exactly once at -{expectedCover} in the authoritative ledger");
            EndStructuredCombat(session, pc);

            if (session.SelectedSector != BearSector)
            {
                Check(session.SelectSector(BearSector),
                    "Phase 3 returns to the authentic Polar Bear Cub sector");
                yield return null;
                Refresh(out loader, out lifecycle);
                if (lifecycle.Presentation == null)
                    Check(lifecycle.SpawnAndBind(), "Phase 3 production PC rebinds after cover proof");
                pcRuntime = lifecycle.Presentation;
                Check(session.TryGetLoadedObject(Bear, out bearRuntime),
                    "Phase 3 Polar Bear Cub presentation is restored");
            }

            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "authentic bow unequips before source-derived Bow Master transfer");
            Vector2Int masterTarget = bearRuntime.Tile;
            Vector2Int masterTile = FindClearRangedTile(loader.NavigationMap, masterTarget, 5, 8);
            WorldObject masterRuntime = RegisterBowMaster(session, loader, masterTile);
            MoveOwnedItem(session, bow, BowMaster);
            MoveOwnedItem(session, arrows, BowMaster);
            Check(session.Progression.GetTrainingLevel(BowMaster, CharacterSkill.Bow)
                  == SkillTrainingLevel.Master,
                "validation actor resolves source-derived Master Bow training");
            Check(session.EquipItem(BowMaster, Bow, WornLocation.Weapon).Succeeded,
                "source-derived Bow Master equips the authentic production bow");
            Check(session.Combat.StartCombat(pc, BowMaster).Succeeded,
                "Bow Master representative combat starts");
            if (session.Combat.Participants.All(value => value.Identity != Bear))
                Check(session.Combat.RegisterParticipant(Bear).Succeeded,
                    "authentic Polar Bear Cub joins the Bow Master proof");
            AdvanceTo(session, BowMaster);
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult mastered = session.Combat.Attack(new CombatAttackRequest(
                BowMaster, Bear, CombatAttackMode.BasicRanged, CombatCalledLocation.Arm));
            CombatAttackModifier masterRange = mastered.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.PerceptionRange);
            CombatAttackModifier masterLocation = mastered.ModifierLedger.Entries.Single(value =>
                value.Reason == CombatAttackModifierReason.CalledLocation);
            Check(mastered.Succeeded && masterRange.Value < 0 && masterRange.Applied
                  && masterRange.Suppressed && masterLocation.Value == -30
                  && masterLocation.Applied && !masterLocation.Suppressed,
                "source-derived Bow Master suppresses only range while called-location remains");
            EndStructuredCombat(session, pc);
            Object.Destroy(masterRuntime.gameObject);

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M8G PHASE 3 PLAYMODE VALIDATION PASS: clearBow=legal+cover0; "
                      + "hardBlock=LineOfFireBlocked+zeroMutation; "
                      + $"numericCover={expectedCover}+singleLedgerEntry; "
                      + "bowMaster=rangeSuppressed+calledArmRetained; fixtures=authenticBow+arrows+bear+cover; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static IEnumerator ValidateStructuredAttacks(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        Action<CombatRoundBoundary> roundHandler = null;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "Phase 2 authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "Phase 2 production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = Require(session, Bow, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = Require(session, Arrows, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);

            Check(session.SelectSector(BearSector), "Phase 2 authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "Phase 2 production PC rebinds");
            WorldObject pcRuntime = lifecycle.Presentation;
            PersistentObjectState bear = Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(session.TryGetLoadedObject(Bear, out WorldObject bearRuntime),
                "Phase 2 Polar Bear Cub has one production presentation");
            MoveActor(session, loader, pc, pcRuntime, FindClearMeleeTile(loader.NavigationMap, bearRuntime.Tile));

            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "structured melee combat starts");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult normalMelee = session.Combat.Attack(new CombatAttackRequest(Bear, pc));
            Check(normalMelee.Succeeded && normalMelee.Request.Attacker == Bear
                  && normalMelee.Request.Target == pc
                  && normalMelee.Request.Mode == CombatAttackMode.BasicMelee
                  && normalMelee.RequestedLocation == CombatCalledLocation.None
                  && normalMelee.ActionPointsSpent == CombatStateService.UnarmedAttackActionPointCost
                  && normalMelee.ModifierLedger.Entries.Count == 7
                  && normalMelee.ModifierLedger.UnclampedTotal
                     == normalMelee.ModifierLedger.Entries.Where(value => value.Applied && !value.Suppressed)
                         .Sum(value => value.Value)
                  && normalMelee.FinalEffectiveAttackValue == normalMelee.Chance.AttackChance,
                "ordinary structured melee uses the immutable request and authoritative modifier ledger");
            AdvanceTo(session, pc);

            int invalidAp = session.Combat.CurrentActionPoints;
            int invalidHp = session.Vitality.GetCurrentHitPoints(Bear);
            int invalidAmmo = arrows.StackQuantity.Value;
            ArcanumObjectId invalidTurn = session.Combat.CurrentParticipant;
            CombatAttackResult invalid = session.Combat.Attack(new CombatAttackRequest(pc, Bear,
                CombatAttackMode.BasicMelee, (CombatCalledLocation)99));
            Check(invalid.Failure == CombatFailure.InvalidCalledLocation
                  && session.Combat.CurrentActionPoints == invalidAp
                  && session.Vitality.GetCurrentHitPoints(Bear) == invalidHp
                  && arrows.StackQuantity == invalidAmmo
                  && session.Combat.CurrentParticipant == invalidTurn,
                "malformed called location fails before AP, ammo, vitality, or turn mutation: "
                + $"failure={invalid.Failure}; ap={invalidAp}->{session.Combat.CurrentActionPoints}; "
                + $"hp={invalidHp}->{session.Vitality.GetCurrentHitPoints(Bear)}; "
                + $"ammo={invalidAmmo}->{arrows.StackQuantity}; "
                + $"turn={invalidTurn}->{session.Combat.CurrentParticipant}");
            EndStructuredCombat(session, pc);

            var locations = new[]
            {
                (CombatCalledLocation.Torso, 0, 40),
                (CombatCalledLocation.Head, -50, 0),
                (CombatCalledLocation.Arm, -30, 10),
                (CombatCalledLocation.Leg, -30, 10),
            };
            foreach ((CombatCalledLocation location, int modifier, int final) in locations)
            {
                Check(session.Combat.StartCombat(pc, Bear).Succeeded,
                    $"{location} called-location combat starts");
                session.Combat.SetRandomSource(new SequenceRandom(100, 100));
                CombatAttackResult called = session.Combat.Attack(new CombatAttackRequest(
                    Bear, pc, CombatAttackMode.BasicMelee, location));
                CombatAttackModifier locationEntry = called.ModifierLedger.Entries.Single(
                    value => value.Reason == CombatAttackModifierReason.CalledLocation);
                Check(called.Succeeded && called.RequestedLocation == location
                      && (int)location == Array.IndexOf(new[]
                      {
                          CombatCalledLocation.Torso, CombatCalledLocation.Head,
                          CombatCalledLocation.Arm, CombatCalledLocation.Leg,
                      }, location)
                      && locationEntry.Value == modifier && locationEntry.Applied
                      && called.FinalEffectiveAttackValue == final,
                    $"source location {location} maps once to modifier {modifier} and final {final}");
                EndStructuredCombat(session, pc);
            }

            int bearHp = session.Vitality.GetCurrentHitPoints(Bear);
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "called critical-failure combat starts");
            session.Combat.SetRandomSource(new SequenceRandom(100, 1, 100, 3, 0));
            CombatAttackResult criticalFailure = session.Combat.Attack(new CombatAttackRequest(
                Bear, pc, CombatAttackMode.BasicMelee, CombatCalledLocation.Head));
            Check(criticalFailure.Succeeded
                  && criticalFailure.Outcome == CombatAttackOutcome.CriticalFailure
                  && criticalFailure.CriticalEffect == CombatCriticalEffect.SelfHit
                  && criticalFailure.EffectTargetIdentity == Bear
                  && session.Vitality.GetCurrentHitPoints(Bear) < bearHp,
                "called melee critical failure reuses the existing self-hit transaction");
            EndStructuredCombat(session, pc);
            RestoreDamage(session, Bear, bearHp);

            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Bow) == SkillIncreaseResult.Success,
                "production PC receives one bounded Bow increase for called-shot proof");
            Check(session.EquipItem(pc, Bow, WornLocation.Weapon).Succeeded,
                "authentic bow equips through M3 authority");
            MoveActor(session, loader, pc, pcRuntime,
                FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8));
            Check(session.Combat.StartCombat(pc, Bear).Succeeded, "structured ranged combat starts");
            AdvanceTo(session, pc);

            invalidAp = session.Combat.CurrentActionPoints;
            invalidHp = session.Vitality.GetCurrentHitPoints(Bear);
            invalidAmmo = arrows.StackQuantity.Value;
            invalid = session.Combat.Attack(new CombatAttackRequest(pc, Bear,
                CombatAttackMode.BasicRanged, (CombatCalledLocation)99));
            Check(invalid.Failure == CombatFailure.InvalidCalledLocation
                  && session.Combat.CurrentActionPoints == invalidAp
                  && session.Vitality.GetCurrentHitPoints(Bear) == invalidHp
                  && arrows.StackQuantity == invalidAmmo,
                "malformed ranged request also rolls back atomically");

            int completedRounds = 0;
            roundHandler = _ => completedRounds++;
            session.Combat.RoundCompleted += roundHandler;
            session.Combat.SetRandomSource(new SequenceRandom(1, 1, 5, 2, 100, 100, 100));
            CombatAttackResult calledRanged = session.Combat.Attack(new CombatAttackRequest(
                pc, Bear, CombatAttackMode.BasicRanged, CombatCalledLocation.Arm));
            CombatAttackModifier strength = calledRanged.ModifierLedger.Entries.Single(
                value => value.Reason == CombatAttackModifierReason.MinimumStrength);
            CombatAttackModifier range = calledRanged.ModifierLedger.Entries.Single(
                value => value.Reason == CombatAttackModifierReason.PerceptionRange);
            CombatAttackModifier weapon = calledRanged.ModifierLedger.Entries.Single(
                value => value.Reason == CombatAttackModifierReason.WeaponToHit);
            CombatAttackModifier arm = calledRanged.ModifierLedger.Entries.Single(
                value => value.Reason == CombatAttackModifierReason.CalledLocation);
            Check(calledRanged.Succeeded && calledRanged.Outcome == CombatAttackOutcome.CriticalSuccess
                  && calledRanged.CriticalChance == 8
                  && calledRanged.CriticalEffect == CombatCriticalEffect.BonusDamage50
                  && calledRanged.RequestedLocation == CombatCalledLocation.Arm
                  && strength.Value == -10 && range.Value == 0 && weapon.Value == 0 && arm.Value == -30
                  && calledRanged.ModifierLedger.UnclampedTotal
                     == calledRanged.ModifierLedger.Entries.Where(value => value.Applied && !value.Suppressed)
                         .Sum(value => value.Value)
                  && calledRanged.FinalEffectiveAttackValue == calledRanged.Chance.AttackChance,
                "called ranged critical records skill/strength/range/weapon/location once and reuses M8F damage");
            int committedBearHp = session.Vitality.GetCurrentHitPoints(Bear);

            var delayedRequest = new CombatAttackRequest(pc, Bear,
                CombatAttackMode.BasicRanged, CombatCalledLocation.Leg);
            ArcanumObjectId authority = session.Combat.CurrentParticipant;
            int roundBeforeRebuild = session.Combat.RoundNumber;
            int apBeforeRebuild = session.Combat.CurrentActionPoints;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Combat.CurrentParticipant == authority
                      && session.Combat.RoundNumber == roundBeforeRebuild
                      && session.Combat.CurrentActionPoints == apBeforeRebuild
                      && completedRounds == 0,
                    $"{mode} presentation rebuild cannot mutate request/combat/round authority");
            }
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult delayed = session.Combat.Attack(delayedRequest);
            Check(delayed.Succeeded && delayed.RequestedLocation == CombatCalledLocation.Leg
                  && delayed.ModifierLedger.Entries.Single(value =>
                      value.Reason == CombatAttackModifierReason.CalledLocation).Value == -30
                  && session.Combat.CurrentParticipant == Bear && completedRounds == 1,
                "pre-rebuild request resolves against current authority and completes exactly one Phase 1 round");

            string json = session.SaveGames.SerializeCurrentSession();
            Check(json.Contains("\"version\": 1") && session.Combat.LastAttackResult.HasValue,
                "active structured-attack save remains V1 with transient diagnostic present only in memory");
            Check(session.SaveGames.LoadJson(json).Succeeded, "structured-attack V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            bear = Require(session, Bear, ObjectType.Npc, BearPrototype);
            Check(!session.Combat.IsActive && session.Combat.Participants.Count == 0
                  && !session.Combat.LastAttackResult.HasValue && completedRounds == 1
                  && session.Vitality.GetCurrentHitPoints(Bear) == committedBearHp,
                "V1 load drops request/ledger/combat transients, preserves committed vitality, and emits no boundary");

            session.Combat.RoundCompleted -= roundHandler;
            roundHandler = null;
            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M8G PHASE 2 PLAYMODE VALIDATION PASS: requests=melee+ranged immutable; "
                      + "locations=torso0/head1/arm2/leg3; modifiers=0/-50/-30/-30 exactlyOnce; "
                      + "ledger=skill+attribute+armor+strength+range+weapon+location authoritative; "
                      + "criticalSuccess=calledArm+50; criticalFailure=calledHead+selfHit; "
                      + "malformed=failClosed; graphics=Original->Enhanced->Original independent; "
                      + "roundBoundary=exactlyOnce; saveV1=noRequestOrLedgerTransient+vitalityRetained; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            if (roundHandler != null) session.Combat.RoundCompleted -= roundHandler;
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
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

    private static WorldObject RegisterBowMaster(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, Vector2Int tile, bool includeMasterDodge = false)
    {
        int[] stats = (int[])BearStats.Clone();
        stats[1] = 20;
        stats[CharacterProgressionSource.LevelSourceSlot] = 30;
        var basicSkills = new int[CharacterSkillRules.BasicSkillCount];
        basicSkills[(int)CharacterSkill.Bow] = 5 | ((int)SkillTrainingLevel.Master << 6);
        if (includeMasterDodge)
            basicSkills[(int)CharacterSkill.Dodge] = 5 | ((int)SkillTrainingLevel.Master << 6);
        var source = new ObjectInstance(ObjectType.Npc, BearPrototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(BowMaster));
        PersistentObjectState state = session.GetOrCreate(source, BearSector, source.CurrentArtId.Value,
            false, false);
        session.Characters.GetOrCreateSourceCharacter(BowMaster, ObjectType.Npc, BearPrototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(BowMaster, ObjectType.Npc, BearPrototype,
            CharacterProgressionSource.Resolve(stats, null, basicSkills, null, null, null, 0));
        session.DerivedStats.GetOrCreateSourceCharacter(BowMaster, ObjectType.Npc, BearPrototype,
            CharacterDerivedSource.Resolve(stats, null, null, 0, new int[5], null, null, 50,
                OnfKos, 0));
        session.Vitality.GetOrCreateSourceCharacter(BowMaster, ObjectType.Npc, BearPrototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject("M8G Phase 3 Bow Master validation actor");
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        session.Bind(BearSector, state, runtime);
        session.Combat.RegisterActorSource(new CombatActorSource(BowMaster, ObjectType.Npc,
            BearPrototype, BearSector, 10, OnfKos, 0, 0, BearDamage));
        loader.NavigationMap.Register(runtime, 0);
        return runtime;
    }

    private static WorldObject RegisterBowExpert(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, Vector2Int tile)
    {
        int[] stats = (int[])BearStats.Clone();
        stats[1] = 20;
        stats[CharacterProgressionSource.LevelSourceSlot] = 20;
        var basicSkills = new int[CharacterSkillRules.BasicSkillCount];
        basicSkills[(int)CharacterSkill.Bow] = 5 | ((int)SkillTrainingLevel.Expert << 6);
        var source = new ObjectInstance(ObjectType.Npc, BearPrototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(BowExpert));
        PersistentObjectState state = session.GetOrCreate(source, BearSector, source.CurrentArtId.Value,
            false, false);
        session.Characters.GetOrCreateSourceCharacter(BowExpert, ObjectType.Npc, BearPrototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(BowExpert, ObjectType.Npc, BearPrototype,
            CharacterProgressionSource.Resolve(stats, null, basicSkills, null, null, null, 0));
        session.DerivedStats.GetOrCreateSourceCharacter(BowExpert, ObjectType.Npc, BearPrototype,
            CharacterDerivedSource.Resolve(stats, null, null, 0, new int[5], null, null, 50,
                OnfKos, 0));
        session.Vitality.GetOrCreateSourceCharacter(BowExpert, ObjectType.Npc, BearPrototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject("M8G Phase 4 Bow Expert validation actor");
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        session.Bind(BearSector, state, runtime);
        session.Combat.RegisterActorSource(new CombatActorSource(BowExpert, ObjectType.Npc,
            BearPrototype, BearSector, 10, OnfKos, 0, 0, BearDamage));
        loader.NavigationMap.Register(runtime, 0);
        return runtime;
    }

    private static WorldObject RegisterCoverTarget(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, Vector2Int tile)
    {
        string sector = session.SelectedSector;
        int[] stats = (int[])BearStats.Clone();
        var source = new ObjectInstance(ObjectType.Npc, BearPrototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(CoverTarget));
        PersistentObjectState state = session.GetOrCreate(source, sector, source.CurrentArtId.Value,
            false, false);
        session.Characters.GetOrCreateSourceCharacter(CoverTarget, ObjectType.Npc, BearPrototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(CoverTarget, ObjectType.Npc, BearPrototype,
            CharacterProgressionSource.Resolve(stats, null, null, null, null, null, OcfAnimal));
        session.DerivedStats.GetOrCreateSourceCharacter(CoverTarget, ObjectType.Npc, BearPrototype,
            CharacterDerivedSource.Resolve(stats, null, null, 0, new int[5], null, null, 50,
                OnfKos, OcfAnimal));
        session.Vitality.GetOrCreateSourceCharacter(CoverTarget, ObjectType.Npc, BearPrototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject("M8G Phase 3 cover validation target");
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        session.Bind(sector, state, runtime);
        session.Combat.RegisterActorSource(new CombatActorSource(CoverTarget, ObjectType.Npc,
            BearPrototype, sector, 10, OnfKos, OcfAnimal, 0, BearDamage));
        loader.NavigationMap.Register(runtime, 0);
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

    private static void EndStructuredCombat(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        foreach (ArcanumObjectId identity in session.Combat.Participants
                     .Select(value => value.Identity).Where(value => value != pc).ToArray())
            Check(session.Combat.RemoveParticipant(identity).Succeeded,
                "Phase 2 validation removes each engaged hostile before ending combat");
        Check(session.Combat.EndCombat(pc).Succeeded,
            "Phase 2 validation clears only transient combat state");
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
            ProjectileTraversalResult traversal = map.GetProjectileTraversal(candidate, target);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && !traversal.IsBlocked && traversal.CoverPenalty == 0) return candidate;
        }
        throw new InvalidOperationException("M8F validation FAIL: no clear ranged tile.");
    }

    private static void FindBlockedPair(SectorNavigationMap map,
        out Vector2Int source, out Vector2Int target)
    {
        for (int targetY = 0; targetY < 64; targetY++)
        for (int targetX = 0; targetX < 64; targetX++)
        {
            var candidateTarget = new Vector2Int(targetX, targetY);
            if (!map.IsWalkable(candidateTarget)) continue;
            for (int distance = 2; distance <= 15; distance++)
            for (int sourceY = Math.Max(0, targetY - distance);
                 sourceY <= Math.Min(63, targetY + distance); sourceY++)
            for (int sourceX = Math.Max(0, targetX - distance);
                 sourceX <= Math.Min(63, targetX + distance); sourceX++)
            {
                var candidateSource = new Vector2Int(sourceX, sourceY);
                if (InteractionRangeRules.Distance(candidateSource, candidateTarget) != distance
                    || !map.IsWalkable(candidateSource)
                    || !map.GetProjectileTraversal(candidateSource, candidateTarget).IsBlocked) continue;
                source = candidateSource;
                target = candidateTarget;
                return;
            }
        }
        throw new InvalidOperationException("M8G Phase 3 validation FAIL: no authentic blocked LOS pair.");
    }

    private static bool TryFindCoverPair(WorldObjectSectorLoader loader, out WorldObject fixture,
        out Vector2Int source, out Vector2Int target, out int coverPenalty)
    {
        var directions = new List<Vector2Int>();
        for (int dy = -4; dy <= 4; dy++)
        for (int dx = 0; dx <= 4; dx++)
        {
            if (dx == 0 && dy <= 0 || dx == 0 && dy == 0) continue;
            int divisor = GreatestCommonDivisor(Math.Abs(dx), Math.Abs(dy));
            if (divisor == 1) directions.Add(new Vector2Int(dx, dy));
        }
        foreach (WorldObject candidate in loader.SpriteOwners
                     .Where(value => value?.WorldObject != null)
                     .Select(value => value.WorldObject)
                     .Where(value => !value.Off && value.Type is ObjectType.Container
                         or ObjectType.Scenery or ObjectType.Projectile or ObjectType.Trap
                         or ObjectType.Wall or ObjectType.Portal)
                     .OrderBy(value => value.Identity.Key, StringComparer.Ordinal))
        foreach (Vector2Int direction in directions)
        for (int before = 1; before <= 7; before++)
        for (int after = 1; after <= 7; after++)
        {
            Vector2Int candidateSource = candidate.Tile - direction * before;
            Vector2Int candidateTarget = candidate.Tile + direction * after;
            if (!loader.NavigationMap.Contains(candidateSource)
                || !loader.NavigationMap.Contains(candidateTarget)
                || !loader.NavigationMap.IsWalkable(candidateSource)
                || !loader.NavigationMap.IsWalkable(candidateTarget)
                || InteractionRangeRules.Distance(candidateSource, candidateTarget) > 15) continue;
            ProjectileTraversalResult traversal = loader.NavigationMap.GetProjectileTraversal(
                candidateSource, candidateTarget);
            if (traversal.IsBlocked || traversal.CoverPenalty <= 0) continue;
            fixture = candidate;
            source = candidateSource;
            target = candidateTarget;
            coverPenalty = traversal.CoverPenalty;
            return true;
        }
        fixture = null;
        source = default;
        target = default;
        coverPenalty = 0;
        return false;
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }
        return left;
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
