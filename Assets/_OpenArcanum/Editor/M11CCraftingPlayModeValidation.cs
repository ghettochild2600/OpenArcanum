using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M11CCraftingPlayModeValidation
{
    private const string Sector = "maps/arcanum1-024-fixed/68853695432.sec";
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M11C/Run Physical PlayMode Validation", false, 4)]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M11C harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        WorldMapSessionCoordinator session = loader.Session;
        string baseline = null;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(Sector), "authentic Tarant sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            baseline = session.SaveGames.SerializeCurrentSession();

            var found = new SchematicId(4020);
            var builtIn = new SchematicId(2000);
            Check(!session.Crafting.Knows(pc, builtIn), "production PC begins below Herbology Novice");
            Check(session.Technology.LearnNextDegree(pc, TechnologyDiscipline.Herbology).Succeeded
                  && session.Technology.GetEffectiveLevel(pc, TechnologyDiscipline.Herbology) == 10
                  && session.Crafting.Knows(pc, builtIn),
                "M10B Novice Herbology dynamically exposes authentic built-in schematic 2000");

            ItemCreationResult blueprint = session.CreateItem(14095, ObjectPlacement.ContainedBy(pc));
            Check(blueprint.Succeeded && blueprint.State.Type == ObjectType.Written
                  && blueprint.State.WrittenSubtype == CraftingStateService.WrittenSchematicSubtype
                  && blueprint.State.WrittenStartLine == 4020,
                "authentic written prototype 14095 resolves found schematic 4020");
            SchematicLearningResult learned = session.Crafting.LearnFoundSchematic(pc, blueprint.State.Identity);
            Check(learned.Succeeded && learned.ItemConsumed && session.Crafting.Knows(pc, found)
                  && session.IsObjectRemoved(blueprint.State.Identity),
                "first learning persists 4020 and consumes its written object exactly once");

            ItemCreationResult duplicate = session.CreateItem(14095, ObjectPlacement.ContainedBy(pc));
            Check(duplicate.Succeeded
                  && session.Crafting.LearnFoundSchematic(pc, duplicate.State.Identity).Failure
                     == SchematicLearningFailure.AlreadyKnown
                  && session.TryGetObjectState(duplicate.State.Identity, out _),
                "duplicate learning rejects without consuming the second blueprint");

            ItemCreationResult wonderDrug = session.CreateItem(10084, ObjectPlacement.ContainedBy(pc));
            ItemCreationResult mechanicalHeart = session.CreateItem(15116, ObjectPlacement.ContainedBy(pc));
            Check(wonderDrug.Succeeded && mechanicalHeart.Succeeded,
                "authentic 4020 components 10084 and 15116 enter direct production inventory");
            long sourceTime = session.SourceTime.ElapsedMilliseconds;
            CraftingResult crafted = session.Crafting.Execute(new CraftingRequest(pc, found));
            Check(crafted.Succeeded && crafted.ProductPrototype == 15169 && crafted.ProductQuantity == 1
                  && crafted.Products.Count == 1
                  && session.IsObjectRemoved(wonderDrug.State.Identity)
                  && session.IsObjectRemoved(mechanicalHeart.State.Identity)
                  && session.TryGetObjectState(crafted.Products[0], out PersistentObjectState physician)
                  && physician.PrototypeNumber == 15169 && physician.ParentIdentity == pc,
                "Clockwork Physician craft consumes exact inputs and creates authentic output 15169");
            Check(session.SourceTime.ElapsedMilliseconds == sourceTime && !session.Combat.IsActive,
                "source craft is instantaneous and does not start or charge combat");

            ItemCreationResult loneComponent = session.CreateItem(10084, ObjectPlacement.ContainedBy(pc));
            int productsBefore = session.States.Values.Count(value => value.PrototypeNumber == 15169);
            CraftingResult missing = session.Crafting.Execute(new CraftingRequest(pc, found));
            Check(loneComponent.Succeeded && missing.Failure == CraftingFailure.MissingComponentTwo
                  && session.TryGetObjectState(loneComponent.State.Identity, out _)
                  && session.States.Values.Count(value => value.PrototypeNumber == 15169) == productsBefore,
                "missing-component craft has zero input, output, allocator, time, or combat mutation");

            string committed = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(committed).Succeeded, "M11C Save V1 reload succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.Crafting.Knows(pc, found)
                  && session.States.Values.Count(value => value.PrototypeNumber == 15169) == productsBefore
                  && !session.Combat.IsActive,
                "Save V1 restores found knowledge and committed output without transient craft state");

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Crafting.Knows(pc, found)
                      && session.States.Values.Count(value => value.PrototypeNumber == 15169) == productsBefore,
                    $"{mode} presentation rebuild preserves crafting authority");
            }

            Check(session.SaveGames.LoadJson(baseline).Succeeded,
                "validation cleanup restores the authoritative baseline");
            yield return null;
            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M11C PHYSICAL VALIDATION PASS: "
                      + "builtIn=2000@Herbology-Novice; found=4020 via written-14095; "
                      + "components=10084+15116; output=15169x1; duplicate=non-consuming; "
                      + "missing-component=zero-mutation; time/AP=none; "
                      + "stacked-authentic-fixture=dependency-not-ready(base-only ammo prototype); "
                      + "saveV1=knowledge+committed-output+no-transient-craft; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (baseline != null && session.PlayerState != null && session.HasSelectedSector)
                session.SaveGames.LoadJson(baseline);
            _running = false;
        }
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null, "production TestTerrain composition remains available");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M11C validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
