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

internal static class M4DDerivedCharacterValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string AdjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string EquipmentKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";
    private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M4D/Run PlayMode Derived-Stat Validation")]
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
            PersistentCharacterDerivedState pc = session.DerivedStats.Get(pcIdentity);
            Check(pc.ObjectType == ObjectType.Pc && pc.PrototypeNumber == null,
                "production PC owns explicit development derived source");
            Check(session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.CarryWeight) == 4000
                  && session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.MeleeDamageBonus) == -1
                  && session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.ArmorClassAdjustment) == -2
                  && session.DerivedStats.GetArmorClass(pcIdentity) == 0
                  && session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.Speed) == 8
                  && session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.HealRate) == 3
                  && session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.PoisonRecovery) == 8
                  && session.DerivedStats.GetDerivedStat(pcIdentity,
                      CharacterDerivedStat.BeautyReactionModifier) == -7
                  && session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.MaximumFollowers) == 2
                  && session.DerivedStats.GetDerivedStat(pcIdentity,
                      CharacterDerivedStat.MagickTechAptitude) == 0
                  && session.DerivedStats.GetAlignment(pcIdentity) == 0
                  && session.DerivedStats.GetResistance(pcIdentity, CharacterResistance.Poison) == 20,
                "production PC exact M4D baseline");

            int[] unmodifiedPc = Snapshot(session, pcIdentity);
            session.Characters.SetRace(pcIdentity, CharacterRace.HalfOrc);
            Check(session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.MeleeDamageBonus) == 0
                  && session.DerivedStats.GetResistance(pcIdentity, CharacterResistance.Poison) == 35,
                "controlled race mutation immediately changes only dependent queries");
            session.Characters.SetGender(pcIdentity, CharacterGender.Female);
            Check(session.DerivedStats.GetDerivedStat(pcIdentity, CharacterDerivedStat.MeleeDamageBonus) == -1
                  && session.DerivedStats.GetResistance(pcIdentity, CharacterResistance.Poison) == 40,
                "controlled gender mutation immediately changes dependent queries");
            session.Characters.SetGender(pcIdentity, CharacterGender.Male);
            session.Characters.SetRace(pcIdentity, CharacterRace.Human);
            Check(Snapshot(session, pcIdentity).SequenceEqual(unmodifiedPc),
                "restoring race and gender restores every M4D value");

            int[] beforeLevel = Snapshot(session, pcIdentity);
            ExperienceAwardResult award = session.Progression.AwardExperience(pcIdentity, 2100);
            Check(award.Level == 2 && award.CharacterPointsAwarded == 1,
                "controlled progression establishes the requested production Level 2");
            Check(Snapshot(session, pcIdentity).SequenceEqual(beforeLevel),
                "Level mutation leaves the admitted non-Level-dependent M4D subset unchanged");

            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(36, 58), session.PlayerState.ArtId),
                "coordinator loads the real NPC fixture sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(loader != null && lifecycle != null && navigation != null && lifecycle.Presentation != null,
                "production PC presentation rebinds in fixture sector");

            PersistentCharacterDerivedState npc = session.DerivedStats.Get(FixtureIdentity);
            Check(npc.ObjectType == ObjectType.Npc && npc.PrototypeNumber == 17101,
                "stable retail NPC ObjectID owns one derived record");
            Check(session.DerivedStats.GetDerivedStat(FixtureIdentity,
                      CharacterDerivedStat.MeleeDamageBonus) == 0
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity,
                      CharacterDerivedStat.ArmorClassAdjustment) == -1
                  && session.DerivedStats.GetArmorClass(FixtureIdentity) == 0
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity, CharacterDerivedStat.Speed) == 9
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity, CharacterDerivedStat.HealRate) == 5
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity,
                      CharacterDerivedStat.PoisonRecovery) == 16
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity,
                      CharacterDerivedStat.BeautyReactionModifier) == 0
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity,
                      CharacterDerivedStat.MaximumFollowers) == 2
                  && session.DerivedStats.GetDerivedStat(FixtureIdentity,
                      CharacterDerivedStat.MagickTechAptitude) == -5
                  && session.DerivedStats.GetAlignment(FixtureIdentity) == 100
                  && session.DerivedStats.GetResistance(FixtureIdentity, CharacterResistance.Normal) == 0
                  && session.DerivedStats.GetResistance(FixtureIdentity, CharacterResistance.Fire) == 0
                  && session.DerivedStats.GetResistance(FixtureIdentity, CharacterResistance.Electrical) == 0
                  && session.DerivedStats.GetResistance(FixtureIdentity, CharacterResistance.Poison) == 60
                  && session.DerivedStats.GetResistance(FixtureIdentity, CharacterResistance.Magic) == 0,
                "real NPC values match audited retail source and formulas");
            CharacterReactionInputs reaction = session.DerivedStats.GetReactionInputs(FixtureIdentity, pcIdentity);
            Check(reaction.CharacterModifiersApply && reaction.SourceBase == 50 && reaction.BeautyModifier == -7
                  && reaction.RaceModifier == 0 && reaction.Subtotal == 43,
                "bounded pairwise reaction inputs match retail Human-to-Human source");

            int[] beforeInventory = Snapshot(session, pcIdentity);
            PersistentObjectState item = session.States.Values.Single(state => state.Identity.Key == EquipmentKey);
            ObjectPlacement authoredPlacement = item.Placement;
            Check(WorldMapSessionCoordinator.TryGetNaturalWornLocation(item, out WornLocation worn),
                "authentic fixture armor resolves a typed worn location");
            Check(session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(pcIdentity)).Succeeded,
                "authentic item transfers into PC inventory");
            Check(Snapshot(session, pcIdentity).SequenceEqual(beforeInventory),
                "inventory mutation leaves derived statistics unchanged");
            Check(session.EquipItem(pcIdentity, item.Identity, worn).Succeeded,
                "authentic item enters authoritative equipment state");
            Check(Snapshot(session, pcIdentity).SequenceEqual(beforeInventory),
                "future equipment modifier stage is explicitly neutral");
            Check(session.UnequipItem(pcIdentity, worn).Succeeded
                  && session.TransferItem(item.Identity, item.Placement, authoredPlacement).Succeeded,
                "fixture equipment is restored to its authored container");

            RebuildBothModes(loader, initialMode);
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.DerivedStats.Get(pcIdentity), pc)
                  && ReferenceEquals(session.DerivedStats.Get(FixtureIdentity), npc)
                  && Snapshot(session, pcIdentity).SequenceEqual(beforeInventory),
                "Original to Enhanced to Original rebuild preserves derived state");

            Check(session.ReloadSelectedSector(), "fixture sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.DerivedStats.Get(FixtureIdentity), npc)
                  && session.DerivedStats.GetAlignment(FixtureIdentity) == 100
                  && session.DerivedStats.GetResistance(FixtureIdentity, CharacterResistance.Poison) == 60,
                "NPC unload/reload preserves the authentic derived record");

            Check(session.TryTransitionPlayer(AdjacentSector, new Vector2(0, 18), session.PlayerState.ArtId),
                "production PC crosses A to B");
            yield return null;
            Check(ReferenceEquals(session.DerivedStats.Get(pcIdentity), pc)
                  && Snapshot(session, pcIdentity).SequenceEqual(beforeInventory),
                "PC derived state survives in sector B");
            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(63, 18), session.PlayerState.ArtId),
                "production PC crosses B to A");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.DerivedStats.Get(pcIdentity), pc)
                  && ReferenceEquals(session.DerivedStats.Get(FixtureIdentity), npc),
                "A to B to A retains the same PC and NPC derived records");
            CheckUnique(loader, lifecycle, navigation, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M4D PLAYMODE VALIDATION PASS: sector={FixtureSector}; oid={FixtureIdentity}; proto=17101; " +
                      $"npc=[damage=0,acAdj=-1,ac=0,speed=9,heal=5,poisonRecovery=16,reactionMod=0," +
                      $"followers=2,aptitude=-5,alignment=100,resist=0/0/0/60/0]; reaction=43; " +
                      $"pc={pcIdentity}; level={session.Progression.GetLevel(pcIdentity)}; " +
                      $"pc=[carry=4000,damage=-1,acAdj=-2,ac=0,speed=8,heal=3,poisonRecovery=8," +
                      $"reactionMod=-7,followers=2,aptitude=0,alignment=0,resist=0/0/0/20/0]; " +
                      $"derivedStates={session.DerivedStats.States.Count}; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static int[] Snapshot(WorldMapSessionCoordinator session, ArcanumObjectId identity)
        => CharacterDerivedStatRules.AllStats.Select(stat => session.DerivedStats.GetDerivedStat(identity, stat))
            .Concat(new[]
            {
                session.DerivedStats.GetArmorClass(identity),
                session.DerivedStats.GetAlignment(identity),
            })
            .Concat(CharacterDerivedStatRules.AllResistances.Select(resistance =>
                session.DerivedStats.GetResistance(identity, resistance))).ToArray();

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
        Check(session.DerivedStats.States.Count == session.Characters.States.Count
              && session.DerivedStats.States.Count == session.Progression.States.Count
              && session.DerivedStats.States.Count == session.Vitality.States.Count,
            "one attributes/progression/vitality/derived record per authoritative character");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M4D validation FAIL: " + label);
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
