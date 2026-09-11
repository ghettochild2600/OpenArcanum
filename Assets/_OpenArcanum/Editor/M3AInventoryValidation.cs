using System;
using System.Collections;
using System.Linq;
using Arcanum.Runtime.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M3AInventoryValidation
{
    private const string SectorA = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string SectorB = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string ContainerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
    private const string ChildKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";

    [MenuItem("OpenArcanum/M3A/Run Real Inventory Lifecycle Validation")]
    private static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("The production world-object loader is missing.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        int warnings = 0;
        int errors = 0;
        void Count(string _, string __, LogType type)
        {
            if (type == LogType.Warning) warnings++;
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        Application.logMessageReceived += Count;
        try
        {
            Check(session.SelectSector(SectorA), "authoritative sector A selection");
            yield return null;
            PersistentObjectState container = session.States.Values.Single(state => state.Identity.Key == ContainerKey);
            PersistentObjectState child = session.States.Values.Single(state => state.Identity.Key == ChildKey);
            Check(container.PrototypeNumber == 3052 && child.PrototypeNumber == 8127, "real fixture prototypes");
            Check(child.AuthoredParentIdentity == container.Identity, "exact authored parent identity");
            Check(ProjectionCount(loader, child) == 0, "authored child initially has no world projection");

            Check(session.TransferItem(child.Identity, child.Placement,
                ObjectPlacement.InWorld(SectorA, new Vector2(12, 18))).Succeeded, "container to world transfer");
            yield return null;
            Check(ProjectionCount(loader, child) == 1, "world transfer creates exactly one ordinary projection");
            loader.RebuildVisuals();
            yield return null;
            Check(ProjectionCount(loader, child) == 1, "visual rebuild preserves one projection and identity");

            PersistentPlayerState player = session.PlayerState;
            Check(player != null, "production PC state exists");
            Check(session.TransferItem(child.Identity, child.Placement,
                    ObjectPlacement.ContainedBy(player.Identity)).Succeeded,
                "world to production PC transfer");
            yield return null;
            Check(ProjectionCount(loader, child) == 0 && session.ChildrenOf(player.Identity).Contains(child.Identity),
                "PC containment removes world projection");

            ItemCreationResult created = session.CreateItem(8127, ObjectPlacement.ContainedBy(player.Identity));
            Check(created.Succeeded && created.State.Identity.Key == "D_0000000000000001",
                "deterministic dynamic item identity");
            Check(ProjectionCount(loader, created.State) == 0, "created contained item is not projected");
            var createdIdentity = created.State.Identity;
            loader.RebuildVisuals();
            yield return null;
            Check(created.State.Identity == createdIdentity && created.State.ParentIdentity == player.Identity
                                                           && ProjectionCount(loader, created.State) == 0,
                "visual rebuild preserves dynamic item identity and containment");
            ItemCreationResult afterRebuild = session.CreateItem(8127, ObjectPlacement.ContainedBy(player.Identity));
            Check(afterRebuild.Succeeded && afterRebuild.State.Identity.Key == "D_0000000000000002",
                "visual rebuild preserves dynamic identity allocation state");

            Check(session.ReloadSelectedSector(), "sector reload");
            yield return null;
            Check(child.ParentIdentity == player.Identity && ProjectionCount(loader, child) == 0,
                "reload preserves PC containment");
            Check(session.TryTransitionPlayer(SectorB, new Vector2(0, 58), player.ArtId), "PC crosses to sector B");
            yield return null;
            Check(child.ParentIdentity == player.Identity && created.State.ParentIdentity == player.Identity
                                                       && afterRebuild.State.ParentIdentity == player.Identity,
                "PC inventory survives crossing");
            Check(session.TryTransitionPlayer(SectorA, new Vector2(63, 58), player.ArtId), "PC returns to sector A");
            yield return null;
            Check(ProjectionCount(loader, child) == 0 && ProjectionCount(loader, created.State) == 0
                                                      && ProjectionCount(loader, afterRebuild.State) == 0,
                "return does not duplicate contained items");

            Check(session.TransferItem(child.Identity, child.Placement,
                ObjectPlacement.InWorld(SectorA, new Vector2(13, 18))).Succeeded, "PC to world transfer");
            Check(session.TransferItem(created.State.Identity, created.State.Placement,
                ObjectPlacement.InWorld(SectorA, new Vector2(14, 18))).Succeeded, "dynamic PC item to world transfer");
            yield return null;
            Check(ProjectionCount(loader, child) == 1 && ProjectionCount(loader, created.State) == 1,
                "authored and dynamic items each have one projection");
            Check(session.TryGetLoadedObject(child.Identity, out WorldObject authoredRuntime)
                  && authoredRuntime.TilePosition == new Vector2(13, 18), "authored drop position");
            Check(session.TryGetLoadedObject(created.State.Identity, out WorldObject dynamicRuntime)
                  && dynamicRuntime.TilePosition == new Vector2(14, 18), "dynamic drop position");

            Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsSortMode.None).Length == 1,
                "one session coordinator");
            Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsSortMode.None).Length == 1,
                "one object loader");
            Check(Object.FindObjectsByType<ProductionPlayerLifecycle>(FindObjectsSortMode.None).Length == 1,
                "one production PC lifecycle");
            Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsSortMode.None).Length == 1,
                "one navigation controller");
            Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                          .Count(root => root.name == "WorldObjects") == 1,
                "one sector object root");
            Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                          .Count(runtime => runtime.Identity == player.Identity) == 1,
                "one production PC runtime");
            Check(Object.FindObjectsByType<WorldObjectSpriteOwner>(FindObjectsInactive.Include,
                              FindObjectsSortMode.None)
                          .Count(owner => owner.WorldObject != null && owner.WorldObject.Identity == player.Identity) == 1,
                "one production PC sprite owner");
            Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
                "no duplicate or orphan sprite owners");
            Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
                          .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
                "one world presentation per persistent identity");
            Check(errors == 0 && warnings == 0, $"no new Unity warnings/errors (warnings={warnings}, errors={errors})");
            Debug.Log($"M3A inventory validation PASS: realContainer={container.Identity}; realChild={child.Identity}; " +
                      $"dynamic={created.State.Identity}; afterRebuild={afterRebuild.State.Identity}; " +
                      $"authoredProto={child.PrototypeNumber}; " +
                      $"containerProto={container.PrototypeNumber}; warnings={warnings}; errors={errors}.");
        }
        finally { Application.logMessageReceived -= Count; }
    }

    private static int ProjectionCount(WorldObjectSectorLoader loader, PersistentObjectState state)
        => loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                             && owner.WorldObject.Identity == state.Identity);

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M3A inventory validation FAIL: " + label);
    }
}
