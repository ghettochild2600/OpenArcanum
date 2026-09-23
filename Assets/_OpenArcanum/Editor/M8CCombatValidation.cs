using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8CCombatValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string OtherMapSector = "maps/bates mansion lev 1/67108865.sec";
    private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
    private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private const int BowPrototype = 6055;
    private const int ArrowPrototype = 7058;
    private const int BulletPrototype = 7059;
    private const int MeleeWeaponPrototype = 6071;
    private static readonly ArcanumObjectId BowIdentity = ParseIdentity(BowKey);
    private static readonly ArcanumObjectId ArrowIdentity = ParseIdentity(ArrowKey);
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static readonly ArcanumObjectId MissingIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;
    private static string _temporarySlot;

    [MenuItem("OpenArcanum/M8C/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8C harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        _temporarySlot = null;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle,
                out PlayerNavigationController navigation, out ProductionSaveLoadPresenter savePresenter);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

            PersistentPlayerState player = session.PlayerState;
            Check(player != null && player.Identity.IsPersistent, "production PC has a stable ObjectID");
            ArcanumObjectId pc = player.Identity;
            PersistentObjectState bow = RequireState(session, BowIdentity, ObjectType.Weapon, BowPrototype);
            PersistentObjectState arrows = RequireState(session, ArrowIdentity, ObjectType.Ammo, ArrowPrototype);
            ArcanumObjectId sourceContainer = arrows.Placement.ParentIdentity;
            Check(sourceContainer.IsPersistent && arrows.StackQuantity == 70 && arrows.AmmoItemType == 0,
                "authentic arrow stack resolves with source quantity 70 and arrow type 0");
            Check(bow.WeaponData != null && bow.WeaponData.Skill == WeaponSkill.Bow
                  && bow.WeaponData.Range == 15 && bow.WeaponData.AttackActionPointCost == 6
                  && bow.WeaponData.AmmoType == 0 && bow.WeaponData.AmmoConsumption == 1,
                "authentic bow resolves range 15, AP 6, and one-arrow consumption");

            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, bow.Identity, WornLocation.Weapon).Succeeded,
                "M3C equips the authentic bow on the production PC");
            Check(session.TryGetEquippedItem(pc, WornLocation.Weapon, out PersistentObjectState equipped)
                  && equipped.Identity == BowIdentity && session.TryGetAmmo(pc, 0, 1, out PersistentObjectState ammo)
                  && ammo.Identity == ArrowIdentity,
                "production equipment and ammo queries resolve exact persistent ObjectIDs");

            bool physicalPortalValidated = TryFindPortalPair(loader, out WorldObject sourcePortal,
                out Vector2Int portalSource, out Vector2Int portalTarget);
            if (physicalPortalValidated)
            {
                yield return SetPortal(session, sourcePortal, false);
                Check(!loader.NavigationMap.HasProjectileLineOfFire(portalSource, portalTarget),
                    "authentic closed portal blocks source-grid projectile traversal");
                yield return SetPortal(session, sourcePortal, true);
                Check(loader.NavigationMap.HasProjectileLineOfFire(portalSource, portalTarget),
                    "opening the same authentic portal permits source-grid projectile traversal");
                yield return SetPortal(session, sourcePortal, false);
            }
            else
            {
                Debug.Log("M8C PORTAL FIXTURE NOTE: selected authentic sectors contain no portal whose open state "
                          + "alone clears projectile LOS; closed/open semantics remain covered by focused source-grid validation.");
            }

            Check(session.SelectSector(CombatSector), "authentic bear sector loads with equipment retained");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out savePresenter);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds in combat sector");
            PersistentObjectState bear = RequireState(session, BearIdentity, ObjectType.Npc, 28422);
            Check(session.TryGetLoadedObject(BearIdentity, out WorldObject bearRuntime),
                "authentic bear has one production presentation");
            WorldObject pcRuntime = lifecycle.Presentation;
            Vector2Int clearTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 3);
            MoveActor(session, loader, pc, pcRuntime, clearTile, controlled: true);
            Check(InteractionRangeRules.Distance(clearTile, bearRuntime.Tile) == 3
                  && loader.NavigationMap.HasProjectileLineOfFire(clearTile, bearRuntime.Tile),
                "authentic target is at source distance 3 with clear source-grid LOS");

            RollbackSnapshot inactive = new(session);
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure == CombatFailure.Inactive,
                "ranged attack while combat is inactive fails");
            CheckRollback(session, inactive, "inactive ranged attack");

            StartCombat(session, pc);
            RollbackSnapshot outOfTurn = new(session);
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.NotCurrentParticipant, "out-of-turn ranged attack fails");
            CheckRollback(session, outOfTurn, "out-of-turn ranged attack");
            Check(session.Combat.EndCurrentTurn(BearIdentity).Succeeded
                  && session.Combat.CurrentParticipant == pc && session.Combat.CurrentActionPoints == 8,
                "bear-to-PC turn handoff supplies 8 AP");

            int bearHpBefore = session.Vitality.GetCurrentHitPoints(BearIdentity);
            int bearFatigueBefore = session.Vitality.GetCurrentFatigue(BearIdentity);
            int resistance = session.DerivedStats.GetResistance(BearIdentity, CharacterResistance.Normal);
            CombatHitChance hitChance = session.Combat.GetBasicRangedHitChance(pc, BearIdentity,
                bow.WeaponData, 3);
            session.Combat.SetRandomSource(hitChance.DodgeChance > 0
                ? new SequenceRandom(1, 100, 100, 10, 5)
                : new SequenceRandom(1, 100, 10, 5));
            CombatAttackResult hit = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            int expectedHpDamage = 10 - resistance * 10 / 100;
            int expectedFatigueDamage = 5 - (3 * resistance / 4) * 5 / 100;
            Check(hit.Succeeded && hit.Hit && !hit.Dodged && hit.ActionPointCost == 6
                  && hit.ActionPointsSpent == 6 && hit.RawHitPointDamage == 10
                  && hit.RawFatigueDamage == 5 && hit.MitigatedHitPointDamage == expectedHpDamage
                  && hit.MitigatedFatigueDamage == expectedFatigueDamage
                  && hit.WeaponIdentity == BowIdentity && hit.AmmoIdentity == ArrowIdentity
                  && hit.AmmoQuantityBefore == 70 && hit.AmmoQuantityAfter == 69,
                "seeded hit spends AP/ammo once and resolves authentic raw/resisted damage");
            Check(session.Vitality.GetCurrentHitPoints(BearIdentity) == bearHpBefore - expectedHpDamage
                  && session.Vitality.GetCurrentFatigue(BearIdentity) == bearFatigueBefore - expectedFatigueDamage
                  && session.Combat.CurrentActionPoints == 2,
                "M4B vitality mutates once and the PC retains exactly 2 AP");

            NextPcTurn(session, pc);
            int missHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            int missFatigue = session.Vitality.GetCurrentFatigue(BearIdentity);
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult miss = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(miss.Succeeded && !miss.Hit && miss.ActionPointsSpent == 6
                  && miss.AmmoQuantityBefore == 69 && miss.AmmoQuantityAfter == 68
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == missHp
                  && session.Vitality.GetCurrentFatigue(BearIdentity) == missFatigue,
                "seeded miss spends AP/ammo once and applies zero vitality damage");

            NextPcTurn(session, pc);
            int authoredBeforeSelection = arrows.StackQuantity.Value;
            StackSplitResult selectionSplit = session.SplitStack(ArrowIdentity, 2);
            Check(selectionSplit.Succeeded && selectionSplit.CreatedState.StackQuantity == 2
                  && session.TryGetAmmo(pc, 0, 1, out ammo)
                  && ammo.Identity == selectionSplit.CreatedState.Identity,
                "deterministic stable-ID selection chooses the same lowest compatible stack");
            ArcanumObjectId selectedStack = selectionSplit.CreatedState.Identity;
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult selectedFirst = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(selectedFirst.Succeeded && selectedFirst.AmmoIdentity == selectedStack
                  && selectedFirst.AmmoQuantityBefore == 2 && selectedFirst.AmmoQuantityAfter == 1
                  && arrows.StackQuantity == authoredBeforeSelection - 2,
                "only the selected compatible stack decrements");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult selectedLast = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(selectedLast.Succeeded && selectedLast.AmmoIdentity == selectedStack
                  && selectedLast.AmmoQuantityAfter == 0 && session.IsObjectRemoved(selectedStack)
                  && arrows.StackQuantity == authoredBeforeSelection - 2,
                "selected stack depletion tombstones exactly that identity");
            Check(session.Combat.CurrentParticipant == BearIdentity,
                "PC overdraw reaches zero AP and advances exactly once");
            Check(session.Combat.EndCurrentTurn(BearIdentity).Succeeded
                  && session.Combat.CurrentParticipant == pc, "next PC turn follows depleted-stack attack");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult fallback = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(fallback.Succeeded && fallback.AmmoIdentity == ArrowIdentity
                  && fallback.AmmoQuantityBefore == authoredBeforeSelection - 2,
                "later attack deterministically falls back to the authored compatible stack");

            NextPcTurn(session, pc);
            StackSplitResult lastArrow = session.SplitStack(ArrowIdentity, 1);
            Check(lastArrow.Succeeded && lastArrow.CreatedState.StackQuantity == 1,
                "M3D creates one authoritative quantity-1 arrow stack");
            Check(session.TransferItem(ArrowIdentity, arrows.Placement,
                ObjectPlacement.ContainedBy(sourceContainer)).Succeeded,
                "remaining authored arrows move out through M3A for depletion proof");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult depletion = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(depletion.Succeeded && depletion.AmmoIdentity == lastArrow.CreatedState.Identity
                  && depletion.AmmoQuantityBefore == 1 && depletion.AmmoQuantityAfter == 0
                  && session.IsObjectRemoved(lastArrow.CreatedState.Identity),
                "quantity-1 attack consumes and tombstones the final PC arrow stack");
            RollbackSnapshot noAmmo = new(session);
            session.Combat.SetRandomSource(new SequenceRandom());
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure == CombatFailure.NoAmmo,
                "next ranged attack returns explicit NoAmmo without RNG");
            CheckRollback(session, noAmmo, "NoAmmo failure");
            Check(session.TransferItem(ArrowIdentity, arrows.Placement,
                ObjectPlacement.ContainedBy(pc)).Succeeded, "authored arrows return through M3A");

            Vector2Int outOfRangeTile = FindOutOfRangeTile(loader.NavigationMap, bearRuntime.Tile, 15);
            MoveActor(session, loader, pc, pcRuntime, outOfRangeTile, controlled: true);
            RollbackSnapshot rangeFailure = new(session);
            session.Combat.SetRandomSource(new SequenceRandom());
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.OutOfRange, "distance beyond source range 15 fails explicitly");
            CheckRollback(session, rangeFailure, "out-of-range failure");

            FindBlockedPair(loader.NavigationMap, out Vector2Int blockedSource, out Vector2Int blockedTarget);
            MoveActor(session, loader, pc, pcRuntime, blockedSource, controlled: true);
            MoveActor(session, loader, BearIdentity, bearRuntime, blockedTarget, controlled: false);
            RollbackSnapshot blockedLos = new(session);
            session.Combat.SetRandomSource(new SequenceRandom());
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.LineOfFireBlocked,
                "authentic combat-sector wall/scenery blocks source-grid LOS");
            CheckRollback(session, blockedLos, "blocked-geometry LOS failure");
            Vector2Int clearAfterBlock = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 2, 15);
            MoveActor(session, loader, pc, pcRuntime, clearAfterBlock, controlled: true);
            Check(loader.NavigationMap.HasProjectileLineOfFire(clearAfterBlock, bearRuntime.Tile),
                "clear combat-sector geometry permits source-grid LOS");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult clearLosShot = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(clearLosShot.Succeeded && !clearLosShot.Hit,
                "the same ranged transaction succeeds on clear authentic geometry");

            Check(session.Combat.CurrentParticipant == BearIdentity
                  && session.Combat.EndCurrentTurn(BearIdentity).Succeeded,
                "clear-LOS overdraw advances to the bear and back to PC");
            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "M3C unequips the bow through authoritative equipment state");
            RollbackSnapshot noWeapon = new(session);
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.UnsupportedWeapon, "no equipped weapon fails explicitly");
            CheckRollback(session, noWeapon, "no-weapon failure");
            Check(session.EquipItem(pc, BowIdentity, WornLocation.Weapon).Succeeded,
                "authentic bow re-equips with unchanged identity");

            ItemCreationResult meleeWeapon = session.CreateItem(MeleeWeaponPrototype,
                ObjectPlacement.ContainedBy(pc));
            Check(meleeWeapon.Succeeded && session.EquipItem(pc, meleeWeapon.State.Identity,
                      WornLocation.Weapon).Succeeded, "source melee weapon occupies the authoritative weapon slot");
            RollbackSnapshot invalidWeapon = new(session);
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.UnsupportedWeapon, "unrelated equipped weapon fails ranged attack");
            CheckRollback(session, invalidWeapon, "invalid-weapon failure");
            Check(session.EquipItem(pc, BowIdentity, WornLocation.Weapon).Succeeded
                  && session.TryGetEquippedItem(pc, WornLocation.Weapon, out equipped)
                  && equipped.Identity == BowIdentity, "ordinary attack setup restores exact bow equipment identity");

            Check(session.TransferItem(ArrowIdentity, arrows.Placement,
                ObjectPlacement.ContainedBy(sourceContainer)).Succeeded, "arrows leave PC for incompatible-ammo proof");
            ItemCreationResult bullets = session.CreateItem(BulletPrototype, ObjectPlacement.ContainedBy(pc));
            Check(bullets.Succeeded && bullets.State.AmmoItemType != 0,
                "source bullet stack supplies incompatible ammunition");
            RollbackSnapshot incompatibleAmmo = new(session);
            session.Combat.SetRandomSource(new SequenceRandom());
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.IncompatibleAmmo, "wrong ammunition type fails before AP/RNG");
            CheckRollback(session, incompatibleAmmo, "incompatible-ammo failure");
            Check(session.TransferItem(bullets.State.Identity, bullets.State.Placement,
                      ObjectPlacement.ContainedBy(sourceContainer)).Succeeded
                  && session.TransferItem(ArrowIdentity, arrows.Placement,
                      ObjectPlacement.ContainedBy(pc)).Succeeded,
                "ammo fixtures return through authoritative transfers");

            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Succeeded
                  && session.Combat.CurrentActionPoints == 2,
                "ordinary ranged miss prepares the audited PC overdraw boundary");
            int fatigueBeforeLow = session.Vitality.GetCurrentFatigue(pc);
            session.Vitality.ApplyFatigueDamage(pc, fatigueBeforeLow - 1);
            RollbackSnapshot insufficient = new(session);
            session.Combat.SetRandomSource(new SequenceRandom());
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.InsufficientActionPoints, "low-fatigue PC cannot overdraw a ranged attack");
            CheckRollback(session, insufficient, "insufficient-AP failure");
            session.Vitality.RestoreFatigue(pc, fatigueBeforeLow - 1);

            RollbackSnapshot invalidTarget = new(session);
            Check(session.Combat.Attack(pc, MissingIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.ParticipantNotRegistered, "invalid target fails explicitly");
            CheckRollback(session, invalidTarget, "invalid-target failure");
            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            session.Vitality.ApplyHitPointDamage(pc, pcHp);
            RollbackSnapshot deadActor = new(session);
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.ParticipantUnavailable, "dead current actor cannot attack");
            CheckRollback(session, deadActor, "dead-actor failure");
            session.Vitality.RestoreHitPoints(pc, pcHp);
            int currentBearHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            session.Vitality.ApplyHitPointDamage(BearIdentity, currentBearHp);
            RollbackSnapshot deadTarget = new(session);
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.ParticipantUnavailable, "dead target cannot be attacked");
            CheckRollback(session, deadTarget, "dead-target failure");
            session.Vitality.RestoreHitPoints(BearIdentity, currentBearHp);

            EndCombat(session, pc);
            clearTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, clearTile, controlled: true);
            StartPcTurn(session, pc);
            Vector2Int step = FindRangedStep(loader.NavigationMap, pcRuntime.Tile, bearRuntime.Tile);
            int moveStartAp = session.Combat.CurrentActionPoints;
            CombatMoveResult move = session.Combat.MoveInCombat(pc, step, pcAlwaysRun: true);
            Check(move.Succeeded && move.ActionPointsSpent == 1 && session.Combat.CurrentActionPoints == moveStartAp - 1
                  && session.States.TryGetValue(pc, out _) == false
                  && session.PlayerState.TilePosition == step,
                "combat movement spends 1 AP and updates authoritative PC placement");
            Check(loader.NavigationMap.HasProjectileLineOfFire(step, bearRuntime.Tile)
                  && InteractionRangeRules.Distance(step, bearRuntime.Tile) <= bow.WeaponData.Range,
                "range and LOS recalculate from the moved authoritative position");
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            CombatAttackResult movedShot = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
            Check(movedShot.Succeeded && movedShot.ActionPointsSpent == 6
                  && session.Combat.CurrentActionPoints == 1,
                "movement and ranged attack share one turn with exact total AP accounting");

            NextPcTurn(session, pc);
            ArcanumObjectId graphicsCurrent = session.Combat.CurrentParticipant;
            int graphicsRound = session.Combat.RoundNumber;
            int graphicsAp = session.Combat.CurrentActionPoints;
            int graphicsAmmo = arrows.StackQuantity.Value;
            int graphicsBearHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            int graphicsBearFatigue = session.Vitality.GetCurrentFatigue(BearIdentity);
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Combat.IsActive && session.Combat.CurrentParticipant == graphicsCurrent
                      && session.Combat.RoundNumber == graphicsRound && session.Combat.CurrentActionPoints == graphicsAp
                      && session.TryGetEquippedItem(pc, WornLocation.Weapon, out equipped)
                      && equipped.Identity == BowIdentity && arrows.StackQuantity == graphicsAmmo
                      && session.Vitality.GetCurrentHitPoints(BearIdentity) == graphicsBearHp
                      && session.Vitality.GetCurrentFatigue(BearIdentity) == graphicsBearFatigue,
                    $"{mode} rebuild preserves combat/equipment/ammo/vitality without duplication");
            }
            session.Combat.SetRandomSource(new SequenceRandom(100, 100));
            Check(session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged).Succeeded,
                "ranged attack still succeeds after Original-Enhanced-Original rebuild");

            int retainedAmmo = arrows.StackQuantity.Value;
            int retainedBearHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            int retainedBearFatigue = session.Vitality.GetCurrentFatigue(BearIdentity);
            EndCombat(session, pc);
            Check(session.Vitality.GetCurrentHitPoints(BearIdentity) == retainedBearHp
                  && session.Vitality.GetCurrentFatigue(BearIdentity) == retainedBearFatigue
                  && arrows.StackQuantity == retainedAmmo
                  && session.TryGetEquippedItem(pc, WornLocation.Weapon, out equipped)
                  && equipped.Identity == BowIdentity,
                "EndCombat clears transient state while damage/ammo/equipment remain authoritative");
            StartPcTurn(session, pc);
            Check(arrows.StackQuantity == retainedAmmo
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == retainedBearHp,
                "restarted combat reuses previously spent ammo and vitality damage");
            EndCombat(session, pc);

            StartPcTurn(session, pc);
            savePresenter.Open(SaveLoadPanelMode.Save);
            _temporarySlot = savePresenter.Controller.SuggestedSlotId;
            savePresenter.Controller.SelectNewSlot();
            savePresenter.Controller.RequestSave();
            Check(string.IsNullOrEmpty(savePresenter.Controller.ErrorMessage)
                  && savePresenter.Controller.SelectedSlotId == _temporarySlot && session.Combat.IsActive,
                "save UI stores committed ranged state while combat remains transient");
            Check(!session.SaveGames.SerializeCurrentSession().Contains("combat", StringComparison.OrdinalIgnoreCase),
                "Save V1 contains no transient combat payload");
            savePresenter.Open(SaveLoadPanelMode.Load);
            Check(savePresenter.Controller.SelectSlot(_temporarySlot), "temporary M8C slot is selectable");
            savePresenter.Controller.RequestLoad();
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out savePresenter);
            Check(!session.Combat.IsActive && session.Combat.Participants.Count == 0,
                "load normalizes active combat to Inactive");
            bow = RequireState(session, BowIdentity, ObjectType.Weapon, BowPrototype);
            arrows = RequireState(session, ArrowIdentity, ObjectType.Ammo, ArrowPrototype);
            Check(session.TryGetEquippedItem(pc, WornLocation.Weapon, out equipped)
                  && equipped.Identity == BowIdentity && arrows.StackQuantity == retainedAmmo
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == retainedBearHp
                  && session.Vitality.GetCurrentFatigue(BearIdentity) == retainedBearFatigue,
                "load restores exact equipment/ammo/vitality with no stale combat session");
            Check(session.SaveSlots.DeleteSlot(_temporarySlot).Succeeded, "temporary M8C slot is removed");
            _temporarySlot = null;

            StartPcTurn(session, pc);
            session.ClearSelectedSector();
            yield return null;
            Check(!session.Combat.IsActive && !session.HasSelectedSector,
                "sector unload normalizes transient combat");
            Check(session.SelectSector(CombatSector), "combat sector reloads after unload");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out savePresenter);
            Check(session.TryGetEquippedItem(pc, WornLocation.Weapon, out equipped)
                  && equipped.Identity == BowIdentity
                  && RequireState(session, ArrowIdentity, ObjectType.Ammo, ArrowPrototype).StackQuantity == retainedAmmo
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == retainedBearHp,
                "unload/reload preserves committed equipment/ammo/vitality");

            StartPcTurn(session, pc);
            Check(session.SelectSector(OtherMapSector), "cross-map selection succeeds during ranged combat");
            yield return null;
            Check(!session.Combat.IsActive && session.SelectedSector == OtherMapSector,
                "cross-map transition normalizes transient combat");
            Check(session.SelectSector(CombatSector), "authentic combat sector restores after transition");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out savePresenter);
            Check(session.TryGetEquippedItem(pc, WornLocation.Weapon, out equipped)
                  && equipped.Identity == BowIdentity
                  && RequireState(session, ArrowIdentity, ObjectType.Ammo, ArrowPrototype).StackQuantity == retainedAmmo
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == retainedBearHp,
                "transition preserves committed ranged state without combat duplication");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M8C PLAYMODE VALIDATION PASS: bow={BowIdentity}; arrows={ArrowIdentity}; "
                      + $"target={BearIdentity}; AP=6; hitRaw=10/5; hitMitigated={expectedHpDamage}/{expectedFatigueDamage}; "
                      + "missDamage=0; selection=stable-ID; depletion=tombstoned->NoAmmo; range=15; "
                      + "LOS=authentic-geometry-blocked/clear; portal="
                      + (physicalPortalValidated ? "authentic-closed/open" : "no-qualifying-authentic-fixture") + "; "
                      + "movementAttack=1+6AP; "
                      + "graphics=Original->Enhanced->Original; endCombat=state-retained; saveV1=TransientExcluded; "
                      + $"load/unload/mapChange=Inactive+state-retained; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (!string.IsNullOrEmpty(_temporarySlot)) session.SaveSlots.DeleteSlot(_temporarySlot);
            _temporarySlot = null;
            _running = false;
        }
    }

    private static void StartCombat(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded, "production StartCombat succeeds");
        Check(session.Combat.CurrentParticipant == BearIdentity && session.Combat.CurrentActionPoints == 5,
            "authentic bear owns deterministic first turn with AP 5");
    }

    private static void StartPcTurn(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        StartCombat(session, pc);
        Check(session.Combat.EndCurrentTurn(BearIdentity).Succeeded
              && session.Combat.CurrentParticipant == pc && session.Combat.CurrentActionPoints == 8,
            "PC turn starts with source Speed AP 8");
    }

    private static void NextPcTurn(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.CurrentParticipant == pc && session.Combat.EndCurrentTurn(pc).Succeeded
              && session.Combat.CurrentParticipant == BearIdentity
              && session.Combat.EndCurrentTurn(BearIdentity).Succeeded
              && session.Combat.CurrentParticipant == pc && session.Combat.CurrentActionPoints == 8,
            "turn lifecycle advances bear-to-PC exactly once");
    }

    private static void EndCombat(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.RemoveParticipant(BearIdentity).Succeeded
              && session.Combat.EndCombat(pc).Succeeded && !session.Combat.IsActive,
            "EndCombat clears the transient ranged session");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.Placement.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "source equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(owner)).Succeeded,
                "authentic item transfers through M3A containment authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile, bool controlled)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement state accepts validation placement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        if (controlled) loader.NavigationMap.SetControlledObject(runtime);
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
        throw new InvalidOperationException("M8C validation FAIL: no clear in-range source tile.");
    }

    private static Vector2Int FindOutOfRangeTile(SectorNavigationMap map, Vector2Int target, int range)
    {
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            var candidate = new Vector2Int(x, y);
            if (map.IsWalkable(candidate) && InteractionRangeRules.Distance(candidate, target) > range)
                return candidate;
        }
        throw new InvalidOperationException("M8C validation FAIL: no out-of-range source tile.");
    }

    private static Vector2Int FindRangedStep(SectorNavigationMap map, Vector2Int start, Vector2Int target)
    {
        for (int rotation = 0; rotation < IsoProjection.DirDelta.Length; rotation++)
        {
            Vector2Int candidate = start + IsoProjection.DirDelta[rotation];
            if (map.CanTraverse(start, rotation)
                && InteractionRangeRules.Distance(candidate, target) <= 15
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M8C validation FAIL: no one-step ranged attack position.");
    }

    private static void FindBlockedPair(SectorNavigationMap map, out Vector2Int source, out Vector2Int target)
    {
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
                    || map.HasProjectileLineOfFire(candidateSource, candidateTarget)) continue;
                source = candidateSource;
                target = candidateTarget;
                return;
            }
        }
        throw new InvalidOperationException("M8C validation FAIL: no authentic blocked LOS pair.");
    }

    private static bool TryFindPortalPair(WorldObjectSectorLoader loader, out WorldObject portal,
        out Vector2Int source, out Vector2Int target)
    {
        foreach (WorldObjectSpriteOwner owner in loader.SpriteOwners
                     .Where(value => value?.WorldObject != null
                                     && value.WorldObject.Type == ObjectType.Portal
                                     && value.WorldObject.Identity.IsPersistent
                                     && !value.WorldObject.Off && !value.WorldObject.Locked
                                     && value.WorldObject.PortalOpenable)
                     .OrderBy(value => value.WorldObject.Identity.Key, StringComparer.Ordinal))
        {
            WorldObject candidate = owner.WorldObject;
            bool wasOpen = candidate.IsOpen;
            Vector2Int center = candidate.Tile;
            for (int sourceY = Math.Max(0, center.y - 4); sourceY <= Math.Min(63, center.y + 4); sourceY++)
            for (int sourceX = Math.Max(0, center.x - 4); sourceX <= Math.Min(63, center.x + 4); sourceX++)
            for (int targetY = Math.Max(0, center.y - 4); targetY <= Math.Min(63, center.y + 4); targetY++)
            for (int targetX = Math.Max(0, center.x - 4); targetX <= Math.Min(63, center.x + 4); targetX++)
            {
                var from = new Vector2Int(sourceX, sourceY);
                var to = new Vector2Int(targetX, targetY);
                if (from == to || InteractionRangeRules.Distance(from, to) > 8) continue;
                candidate.IsOpen = false;
                bool blockedWhenClosed = !loader.NavigationMap.HasProjectileLineOfFire(from, to);
                candidate.IsOpen = true;
                bool clearWhenOpen = loader.NavigationMap.HasProjectileLineOfFire(from, to);
                if (!blockedWhenClosed || !clearWhenOpen) continue;
                candidate.IsOpen = wasOpen;
                portal = candidate;
                source = from;
                target = to;
                return true;
            }
            candidate.IsOpen = wasOpen;
        }
        portal = null;
        source = target = default;
        return false;
    }

    private static IEnumerator SetPortal(WorldMapSessionCoordinator session, WorldObject portal, bool open)
    {
        if (portal.IsOpen == open && session.States[portal.Identity].PortalOpen == open) yield break;
        Check(portal.RequestPortalOpen(open), $"authentic portal accepts {(open ? "open" : "close")} request");
        float deadline = Time.realtimeSinceStartup + 8f;
        while (session.Portals.TryGetPhase(portal.Identity, out PortalPhase phase)
               && phase is PortalPhase.Opening or PortalPhase.Closing
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline && portal.IsOpen == open
              && session.States[portal.Identity].PortalOpen == open,
            $"authentic portal reaches stable {(open ? "open" : "closed")} state");
    }

    private static PersistentObjectState RequireState(WorldMapSessionCoordinator session, ArcanumObjectId identity,
        ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state),
            $"exact {type} ObjectID resolves");
        Check(state.Type == type && state.PrototypeNumber == prototype,
            $"exact {type} prototype {prototype} resolves");
        return state;
    }

    private static void CheckRollback(WorldMapSessionCoordinator session, RollbackSnapshot expected, string label)
        => Check(expected.Equals(new RollbackSnapshot(session)), label + " preserves AP/ammo/equipment/vitality/positions/turn");

    private readonly struct RollbackSnapshot : IEquatable<RollbackSnapshot>
    {
        private readonly string _sessionJson;
        private readonly CombatLifecycle _lifecycle;
        private readonly ArcanumObjectId _current;
        private readonly int _round;
        private readonly int _ap;
        private readonly int _participants;

        public RollbackSnapshot(WorldMapSessionCoordinator session)
        {
            _sessionJson = session.SaveGames.SerializeCurrentSession();
            _lifecycle = session.Combat.Lifecycle;
            _current = session.Combat.CurrentParticipant;
            _round = session.Combat.RoundNumber;
            _ap = session.Combat.CurrentActionPoints;
            _participants = session.Combat.Participants.Count;
        }

        public bool Equals(RollbackSnapshot other)
            => _sessionJson == other._sessionJson && _lifecycle == other._lifecycle
               && _current == other._current && _round == other._round && _ap == other._ap
               && _participants == other._participants;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);

        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "deterministic RNG sample is inside source roll bounds");
            return value;
        }
    }

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle,
        out PlayerNavigationController navigation, out ProductionSaveLoadPresenter savePresenter)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        savePresenter = Object.FindFirstObjectByType<ProductionSaveLoadPresenter>();
        Check(loader != null && lifecycle != null && navigation != null && savePresenter != null,
            "production TestTerrain composition remains available");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8C validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
