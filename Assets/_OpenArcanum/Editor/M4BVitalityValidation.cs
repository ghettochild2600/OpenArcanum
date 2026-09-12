using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M4BVitalityValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string AdjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M4B/Run Source Vitality Audit")]
    private static void RunSourceAudit()
    {
        using DatVirtualFileSystem vfs = MountSourceData();
        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no data/proto directory.");
        var prototypes = new ProtoLibrary(protoDirectory);
        ObjectInstance instance = ReadFixture(vfs);
        ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber)
            ?? throw new InvalidOperationException($"Missing fixture prototype {instance.PrototypeNumber}.");
        CharacterVitalitySource source = CharacterVitalitySource.Resolve(
            instance.StatBase, prototype.StatBase,
            instance.HpPoints, prototype.HpPoints,
            instance.HpAdjustment, prototype.HpAdjustment,
            instance.HpDamage, prototype.HpDamage,
            instance.FatiguePoints, prototype.FatiguePoints,
            instance.FatigueAdjustment, prototype.FatigueAdjustment,
            instance.FatigueDamage, prototype.FatigueDamage);
        var characters = new CharacterStatService();
        characters.GetOrCreateSourceCharacter(FixtureIdentity, ObjectType.Npc, instance.PrototypeNumber,
            instance.StatBase, prototype.StatBase);
        var vitality = new CharacterVitalityService(characters);
        PersistentCharacterVitalityState state = vitality.GetOrCreateSourceCharacter(FixtureIdentity,
            ObjectType.Npc, instance.PrototypeNumber, source);

        Debug.Log($"M4B SOURCE FIXTURE: sector={FixtureSector}; oid={FixtureIdentity}; " +
                  $"proto={instance.PrototypeNumber}; level={source.Level}; " +
                  $"instance=[hpPts={Format(instance.HpPoints)},hpAdj={Format(instance.HpAdjustment)}," +
                  $"hpDamage={Format(instance.HpDamage)},fatiguePts={Format(instance.FatiguePoints)}," +
                  $"fatigueAdj={Format(instance.FatigueAdjustment)},fatigueDamage={Format(instance.FatigueDamage)}]; " +
                  $"prototype=[hpPts={Format(prototype.HpPoints)},hpAdj={Format(prototype.HpAdjustment)}," +
                  $"hpDamage={Format(prototype.HpDamage)},fatiguePts={Format(prototype.FatiguePoints)}," +
                  $"fatigueAdj={Format(prototype.FatigueAdjustment)},fatigueDamage={Format(prototype.FatigueDamage)}]; " +
                  $"resolved=[hpPts={source.HitPointPoints},hpAdj={source.HitPointAdjustment}," +
                  $"hpDamage={source.HitPointDamage},fatiguePts={source.FatiguePoints}," +
                  $"fatigueAdj={source.FatigueAdjustment},fatigueDamage={source.FatigueDamage}]; " +
                  $"hp={state.CurrentHitPoints}/{state.MaximumHitPoints}; " +
                  $"fatigue={state.CurrentFatigue}/{state.MaximumFatigue}");
    }

    [MenuItem("OpenArcanum/M4B/Run PlayMode Vitality Validation")]
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
            PersistentCharacterVitalityState pc = session.Vitality.Get(pcIdentity);
            Check(pc.ObjectType == ObjectType.Pc && pc.Source.Equals(CharacterVitalitySource.DevelopmentPlayer),
                "production PC owns explicit source-compatible vitality inputs");
            Check(pc.MaximumHitPoints == 30 && pc.CurrentHitPoints == 30
                  && pc.MaximumFatigue == 30 && pc.CurrentFatigue == 30,
                "production PC starts at HP 30/30 and Fatigue 30/30");
            session.Vitality.ApplyHitPointDamage(pcIdentity, 6);
            session.Vitality.ApplyFatigueDamage(pcIdentity, 8);
            Check(pc.CurrentHitPoints == 24 && pc.CurrentFatigue == 22,
                "controlled PC HP and fatigue damage mutate only authoritative state");
            session.Vitality.RestoreHitPoints(pcIdentity, 2);
            session.Vitality.RestoreFatigue(pcIdentity, 3);
            Check(pc.CurrentHitPoints == 26 && pc.CurrentFatigue == 25,
                "controlled restores reduce accumulated damage with zero-floor semantics");

            session.Characters.SetGender(pcIdentity, CharacterGender.Female);
            Check(pc.MaximumHitPoints == 28 && pc.CurrentHitPoints == 26
                  && pc.MaximumFatigue == 32 && pc.CurrentFatigue == 25,
                "gender maximum changes preserve current HP/fatigue where possible");
            session.Characters.SetGender(pcIdentity, CharacterGender.Male);
            Check(pc.MaximumHitPoints == 30 && pc.CurrentHitPoints == 26
                  && pc.MaximumFatigue == 30 && pc.CurrentFatigue == 25,
                "removing the modifier preserves current HP/fatigue");

            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(36, 58), session.PlayerState.ArtId),
                "coordinator loads the real vitality fixture sector");
            yield return null;
            RefreshSceneReferences(out loader, out lifecycle, out navigation);
            Check(loader != null && lifecycle != null && navigation != null && lifecycle.Presentation != null,
                "production presentation rebinds in fixture sector");
            PersistentCharacterVitalityState npc = session.Vitality.Get(FixtureIdentity);
            Check(npc.ObjectType == ObjectType.Npc && npc.PrototypeNumber == 17101 && npc.Source.Level == 21,
                "real NPC vitality is keyed by exact ObjectID and prototype");
            Check(npc.Source.HitPointPoints == 0 && npc.Source.HitPointAdjustment == 4
                  && npc.Source.HitPointDamage == 0 && npc.Source.FatiguePoints == 0
                  && npc.Source.FatigueAdjustment == 0 && npc.Source.FatigueDamage == 4,
                "real NPC instance/prototype vitality inheritance matches retail source");
            Check(npc.MaximumHitPoints == 76 && npc.CurrentHitPoints == 76
                  && npc.MaximumFatigue == 86 && npc.CurrentFatigue == 82,
                "real NPC exact retail maximum/current HP and fatigue values");
            session.Vitality.ApplyHitPointDamage(FixtureIdentity, 5);
            session.Vitality.ApplyFatigueDamage(FixtureIdentity, 7);
            Check(npc.CurrentHitPoints == 71 && npc.CurrentFatigue == 75,
                "controlled real-NPC damage mutates persistent session state");
            Check(ProjectionCount(loader, FixtureIdentity) == 1, "real NPC has one presentation");

            RebuildBothModes(loader, initialMode);
            yield return null;
            RefreshSceneReferences(out loader, out lifecycle, out navigation);
            Check(ReferenceEquals(session.Vitality.Get(pcIdentity), pc)
                  && ReferenceEquals(session.Vitality.Get(FixtureIdentity), npc),
                "Original/Enhanced visual rebuild preserves both vitality records");
            Check(pc.CurrentHitPoints == 26 && pc.CurrentFatigue == 25
                  && npc.CurrentHitPoints == 71 && npc.CurrentFatigue == 75,
                "graphics rebuild preserves exact mutated current values");
            Check(ProjectionCount(loader, FixtureIdentity) == 1, "graphics rebuild leaves one NPC presentation");

            Check(session.ReloadSelectedSector(), "fixture sector unload/reload succeeds");
            yield return null;
            RefreshSceneReferences(out loader, out lifecycle, out navigation);
            Check(ReferenceEquals(session.Vitality.Get(FixtureIdentity), npc)
                  && npc.CurrentHitPoints == 71 && npc.CurrentFatigue == 75,
                "NPC unload/reload preserves identity and damage state");

            Check(session.TryTransitionPlayer(AdjacentSector, new Vector2(0, 18), session.PlayerState.ArtId),
                "production PC crosses A to B");
            yield return null;
            Check(ReferenceEquals(session.Vitality.Get(pcIdentity), pc)
                  && session.Vitality.TryGet(FixtureIdentity, out PersistentCharacterVitalityState unloadedNpc)
                  && ReferenceEquals(unloadedNpc, npc),
                "PC and unloaded NPC vitality survive in sector B");
            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(63, 18), session.PlayerState.ArtId),
                "production PC crosses B to A");
            yield return null;
            RefreshSceneReferences(out loader, out lifecycle, out navigation);
            Check(ReferenceEquals(session.Vitality.Get(pcIdentity), pc)
                  && ReferenceEquals(session.Vitality.Get(FixtureIdentity), npc)
                  && pc.CurrentHitPoints == 26 && pc.CurrentFatigue == 25
                  && npc.CurrentHitPoints == 71 && npc.CurrentFatigue == 75,
                "A to B to A retains exact authoritative vitality state");
            CheckUnique(loader, lifecycle, navigation, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M4B PLAYMODE VALIDATION PASS: sector={FixtureSector}; oid={FixtureIdentity}; proto=17101; " +
                      $"retailHp=76/76; retailFatigue=82/86; mutatedNpcHp={npc.CurrentHitPoints}/{npc.MaximumHitPoints}; " +
                      $"mutatedNpcFatigue={npc.CurrentFatigue}/{npc.MaximumFatigue}; " +
                      $"pcHp={pc.CurrentHitPoints}/{pc.MaximumHitPoints}; pcFatigue={pc.CurrentFatigue}/{pc.MaximumFatigue}; " +
                      $"vitalityStates={session.Vitality.States.Count}; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static void RefreshSceneReferences(out WorldObjectSectorLoader loader,
        out ProductionPlayerLifecycle lifecycle, out PlayerNavigationController navigation)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
    }

    private static int ProjectionCount(WorldObjectSectorLoader loader, ArcanumObjectId identity)
        => loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                             && owner.WorldObject.Identity == identity);

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
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(root => root.name == "WorldObjects") == 1, "one world-object presentation root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC presentation");
        Check(ProjectionCount(loader, session.PlayerState.Identity) == 1, "one production PC sprite owner");
        Check(ProjectionCount(loader, FixtureIdentity) == 1, "one real NPC presentation");
        Check(navigation.Player == lifecycle.Presentation, "navigation remains bound only to production PC");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
        Check(session.Vitality.States.Keys.Distinct().Count() == session.Vitality.States.Count,
            "one authoritative vitality record per identity");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M4B validation FAIL: " + label);
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

    private static ObjectInstance ReadFixture(DatVirtualFileSystem vfs)
    {
        foreach (ObjectInstance instance in SectorReader.ReadObjects(vfs.ReadAllBytes(FixtureSector)))
            if (instance.Identity == FixtureIdentity) return instance;

        long sectorId = long.Parse(Path.GetFileNameWithoutExtension(FixtureSector));
        string mapPrefix = FixtureSector.Substring(0, FixtureSector.LastIndexOf('/') + 1);
        foreach (string mobilePath in vfs.EnumerateFiles(mapPrefix)
                     .Where(path => path.EndsWith(".mob", StringComparison.OrdinalIgnoreCase)))
        {
            byte[] bytes = vfs.ReadAllBytes(mobilePath);
            int offset = 0;
            ObjectInstance instance;
            try { instance = ObjectInstanceReader.Read(bytes, ref offset); }
            catch { continue; }
            if (instance.Identity == FixtureIdentity && BelongsToSector(instance, sectorId)) return instance;
        }
        throw new InvalidOperationException($"Source fixture {FixtureIdentity} was not found in {FixtureSector}.");
    }

    private static DatVirtualFileSystem MountSourceData()
    {
        var vfs = new DatVirtualFileSystem();
        string module = GameDataLocator.Find("modules/Arcanum.dat");
        if (string.IsNullOrEmpty(module))
            throw new FileNotFoundException("The configured source data has no modules/Arcanum.dat.");
        vfs.MountFile(module);
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }
        return vfs;
    }

    private static bool BelongsToSector(ObjectInstance instance, long sectorId)
    {
        if (!instance.Location.HasValue) return false;
        long sectorX = (uint)instance.MapX >> 6;
        long sectorY = (uint)instance.MapY >> 6;
        return (sectorX | (sectorY << 26)) == sectorId;
    }

    private static string Format(int? value) => value?.ToString() ?? "<inherited>";
}
