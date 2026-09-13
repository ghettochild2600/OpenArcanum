using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M4CProgressionValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string AdjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M4C/Run PlayMode Progression Validation")]
    private static void RunPlayModeValidation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(ValidateLifecycle(loader));
    }

    private static IEnumerator ValidateLifecycle(WorldObjectSectorLoader loader)
    {
        BeginTracking();
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        try
        {
            WorldMapSessionCoordinator session = loader.Session;
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(lifecycle != null && navigation != null, "production lifecycle and navigation exist");
            if (session.PlayerState == null || lifecycle.Presentation == null)
                Check(lifecycle.SpawnAndBind(), "production PC is created and bound");

            ArcanumObjectId pcIdentity = session.PlayerState.Identity;
            PersistentCharacterProgressionState pc = session.Progression.Get(pcIdentity);
            Check(pc.ObjectType == ObjectType.Pc && pc.Level == 1 && pc.Experience == 0
                  && pc.UnspentCharacterPoints == 5, "production PC has explicit source-compatible progression baseline");
            Check(CharacterSkillRules.AllSkills.All(skill =>
                    session.Progression.GetPurchasedSkillPoints(pcIdentity, skill) == 0
                    && session.Progression.GetTrainingLevel(pcIdentity, skill) == SkillTrainingLevel.None),
                "production PC begins with zero permanent skills and training");

            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(36, 58), session.PlayerState.ArtId),
                "coordinator loads the real NPC fixture sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(loader != null && lifecycle != null && navigation != null && lifecycle.Presentation != null,
                "production presentation rebinds in fixture sector");
            PersistentCharacterProgressionState npc = session.Progression.Get(FixtureIdentity);
            Check(npc.ObjectType == ObjectType.Npc && npc.PrototypeNumber == 17101 && npc.Level == 21
                  && npc.Experience == 162500 && npc.UnspentCharacterPoints == 0,
                "real NPC progression matches retail source exactly");
            Check(session.Progression.GetBaseSkillRank(FixtureIdentity, CharacterSkill.Bow) == 8
                  && session.Progression.GetBaseSkillRank(FixtureIdentity, CharacterSkill.Melee) == 8
                  && session.Progression.GetBaseSkillRank(FixtureIdentity, CharacterSkill.Throwing) == 4
                  && session.Progression.GetBaseSkillRank(FixtureIdentity, CharacterSkill.Gambling) == 8
                  && session.Progression.GetBaseSkillRank(FixtureIdentity, CharacterSkill.Firearms) == 4,
                "real NPC non-default basic and technical ranks match retail source");
            Check(CharacterSkillRules.AllSkills.All(skill =>
                    session.Progression.GetTrainingLevel(FixtureIdentity, skill) == SkillTrainingLevel.None),
                "real NPC training values match retail source");

            PersistentCharacterVitalityState vitality = session.Vitality.Get(pcIdentity);
            session.Vitality.ApplyHitPointDamage(pcIdentity, 5);
            session.Vitality.ApplyFatigueDamage(pcIdentity, 7);
            int currentHp = session.Vitality.GetCurrentHitPoints(pcIdentity);
            int currentFatigue = session.Vitality.GetCurrentFatigue(pcIdentity);
            ExperienceAwardResult award = session.Progression.AwardExperience(pcIdentity, 2100);
            Check(award.Level == 2 && award.CharacterPointsAwarded == 1
                  && session.Progression.GetUnspentCharacterPoints(pcIdentity) == 6,
                "controlled retail threshold awards exactly one level and character point");
            Check(session.Vitality.GetMaximumHitPoints(pcIdentity) == 32
                  && session.Vitality.GetMaximumFatigue(pcIdentity) == 32
                  && session.Vitality.GetCurrentHitPoints(pcIdentity) == currentHp
                  && session.Vitality.GetCurrentFatigue(pcIdentity) == currentFatigue,
                "M4B maxima use authoritative level while current damage semantics remain stable");
            Check(session.Progression.IncreaseSkill(pcIdentity, CharacterSkill.Bow) == SkillIncreaseResult.Success
                  && session.Progression.GetBaseSkillRank(pcIdentity, CharacterSkill.Bow) == 4
                  && session.Progression.GetUnspentCharacterPoints(pcIdentity) == 5,
                "controlled valid skill increase consumes exactly one character point");
            Check(session.Progression.IncreaseSkill(pcIdentity, CharacterSkill.Bow)
                  == SkillIncreaseResult.GoverningAttributeTooLow
                  && session.Progression.GetBaseSkillRank(pcIdentity, CharacterSkill.Bow) == 4
                  && session.Progression.GetUnspentCharacterPoints(pcIdentity) == 5,
                "governing-attribute rejection is atomic");

            RebuildBothModes(loader, initialMode);
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.Progression.Get(pcIdentity), pc)
                  && ReferenceEquals(session.Progression.Get(FixtureIdentity), npc)
                  && session.Progression.GetLevel(pcIdentity) == 2
                  && session.Progression.GetBaseSkillRank(pcIdentity, CharacterSkill.Bow) == 4,
                "Original/Enhanced rebuild preserves progression state");

            Check(session.ReloadSelectedSector(), "fixture sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.Progression.Get(FixtureIdentity), npc)
                  && npc.Level == 21 && session.Progression.GetBaseSkillRank(FixtureIdentity, CharacterSkill.Bow) == 8,
                "NPC unload/reload preserves the same authentic progression record");

            Check(session.TryTransitionPlayer(AdjacentSector, new Vector2(0, 18), session.PlayerState.ArtId),
                "production PC crosses A to B");
            yield return null;
            Check(ReferenceEquals(session.Progression.Get(pcIdentity), pc)
                  && session.Progression.GetLevel(pcIdentity) == 2
                  && session.Progression.GetBaseSkillRank(pcIdentity, CharacterSkill.Bow) == 4,
                "PC progression survives in sector B");
            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(63, 18), session.PlayerState.ArtId),
                "production PC crosses B to A");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.Progression.Get(pcIdentity), pc)
                  && ReferenceEquals(session.Progression.Get(FixtureIdentity), npc),
                "A to B to A retains the same PC and NPC progression records");
            CheckUnique(loader, lifecycle, navigation, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M4C PLAYMODE VALIDATION PASS: sector={FixtureSector}; oid={FixtureIdentity}; proto=17101; " +
                      $"npcLevel={npc.Level}; npcXp={npc.Experience}; npcBow=8; npcFirearms=4; " +
                      $"pc={pcIdentity}; pcLevel={pc.Level}; pcXp={pc.Experience}; pcPoints={pc.UnspentCharacterPoints}; " +
                      $"pcBow={session.Progression.GetBaseSkillRank(pcIdentity, CharacterSkill.Bow)}; " +
                      $"hpMax={vitality.MaximumHitPoints}; fatigueMax={vitality.MaximumFatigue}; " +
                      $"progressionStates={session.Progression.States.Count}; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static void RebuildBothModes(WorldObjectSectorLoader loader, GraphicsMode initial)
    {
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(initial);
        loader.RebuildVisuals();
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ProductionPlayerLifecycle lifecycle,
        PlayerNavigationController navigation, WorldMapSessionCoordinator session)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC presentation");
        Check(navigation.Player == lifecycle.Presentation, "navigation remains bound only to production PC");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
        Check(session.Progression.States.Count == session.Characters.States.Count,
            "one progression record for every authoritative character");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M4C validation FAIL: " + label);
    }

    private static void BeginTracking()
    {
        StopTracking();
        _warnings = 0;
        _errors = 0;
        Application.logMessageReceived += Track;
        _tracking = true;
    }

    private static void StopTracking()
    {
        if (!_tracking) return;
        Application.logMessageReceived -= Track;
        _tracking = false;
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
