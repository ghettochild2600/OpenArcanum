using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.World;
using Arcanum.Runtime;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M10AMagicRuntime")]
    public sealed class M10AMagicRuntimeTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/47781512457.sec";
        private const int OnfKos = 0x00000100;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _target;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M10AMagicRuntimeTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject pcRuntime = Runtime("Player", ObjectType.Pc);
            _session.BindPlayer(Sector, _pc, pcRuntime);
            _target = AddNpc("G_11111111_1111_1111_1111_111111111111", new Vector2Int(2, 1),
                Stats(8, 8, 8, 1), OnfKos, out WorldObject npcRuntime);
            SectorNavigationMap map = Map();
            map.Register(pcRuntime, 0);
            map.Register(npcRuntime, 0);
            map.SetControlledObject(pcRuntime);
            _session.Combat.BindNavigationMap(map);
            _session.Magic.SetKnownCollegeRank(_pc.Identity, SpellCollege.Earth, 1);
            _session.Magic.SetKnownCollegeRank(_pc.Identity, SpellCollege.NecromanticBlack, 1);
            _session.Magic.SetKnownCollegeRank(_pc.Identity, SpellCollege.NecromanticWhite, 1);
            _session.Magic.SetKnownCollegeRank(_pc.Identity, SpellCollege.Phantasm, 2);
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 99, 99, 99));
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void CatalogMatchesTheThreeAuditedRetailDefinitions()
        {
            AssertDefinition(15, "Strength of Earth", SpellCollege.Earth, true, 5, 99);
            AssertDefinition(55, "Harm", SpellCollege.NecromanticBlack, false, 5, 99);
            AssertDefinition(60, "Minor Healing", SpellCollege.NecromanticWhite, false, 5, 99);
            Assert.That(PhaseOneSpellCatalog.TryGet(15, out SpellDefinition earth), Is.True);
            Assert.That((earth.UpkeepFatigueCost, earth.UpkeepPeriodMilliseconds, earth.AttributeMagnitude),
                Is.EqualTo((1, 80_000, 4)));
            Assert.That(PhaseOneSpellCatalog.TryGet(55, out SpellDefinition harm), Is.True);
            Assert.That((harm.MinimumMagnitude, harm.MaximumMagnitude), Is.EqualTo((3, 40)));
            Assert.That(PhaseOneSpellCatalog.TryGet(60, out SpellDefinition heal), Is.True);
            Assert.That((heal.MinimumMagnitude, heal.MaximumMagnitude), Is.EqualTo((5, 30)));
        }

        [Test]
        public void InvalidAndUnknownSpellsFailBeforeMutation()
        {
            int fatigue = Fatigue(_pc.Identity);
            int hp = Hp(_target);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 999, _target)).Failure,
                Is.EqualTo(SpellCastFailure.InvalidSpell));
            _session.Magic.SetKnownCollegeRank(_pc.Identity, SpellCollege.NecromanticBlack, 0);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).Failure,
                Is.EqualTo(SpellCastFailure.SpellNotKnown));
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue, hp)));
        }

        [Test]
        public void DeadAndUnconsciousCastersAreRejectedWithoutMutation()
        {
            int hp = Hp(_target);
            _session.Vitality.ApplyFatigueDamage(_pc.Identity, Fatigue(_pc.Identity));
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).Failure,
                Is.EqualTo(SpellCastFailure.CasterUnavailable));
            Assert.That(Hp(_target), Is.EqualTo(hp));
        }

        [Test]
        public void HarmUsesDeterministicAptitudeScalingAndM4Vitality()
        {
            int hp = Hp(_target);
            int fatigue = Fatigue(_pc.Identity);
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Magnitude, Is.EqualTo(3));
            Assert.That(Hp(_target), Is.EqualTo(hp - 3));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue - 5));
        }

        [Test]
        public void HarmPartialMagicResistanceReducesDamageWithMinimumOne()
        {
            ArcanumObjectId resistant = AddNpc("G_22222222_2222_2222_2222_222222222222",
                new Vector2Int(3, 1), Stats(8, 8, 8, 1), OnfKos, out _, magicResistance: 80);
            _session.Magic.SetRandomSource(new SequenceMagicRandom(20));
            int hp = Hp(resistant);
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, resistant));
            Assert.That(result.ResistancePercent, Is.EqualTo(60));
            Assert.That(result.Magnitude, Is.EqualTo(1));
            Assert.That(Hp(resistant), Is.EqualTo(hp - 1));
        }

        [Test]
        public void MinorHealingUsesExactScaledAmountAndCapsAtMaximum()
        {
            _session.Vitality.ApplyHitPointDamage(_target, 3);
            int maximum = _session.Vitality.GetMaximumHitPoints(_target);
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity, 60, _target));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Magnitude, Is.EqualTo(5));
            Assert.That(Hp(_target), Is.EqualTo(maximum));
        }

        [Test]
        public void HealingUndamagedOrDeadTargetFailsBeforeFatigueMutation()
        {
            int fatigue = Fatigue(_pc.Identity);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 60, _target)).Failure,
                Is.EqualTo(SpellCastFailure.InvalidTarget));
            _session.Vitality.ApplyHitPointDamage(_target, Hp(_target));
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 60, _target)).Failure,
                Is.EqualTo(SpellCastFailure.TargetUnavailable));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue));
        }

        [Test]
        public void StrengthOfEarthAppliesOnceAndExplicitEndRevertsCleanly()
        {
            int strength = _session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength);
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity, 15, _pc.Identity));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(_session.Characters.GetBaseAttribute(_pc.Identity, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength),
                Is.EqualTo(strength + 4));
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 15, _pc.Identity)).Failure,
                Is.EqualTo(SpellCastFailure.DuplicateEffect));
            Assert.That(_session.Magic.EndEffect(result.ActiveEffectId.Value), Is.True);
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength),
                Is.EqualTo(strength));
        }

        [Test]
        public void StrengthOfEarthPaysExactUpkeepOnSourceTimeAndExpiresOnFailure()
        {
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity, 15, _pc.Identity));
            int fatigue = Fatigue(_pc.Identity);
            _session.Magic.AdvanceTime(79_999);
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue));
            _session.Magic.AdvanceTime(1);
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue - 1));
            _session.Vitality.ApplyFatigueDamage(_pc.Identity, Fatigue(_pc.Identity) + 14);
            _session.Magic.AdvanceTime(80_000);
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
            Assert.That(_session.Magic.EndEffect(result.ActiveEffectId.Value), Is.False);
        }

        [Test]
        public void MaintainedSpellCannotStartBelowZeroButInstantSpellMayOverexertAboveFloor()
        {
            int current = Fatigue(_pc.Identity);
            _session.Vitality.ApplyFatigueDamage(_pc.Identity, current - 4);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 15, _pc.Identity)).Failure,
                Is.EqualTo(SpellCastFailure.InsufficientFatigue));
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).Succeeded, Is.True);
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(-1));
        }

        [Test]
        public void DwarfAndMasteryApplyExactSourceCastCostRules()
        {
            _session.Characters.SetRace(_pc.Identity, CharacterRace.Dwarf);
            int fatigue = Fatigue(_pc.Identity);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).FatigueCost,
                Is.EqualTo(10));
            _session.Vitality.RestoreFatigue(_pc.Identity, 10);
            _session.Magic.SetMastery(_pc.Identity, SpellCollege.NecromanticBlack);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).FatigueCost,
                Is.EqualTo(5));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue - 5));
        }

        [Test]
        public void TurnBasedCastConsumesFourApAndAdvancesOnlyAtZero()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(8));
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target));
            Assert.That(result.ActionPointCost, Is.EqualTo(4));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(4));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void RealTimeCastUsesM8HSchedulingAndResolvesExactlyOnceAtEffectTime()
        {
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target, CombatMode.RealTime).Succeeded, Is.True);
            int fatigue = Fatigue(_pc.Identity);
            int hp = Hp(_target);
            var request = new SpellCastRequest(_pc.Identity, 55, _target);

            Assert.That(_session.Combat.ScheduleRealTimeSpell(request).Succeeded, Is.True);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out var busy)
                        && busy.HasPendingAction, Is.True);
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue, hp)));
            Assert.That(_session.Combat.AdvanceRealTime(49).Succeeded, Is.True);
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue, hp)));
            Assert.That(_session.Combat.AdvanceRealTime(1).Succeeded, Is.True);
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue - 5, hp - 3)));
            Assert.That(_session.Combat.LastRealTimeActionResolution?.SpellResult?.Succeeded, Is.True);
            Assert.That(_session.Combat.AdvanceRealTime(50).Succeeded, Is.True);
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue - 5, hp - 3)));
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out var ready) && ready.IsReady, Is.True);
        }

        [Test]
        public void SaveV1RestoresKnowledgeAndMaintainedEffectsButNotPendingCastTransactions()
        {
            SpellCastResult maintained = _session.Magic.Cast(
                new SpellCastRequest(_pc.Identity, PhaseOneSpellCatalog.StrengthOfEarth, _pc.Identity));
            Assert.That(maintained.Succeeded, Is.True);
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength),
                Is.EqualTo(12));

            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target, CombatMode.RealTime).Succeeded, Is.True);
            int fatigueBeforePending = Fatigue(_pc.Identity);
            int targetHitPoints = Hp(_target);
            Assert.That(_session.Combat.ScheduleRealTimeSpell(
                new SpellCastRequest(_pc.Identity, PhaseOneSpellCatalog.Harm, _target)).Succeeded, Is.True);
            Assert.That(_session.Combat.TryGetRealTimeActorState(_pc.Identity, out var pending)
                        && pending.HasPendingAction, Is.True);

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"version\": 1"));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Magic.KnowsSpell(_pc.Identity, PhaseOneSpellCatalog.Harm), Is.True);
            Assert.That(_session.Magic.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(_session.Magic.ActiveEffects[0].Id, Is.EqualTo(maintained.ActiveEffectId));
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength),
                Is.EqualTo(12));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigueBeforePending));
            Assert.That(Hp(_target), Is.EqualTo(targetHitPoints));
        }

        [Test]
        public void PrerequisiteFailureIsTransactional()
        {
            ArcanumObjectId caster = AddNpc("G_33333333_3333_3333_3333_333333333333",
                new Vector2Int(3, 1), Stats(8, 8, 5, 1), 0, out _);
            _session.Magic.SetKnownCollegeRank(caster, SpellCollege.NecromanticBlack, 1);
            int fatigue = Fatigue(caster);
            int hp = Hp(_target);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(caster, 55, _target)).Failure,
                Is.EqualTo(SpellCastFailure.PrerequisiteNotMet));
            Assert.That((Fatigue(caster), Hp(_target)), Is.EqualTo((fatigue, hp)));
        }

        [Test]
        public void HardLineOfSightBlockRejectsBeforeMutation()
        {
            var blocks = new bool[SectorTerrain.TileCount];
            blocks[2 + SectorTerrain.Size] = true;
            _session.Combat.BindNavigationMap(new SectorNavigationMap(
                new SectorTerrain(new uint[SectorTerrain.TileCount]), blocks,
                TileNameTable.FromMes(MesReader.Read("{300}{grs}"))));
            int fatigue = Fatigue(_pc.Identity);
            int hp = Hp(_target);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).Failure,
                Is.EqualTo(SpellCastFailure.LineOfSightBlocked));
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue, hp)));
        }

        [Test]
        public void MinorHealingRejectsMechanicalCritterBeforeMutation()
        {
            ArcanumObjectId mechanical = AddNpc("G_44444444_4444_4444_4444_444444444444",
                new Vector2Int(3, 1), Stats(8, 8, 8, 1), 0, out _, critterFlags: unchecked((int)0x20000000));
            _session.Vitality.ApplyHitPointDamage(mechanical, 3);
            int fatigue = Fatigue(_pc.Identity);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 60, mechanical)).Failure,
                Is.EqualTo(SpellCastFailure.InvalidTarget));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue));
        }

        [Test]
        public void LethalHarmProcessesM8DAndM8EConsequencesExactlyOnce()
        {
            ArcanumObjectId victim = AddNpc("G_55555555_5555_5555_5555_555555555555",
                new Vector2Int(3, 1), Stats(8, 8, 8, 1), OnfKos, out _, experienceWorth: 12);
            _session.Vitality.ApplyHitPointDamage(victim, Hp(victim) - 3);
            int experience = _session.Progression.GetExperience(_pc.Identity);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, victim)).Succeeded, Is.True);
            Assert.That(_session.Vitality.IsDead(victim), Is.True);
            Assert.That(_session.TryGetObjectState(victim, out PersistentObjectState corpse)
                        && corpse.DeathConsequencesProcessed, Is.True);
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(experience + 2));
            Assert.That(_session.DeathConsequences.Process(_pc.Identity, victim).Failure,
                Is.EqualTo(DeathConsequenceFailure.AlreadyProcessed));
            Assert.That(_session.Progression.GetExperience(_pc.Identity), Is.EqualTo(experience + 2));
        }

        [Test]
        public void CombatOwnershipFailureIsTransactional()
        {
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);
            int fatigue = Fatigue(_pc.Identity);
            int hp = Hp(_target);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _target)).Failure,
                Is.EqualTo(SpellCastFailure.CombatRejected));
            Assert.That((Fatigue(_pc.Identity), Hp(_target)), Is.EqualTo((fatigue, hp)));
        }

        [Test]
        public void SelfHarmAndCrossSectorTargetsAreRejectedTransactionally()
        {
            int fatigue = Fatigue(_pc.Identity);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity, 55, _pc.Identity)).Failure,
                Is.EqualTo(SpellCastFailure.InvalidTarget));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue));
        }

        [Test]
        public void SourceCollegeRankControlsSpellKnowledge()
        {
            int[] source = new int[17];
            source[(int)SpellCollege.NecromanticBlack] = 1;
            _session.Magic.RegisterSourceCharacter(_target, source, null);
            Assert.That(_session.Magic.KnowsSpell(_target, 55), Is.True);
            Assert.That(_session.Magic.KnowsSpell(_target, 56), Is.False);
        }

        [Test, Category("M10APhase2")]
        public void SourceClockUsesRetailScaleCapPauseAndTurnBoundary()
        {
            SourceTimeService clock = _session.SourceTime;
            clock.PollNonCombat(1_000, false);
            clock.PollNonCombat(1_004, false);
            Assert.That(clock.ElapsedMilliseconds, Is.Zero);
            clock.PollNonCombat(1_010, false);
            Assert.That(clock.ElapsedMilliseconds, Is.EqualTo(80));
            clock.PollNonCombat(2_000, false);
            Assert.That(clock.ElapsedMilliseconds, Is.EqualTo(2_080));
            clock.SetPaused(true);
            clock.PollNonCombat(2_100, false);
            Assert.That(clock.ElapsedMilliseconds, Is.EqualTo(2_080));
            clock.SetPaused(false);
            clock.AdvanceTurnBasedRound();
            Assert.That(clock.ElapsedMilliseconds, Is.EqualTo(3_080));
        }

        [Test, Category("M10APhase2")]
        public void FlashDefinitionMatchesAuditedFiniteRetailSpell()
        {
            Assert.That(PhaseOneSpellCatalog.TryGet(PhaseOneSpellCatalog.Flash, out SpellDefinition flash), Is.True);
            Assert.That((flash.Name, flash.College, flash.Rank, flash.BaseFatigueCost,
                    flash.DurationSourceMilliseconds, flash.RuntimeCritterFlag, flash.NoStack,
                    flash.ResistanceAttribute, flash.ResistanceModifier),
                Is.EqualTo(("Flash", SpellCollege.Phantasm, 2, 10, 80_000, 0x80, true,
                    (CharacterAttribute?)CharacterAttribute.Constitution, -5)));
        }

        [Test, Category("M10APhase2")]
        public void FlashAppliesBlindedFlagAndExpiresAtExactSourceDeadline()
        {
            PrepareFlashCaster();
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 20));
            SpellCastResult result = _session.Magic.Cast(
                new SpellCastRequest(_pc.Identity, PhaseOneSpellCatalog.Flash, _target));
            Assert.That(result.Succeeded && result.ActiveEffectId.HasValue, Is.True);
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.True);
            _session.SourceTime.Advance(79_999);
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.True);
            _session.SourceTime.Advance(1);
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.False);
            Assert.That(_session.Magic.LastTermination?.Reason,
                Is.EqualTo(SpellEffectTerminationReason.NaturalExpiration));
        }

        [Test, Category("M10APhase2")]
        public void FlashSavingThrowConsumesCastButCreatesNoEffect()
        {
            PrepareFlashCaster();
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 1));
            int fatigue = Fatigue(_pc.Identity);
            SpellCastResult result = _session.Magic.Cast(
                new SpellCastRequest(_pc.Identity, PhaseOneSpellCatalog.Flash, _target));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ResistancePercent, Is.EqualTo(100));
            Assert.That(result.ActiveEffectId, Is.Null);
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue - 10));
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.False);
        }

        [Test, Category("M10APhase2")]
        public void FlashDuplicateAndMechanicalTargetRejectBeforeMutation()
        {
            PrepareFlashCaster();
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 20));
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.Flash, _target)).Succeeded, Is.True);
            int fatigue = Fatigue(_pc.Identity);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                    PhaseOneSpellCatalog.Flash, _target)).Failure,
                Is.EqualTo(SpellCastFailure.DuplicateEffect));
            ArcanumObjectId mechanical = AddNpc("G_66666666_6666_6666_6666_666666666666",
                new Vector2Int(3, 1), Stats(8, 8, 8, 1), 0, out _,
                critterFlags: unchecked((int)0x20000000));
            Assert.That(_session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                    PhaseOneSpellCatalog.Flash, mechanical)).Failure,
                Is.EqualTo(SpellCastFailure.InvalidTarget));
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue));
        }

        [Test, Category("M10APhase2")]
        public void MaintainedCancellationIsOwnedValidatedAndExactlyOnce()
        {
            int strength = _session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength);
            SpellCastResult result = _session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.StrengthOfEarth, _pc.Identity));
            Assert.That(_session.Magic.CancelMaintainedEffect(_target, result.ActiveEffectId.Value), Is.False);
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength),
                Is.EqualTo(strength + 4));
            Assert.That(_session.Magic.CancelMaintainedEffect(_pc.Identity, result.ActiveEffectId.Value), Is.True);
            Assert.That(_session.Magic.CancelMaintainedEffect(_pc.Identity, result.ActiveEffectId.Value), Is.False);
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity, CharacterAttribute.Strength),
                Is.EqualTo(strength));
            Assert.That(_session.Magic.LastTermination?.Reason,
                Is.EqualTo(SpellEffectTerminationReason.CasterCancellation));
        }

        [Test, Category("M10APhase2")]
        public void DispelEndsMatchingEffectsOnceAndStopsFutureUpkeep()
        {
            PrepareFlashCaster();
            SpellCastResult earth = _session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.StrengthOfEarth, _pc.Identity));
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 20));
            SpellCastResult flash = _session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.Flash, _target));
            int fatigue = Fatigue(_pc.Identity);
            Assert.That(earth.ActiveEffectId.HasValue && flash.ActiveEffectId.HasValue, Is.True);
            Assert.That(_session.Magic.DispelEffects(_pc.Identity), Is.EqualTo(2));
            Assert.That(_session.Magic.DispelEffects(_pc.Identity), Is.Zero);
            _session.SourceTime.Advance(80_000);
            Assert.That(Fatigue(_pc.Identity), Is.EqualTo(fatigue));
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
            Assert.That(_session.Magic.LastTermination?.Reason,
                Is.EqualTo(SpellEffectTerminationReason.Dispelled));
        }

        [Test, Category("M10APhase2")]
        public void TurnBasedAndRealTimeCombatAdvanceTheSameSourceClock()
        {
            long start = _session.SourceTime.ElapsedMilliseconds;
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_pc.Identity).Succeeded, Is.True);
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(start + 1_000));
            Assert.That(_session.Combat.RemoveParticipant(_target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCombat(_pc.Identity).Succeeded, Is.True);
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target, CombatMode.RealTime).Succeeded, Is.True);
            Assert.That(_session.Combat.AdvanceRealTime(250).Succeeded, Is.True);
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(start + 3_000));
        }

        [Test, Category("M10APhase2")]
        public void FiniteEffectAndClockRoundTripWithoutTransientCombat()
        {
            PrepareFlashCaster();
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 20));
            SpellCastResult flash = _session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.Flash, _target));
            _session.SourceTime.Advance(25_000);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(25_000));
            Assert.That(_session.Magic.ActiveEffects.Single().Id, Is.EqualTo(flash.ActiveEffectId));
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.True);
            _session.SourceTime.Advance(54_999);
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.True);
            _session.SourceTime.Advance(1);
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
        }

        [Test, Category("M10APhase2")]
        public void InvalidParticipantTerminationRemovesFiniteAndMaintainedEffects()
        {
            PrepareFlashCaster();
            SpellCastResult earth = _session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.StrengthOfEarth, _pc.Identity));
            _session.Magic.SetRandomSource(new SequenceMagicRandom(99, 20));
            SpellCastResult flash = _session.Magic.Cast(new SpellCastRequest(_pc.Identity,
                PhaseOneSpellCatalog.Flash, _target));
            Assert.That(earth.ActiveEffectId.HasValue && flash.ActiveEffectId.HasValue, Is.True);
            _session.Vitality.ApplyHitPointDamage(_target, Hp(_target));
            _session.SourceTime.Advance(1);
            Assert.That(_session.Magic.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(_session.Combat.HasCritterFlag(_target, 0x80), Is.False);
            _session.Vitality.ApplyFatigueDamage(_pc.Identity, Fatigue(_pc.Identity));
            _session.SourceTime.Advance(1);
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
            Assert.That(_session.Magic.LastTermination?.Reason,
                Is.EqualTo(SpellEffectTerminationReason.InvalidParticipant));
        }

        private void PrepareFlashCaster()
            => _session.Characters.SetEffectModifier(_pc.Identity, "test:m10a-phase2-willpower",
                CharacterAttribute.Willpower, 1);

        private void AssertDefinition(int id, string name, SpellCollege college, bool maintained,
            int fatigue, int range)
        {
            Assert.That(PhaseOneSpellCatalog.TryGet(id, out SpellDefinition value), Is.True);
            Assert.That((value.Name, value.College, value.Rank, value.BaseFatigueCost, value.Range, value.Maintained),
                Is.EqualTo((name, college, 1, fatigue, range, maintained)));
        }

        private int Hp(ArcanumObjectId identity) => _session.Vitality.GetCurrentHitPoints(identity);
        private int Fatigue(ArcanumObjectId identity) => _session.Vitality.GetCurrentFatigue(identity);

        private ArcanumObjectId AddNpc(string key, Vector2Int tile, int[] stats, int npcFlags,
            out WorldObject runtime, int magicResistance = 0, int critterFlags = 0,
            int experienceWorth = 0)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            const int prototype = 28001;
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            int[] resistance = new int[CharacterDerivedStatRules.ResistanceCount];
            resistance[(int)CharacterResistance.Magic] = magicResistance;
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, resistance, null, null, 50, npcFlags, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0, null, 0, null, 0, null, 0));
            _session.Magic.RegisterSourceCharacter(identity, new int[17], null);
            runtime = Runtime(key, ObjectType.Npc);
            _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, 1, npcFlags, critterFlags, 0, new[] { 1, 2, 0, 0, 0, 0, 0, 0, 0, 0 },
                experienceWorth: experienceWorth));
            return identity;
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private static SectorNavigationMap Map()
            => new(new SectorTerrain(new uint[SectorTerrain.TileCount]), new bool[SectorTerrain.TileCount],
                TileNameTable.FromMes(MesReader.Read("{300}{grs}")));

        private static int[] Stats(int strength, int intelligence, int willpower, int level)
        {
            var values = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int i = 0; i < CharacterAttributeSet.Count; i++) values[i] = 8;
            values[(int)CharacterAttribute.Strength] = strength;
            values[(int)CharacterAttribute.Intelligence] = intelligence;
            values[(int)CharacterAttribute.Willpower] = willpower;
            values[CharacterProgressionSource.LevelSourceSlot] = level;
            values[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return values;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);
        private static ArcanumObjectId ParseIdentity(string key)
        {
            if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity)) throw new InvalidOperationException();
            return identity;
        }

        private static byte[] GuidBytes(string key)
        {
            string compact = key.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++) bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private sealed class SequenceMagicRandom : IMagicRandom
        {
            private readonly Queue<int> _values;
            public SequenceMagicRandom(params int[] values) => _values = new Queue<int>(values);
            public int Next(int minimumInclusive, int maximumExclusive)
            {
                Assert.That(_values, Is.Not.Empty);
                int value = _values.Dequeue();
                Assert.That(value, Is.InRange(minimumInclusive, maximumExclusive - 1));
                return value;
            }
        }

        private sealed class FixedTiming : ICombatRealTimeTimingSource
        {
            public bool TryGetTiming(CombatRealTimeTimingRequest request,
                out CombatRealTimeTiming timing)
            {
                timing = new CombatRealTimeTiming(50, 100, 50, 1, 2);
                return true;
            }
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
