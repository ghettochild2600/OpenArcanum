using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M4ASourceAudit
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string AdjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    private static readonly string[] FixtureSectors =
    {
        "maps/arcanum1-024-fixed/101602821844.sec",
        "maps/arcanum1-024-fixed/101602821845.sec",
    };

    [MenuItem("OpenArcanum/M4A/Run Source Audit")]
    private static void Run()
    {
        using var vfs = MountSourceData();
        MesFile effects = MesReader.Read(vfs.ReadAllBytes("rules/effect.mes"));
        foreach (int id in Enumerable.Range(64, 11).Append(330))
            Debug.Log($"M4A SOURCE EFFECT {id}: {effects.Get(id) ?? "<missing>"}");

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        if (string.IsNullOrEmpty(protoDirectory))
            throw new DirectoryNotFoundException("The configured source data has no data/proto directory.");
        var prototypes = new ProtoLibrary(protoDirectory);

        foreach (string sector in FixtureSectors)
        {
            long sectorId = long.Parse(Path.GetFileNameWithoutExtension(sector));
            var instances = new List<(ObjectInstance Instance, string Source)>();
            foreach (ObjectInstance instance in SectorReader.ReadObjects(vfs.ReadAllBytes(sector)))
                instances.Add((instance, sector));

            string mapPrefix = sector.Substring(0, sector.LastIndexOf('/') + 1);
            foreach (string mobilePath in vfs.EnumerateFiles(mapPrefix)
                         .Where(path => path.EndsWith(".mob", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                byte[] bytes = vfs.ReadAllBytes(mobilePath);
                int offset = 0;
                ObjectInstance instance;
                try { instance = ObjectInstanceReader.Read(bytes, ref offset); }
                catch { continue; }
                if (BelongsToSector(instance, sectorId)) instances.Add((instance, mobilePath));
            }

            int npcCount = 0;
            foreach ((ObjectInstance instance, string source) in instances)
            {
                if (instance.Type != ObjectType.Npc || !instance.Identity.IsPersistent) continue;
                ObjectProtoInfo prototype = prototypes.Get(instance.PrototypeNumber);
                int[] effectiveBase = instance.StatBase ?? prototype?.StatBase;
                if (effectiveBase == null || effectiveBase.Length < 28) continue;
                npcCount++;
                Debug.Log(
                    $"M4A SOURCE NPC: sector={sector}; source={source}; oid={instance.Identity}; " +
                    $"proto={instance.PrototypeNumber}; race={effectiveBase[27]}; gender={effectiveBase[26]}; " +
                    $"base=[{string.Join(",", effectiveBase.Take(8))}]; " +
                    $"instanceOverride={(instance.StatBase != null ? "yes" : "no")}; " +
                    $"prototypeBase=[{Format(prototype?.StatBase)}]; instanceBase=[{Format(instance.StatBase)}]");
            }
            Debug.Log($"M4A SOURCE SECTOR: sector={sector}; npcCount={npcCount}");
        }
    }

    [MenuItem("OpenArcanum/M4A/Run PlayMode Lifecycle Validation")]
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

            PersistentCharacterState pc = session.Characters.Get(session.PlayerState.Identity);
            Check(pc.ObjectType == ObjectType.Pc && pc.Race == CharacterRace.Human
                  && pc.Gender == CharacterGender.Male, "production PC has explicit Human Male identity inputs");
            Check(Values(session, pc.Identity, false).SequenceEqual(Enumerable.Repeat(8, 8)),
                "production PC base is the explicit source-default baseline");
            Check(Values(session, pc.Identity, true).SequenceEqual(Enumerable.Repeat(8, 8)),
                "production PC effective baseline is source-faithful");

            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(36, 58), session.PlayerState.ArtId),
                "coordinator loads the real NPC fixture sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(loader != null && lifecycle != null && navigation != null && lifecycle.Presentation != null,
                "production presentation rebinds in fixture sector");
            Check(session.TryGetObjectState(FixtureIdentity, out PersistentObjectState npcObject)
                  && npcObject.Type == ObjectType.Npc && npcObject.PrototypeNumber == 17101,
                "exact source NPC ObjectID and prototype are session-owned");
            PersistentCharacterState npc = session.Characters.Get(FixtureIdentity);
            Check(npc.ObjectType == ObjectType.Npc && npc.PrototypeNumber == 17101
                  && npc.HasInstanceStatOverride && npc.Race == CharacterRace.Human
                  && npc.Gender == CharacterGender.Female, "NPC source initialization hierarchy and identity inputs");
            Check(Values(session, FixtureIdentity, false).SequenceEqual(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }),
                "NPC all eight base attributes match retail source");
            Check(Values(session, FixtureIdentity, true).SequenceEqual(new[] { 9, 9, 16, 10, 10, 10, 8, 10 }),
                "NPC effective values include retail female effect 330");
            Check(ProjectionCount(loader, FixtureIdentity) == 1, "real NPC has one presentation");

            session.Characters.SetGender(pc.Identity, CharacterGender.Female);
            Check(session.Characters.GetBaseAttribute(pc.Identity, CharacterAttribute.Strength) == 8
                  && session.Characters.GetEffectiveAttribute(pc.Identity, CharacterAttribute.Strength) == 7
                  && session.Characters.GetEffectiveAttribute(pc.Identity, CharacterAttribute.Constitution) == 9,
                "supported female modifier changes effective values but not base");
            session.Characters.SetGender(pc.Identity, CharacterGender.Male);
            Check(session.Characters.GetEffectiveAttribute(pc.Identity, CharacterAttribute.Strength) == 8
                  && session.Characters.GetEffectiveAttribute(pc.Identity, CharacterAttribute.Constitution) == 8,
                "gender-effect removal restores effective values");

            RebuildBothModes(loader, initialMode);
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.Characters.Get(FixtureIdentity), npc)
                  && ReferenceEquals(session.Characters.Get(pc.Identity), pc),
                "Original/Enhanced visual rebuild preserves both character states");
            Check(ProjectionCount(loader, FixtureIdentity) == 1, "graphics rebuild leaves one NPC presentation");

            Check(session.ReloadSelectedSector(), "fixture sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.Characters.Get(FixtureIdentity), npc)
                  && Values(session, FixtureIdentity, true).SequenceEqual(new[] { 9, 9, 16, 10, 10, 10, 8, 10 }),
                "NPC unload/reload preserves authoritative identity and values");

            Check(session.TryTransitionPlayer(AdjacentSector, new Vector2(0, 18), session.PlayerState.ArtId),
                "production PC crosses A to B");
            yield return null;
            Check(ReferenceEquals(session.Characters.Get(pc.Identity), pc)
                  && session.Characters.TryGet(FixtureIdentity, out PersistentCharacterState unloadedNpc)
                  && ReferenceEquals(unloadedNpc, npc), "PC and unloaded NPC character state survive in sector B");
            Check(session.TryTransitionPlayer(FixtureSector, new Vector2(63, 18), session.PlayerState.ArtId),
                "production PC crosses B to A");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(ReferenceEquals(session.Characters.Get(pc.Identity), pc)
                  && ReferenceEquals(session.Characters.Get(FixtureIdentity), npc),
                "A to B to A retains the same PC and NPC character records");
            Check(Values(session, pc.Identity, true).SequenceEqual(Enumerable.Repeat(8, 8))
                  && Values(session, FixtureIdentity, true).SequenceEqual(new[] { 9, 9, 16, 10, 10, 10, 8, 10 }),
                "A to B to A retains exact effective attributes");
            CheckUnique(loader, lifecycle, navigation, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M4A PLAYMODE VALIDATION PASS: sector={FixtureSector}; oid={FixtureIdentity}; proto=17101; " +
                      $"base=[{string.Join(",", Values(session, FixtureIdentity, false))}]; " +
                      $"effective=[{string.Join(",", Values(session, FixtureIdentity, true))}]; " +
                      $"pc={pc.Identity}; pcStrength={session.Characters.GetEffectiveAttribute(pc.Identity, CharacterAttribute.Strength)}; " +
                      $"characterStates={session.Characters.States.Count}; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static int[] Values(WorldMapSessionCoordinator session, ArcanumObjectId identity, bool effective)
        => CharacterStatService.AllAttributes.Select(attribute => effective
            ? session.Characters.GetEffectiveAttribute(identity, attribute)
            : session.Characters.GetBaseAttribute(identity, attribute)).ToArray();

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
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M4A validation FAIL: " + label);
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

    private static string Format(int[] values)
        => values == null ? "<inherited>" : string.Join(",", values.Take(28));
}
