using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class WorldSessionValidation
{
    private const string RealDoorSector = "maps/arcanum1-024-fixed/122473678402.sec";

    [MenuItem("OpenArcanum/World Session/Open Real Sector Scene")]
    private static void OpenScene() => EditorSceneManager.OpenScene("Assets/_Game/Scenes/TestTerrain.unity");

    [MenuItem("OpenArcanum/World Session/Selected Portal Open")]
    private static void OpenPortal() => SelectedPortal().RequestPortalOpen(true);
    [MenuItem("OpenArcanum/World Session/Selected Portal Close")]
    private static void ClosePortal() => SelectedPortal().RequestPortalOpen(false);
    private static WorldObject SelectedPortal()
        => Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<WorldObject>() : null;

    [MenuItem("OpenArcanum/World Session/Run Real Portal Validation")]
    private static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || !loader.IsLoaded) throw new InvalidOperationException("Load the real sector first.");
        loader.StartCoroutine(Validate(loader));
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("World session validation FAIL: " + label);
    }

    private static WorldObjectSpriteOwner Find(WorldObjectSectorLoader loader, ArcanumObjectId id)
        => loader.SpriteOwners.Single(o => o.WorldObject.Identity == id);

    private static int Textures() => Resources.FindObjectsOfTypeAll<Texture2D>().Count(t => t.name == "ArtFrame");

    private static string[] IssueSignatures(WorldObjectSectorLoader loader)
        => loader.RenderIssues.Select(issue => issue.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    [MenuItem("OpenArcanum/World Session/Find Real Door Sector")]
    private static void LogRealDoorSector()
    {
        string path = FindRealDoorSector();
        Debug.Log($"World session real door sector: {path}");
    }

    private static string FindRealDoorSector()
    {
        using var vfs = new DatVirtualFileSystem();
        string module = GameDataLocator.Find("modules/Arcanum.dat");
        if (string.IsNullOrEmpty(module)) throw new FileNotFoundException("Arcanum module archive was not found.");
        vfs.MountFile(module);
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
        }

        string protoDirectory = GameDataLocator.FindDirectory("data/proto");
        var prototypes = new ProtoLibrary(protoDirectory ?? string.Empty);
        var portals = PortalArtResolver.FromMes(MesReader.Read(vfs.ReadAllBytes("art/portal/portal.mes")));
        foreach (string sector in vfs.EnumerateFiles("maps/")
                     .Where(p => p.EndsWith(".sec", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            List<ObjectInstance> instances;
            try { instances = SectorReader.ReadObjects(vfs.ReadAllBytes(sector)); }
            catch { continue; }
            foreach (ObjectInstance instance in instances)
            {
                if (instance.Type != ObjectType.Portal || !instance.Location.HasValue) continue;
                ObjectProtoInfo proto = prototypes.Get(instance.PrototypeNumber);
                uint artId = instance.CurrentArtId ?? proto?.CurrentArtId ?? 0;
                int flags = instance.Flags ?? proto?.Flags ?? 0;
                if (ArtId.Type(artId) != ArtId.TypePortal
                    || PortalTransitionScheduler.IsWindow(artId)
                    || (flags & 0x103) != 0)
                    continue;
                string artPath = portals.Resolve(artId);
                if (string.IsNullOrEmpty(artPath) || !vfs.Exists(artPath)) continue;
                ArtFile art;
                try { art = ArtReader.Read(vfs.ReadAllBytes(artPath)); }
                catch { continue; }
                int rotation = (int)((artId >> 11) & 7);
                if (art.Rotations.Count == 1) rotation = 0;
                if (rotation >= 0 && rotation < art.Rotations.Count
                    && art.Rotations[rotation].Frames.Length > PortalTransitionScheduler.OpenFrame(artId)
                    && art.Fps > 0)
                    return sector;
            }
        }
        throw new InvalidOperationException("No renderable animated door sector was found in the mounted module.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        var session = loader.Session;
        var savedMode = OpenArcanumGraphicsSettings.Mode;
        try
        {
            var identities = string.Join(",", session.States.Values.GroupBy(s => s.Identity.Type)
                .Select(g => $"{g.Key}={g.Count()}"));
            Debug.Log($"World session identity audit: states={session.States.Count}; {identities}; " +
                $"nonpersistent={loader.LastNonPersistentIdentityCount}; inventory={loader.LastInventoryCount}; " +
                $"parentReferences={session.States.Values.Count(s => !s.ParentIdentity.IsNull)}.");
            // The default Dernholm sector contains only windows. Find and load a real animated door
            // so interruption timing is validated against real map data rather than approximated on a window.
            var owner = loader.SpriteOwners
                .Where(o => o.WorldObject.Type == ObjectType.Portal
                    && o.WorldObject.Identity.IsPersistent
                    && o.FrameCount > PortalTransitionScheduler.OpenFrame(o.WorldObject.ArtId))
                .OrderBy(o => PortalTransitionScheduler.IsWindow(o.WorldObject.ArtId))
                .FirstOrDefault(o => !PortalTransitionScheduler.IsWindow(o.WorldObject.ArtId));
            if (owner == null)
            {
                Check(loader.LoadSector(RealDoorSector), "load real door sector");
                yield return null;
                owner = loader.SpriteOwners.FirstOrDefault(o => o.WorldObject.Type == ObjectType.Portal
                    && o.WorldObject.Identity.IsPersistent && !PortalTransitionScheduler.IsWindow(o.WorldObject.ArtId)
                    && o.FrameCount > PortalTransitionScheduler.OpenFrame(o.WorldObject.ArtId));
                Check(owner != null, "real door owner created");
            }
            var id = owner.WorldObject.Identity;
            var stable = session.States[id];
            Selection.activeGameObject = owner.WorldObject.gameObject;
            Debug.Log($"World session real portal: oid={id}; sector={loader.CurrentSector}; " +
                $"proto={owner.WorldObject.PrototypeNumber}; source={owner.OriginalAssetPath}; aid=0x{stable.ArtId:X8}; " +
                $"rotation={owner.RequestedRotation}; frames={owner.FrameCount}; fps={owner.FramesPerSecond}; initialOpen={stable.PortalOpen}.");
            if (stable.PortalOpen) yield return Transition(loader, id, false);
            int stateCount = session.States.Count;
            int ownerCount = loader.RenderedObjectCount;
            int animators = loader.GetComponentsInChildren<SpriteFrameAnimator>().Length;
            int frameCount = loader.SpriteOwners.Sum(o => o.FrameCount);
            string[] issues = IssueSignatures(loader);
            var unrelated = session.States.Values.Where(s => s.Identity != id)
                .ToDictionary(s => s.Identity, s => (s.ArtId, s.PortalOpen, s.ParentIdentity));

            for (int cycle = 0; cycle < 2; cycle++)
            {
                yield return Transition(loader, id, true);
                yield return Reload(loader, id, true, stateCount, ownerCount, animators, frameCount, issues);
                Debug.Log($"World session PASS: Open -> unload -> reload (cycle {cycle + 1}).");
                yield return Transition(loader, id, false);
                yield return Reload(loader, id, false, stateCount, ownerCount, animators, frameCount, issues);
                Debug.Log($"World session PASS: Closed -> unload -> reload (cycle {cycle + 1}).");

                Check(Find(loader, id).WorldObject.RequestPortalOpen(true), "begin interrupted opening");
                Check(session.Portals.Phase(id) == PortalPhase.Opening, "opening phase");
                yield return Reload(loader, id, false, stateCount, ownerCount, animators, frameCount, issues);
                Debug.Log("World session PASS: interrupted Opening restored Closed.");
                yield return Transition(loader, id, true);
                Check(Find(loader, id).WorldObject.RequestPortalOpen(false), "begin interrupted closing");
                Check(session.Portals.Phase(id) == PortalPhase.Closing, "closing phase");
                yield return Reload(loader, id, true, stateCount, ownerCount, animators, frameCount, issues);
                Debug.Log("World session PASS: interrupted Closing restored Open.");
                yield return Transition(loader, id, false);
            }
            foreach (var pair in unrelated)
            {
                var state = session.States[pair.Key];
                Check((state.ArtId, state.PortalOpen, state.ParentIdentity) == pair.Value, "unrelated state/parent retained");
            }
            Check(session.Portals.ActiveCount == 0, "no orphan scheduler work");
            Debug.Log($"World session validation PASS: all four persistence/interruption cases twice; " +
                $"oid={id}; states={stateCount}; owners={ownerCount}; animators={animators}; frames={frameCount}; " +
                $"boundPortals={session.Portals.BoundCount}; active={session.Portals.ActiveCount}; " +
                "identity/parents/unrelated state/graphics rebuild/texture lifetime verified.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode);
            if (loader != null && loader.IsLoaded) loader.RebuildVisuals();
        }
    }

    private static IEnumerator Transition(WorldObjectSectorLoader loader, ArcanumObjectId id, bool open)
    {
        var owner = Find(loader, id);
        double begin = Time.timeAsDouble;
        double interval = (1000 / owner.FramesPerSecond) / 1000d;
        bool window = PortalTransitionScheduler.IsWindow(owner.WorldObject.ArtId);
        int first = PortalTransitionScheduler.OpenFrame(owner.WorldObject.ArtId) - 2;
        var expected = window ? new[] { open ? 1 : 0 }
            : open ? new[] { first, first + 1, first + 2 } : new[] { first + 1, first, 0 };
        var observed = new List<int>();
        Check(owner.WorldObject.RequestPortalOpen(open), "transition request");
        int previous = -1;
        while (true)
        {
            int frame = owner.CurrentFrameIndex;
            if (frame != previous)
            {
                observed.Add(frame); previous = frame;
                Debug.Log($"World session progression: {(open ? "Opening" : "Closing")}; frame={frame}; " +
                    $"elapsed={Time.timeAsDouble - begin:F4}s; ARTinterval={interval:F4}s; sprite={owner.CurrentSprite.name}.");
            }
            Check(frame == PortalTransitionScheduler.Frame(owner.WorldObject.ArtId), "presentation consumes scheduler frame");
            if (loader.Session.Portals.ActiveCount == 0) break;
            Check(Time.timeAsDouble - begin < 5, "transition timeout");
            yield return null;
        }
        Check(observed.SequenceEqual(expected), "exact observed frame sequence");
        Check(loader.Session.States[id].PortalOpen == open, "stable state completes");
        if (!window) Check(Time.timeAsDouble - begin + .001 >= 2 * interval, "no early completion");
        yield return new WaitForSeconds(.2f);
    }

    private static IEnumerator Reload(WorldObjectSectorLoader loader, ArcanumObjectId id, bool expectedOpen,
        int states, int owners, int animators, int frames, string[] issues)
    {
        var owner = Find(loader, id); var runtime = owner.WorldObject;
        uint currentAid = runtime.ArtId;
        var stable = loader.Session.States[id];
        uint stableAid = stable.ArtId;
        Vector3 position = runtime.transform.position;
        Vector3 scale = runtime.transform.localScale;
        Vector3 visual = owner.transform.localPosition;
        // Freeze only the shared scheduler during a rebuild check; no gameplay values are changed.
        loader.Session.enabled = false;
        foreach (var mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
            loader.RebuildVisuals();
            Check(runtime.ArtId == currentAid && stable.ArtId == stableAid, "graphics rebuild leaves transient/stable state intact");
            Check(runtime.transform.position == position && runtime.transform.localScale == scale
                && owner.transform.localPosition == visual, "graphics rebuild preserves placement");
            Check(owner.CurrentFrameIndex == PortalTransitionScheduler.Frame(currentAid), "rebuild preserves frame");
            yield return null;
        }
        int textures = Textures();
        Check(loader.UnloadSector() == owners, "unload count");
        loader.Session.enabled = true;
        Check(loader.Session.Portals.ActiveCount == 0 && loader.Session.Portals.BoundCount == 0, "cancel and unbind before destroy");
        Check(stable.PortalOpen == expectedOpen, "last stable state captured");
        yield return null;
        Check(runtime == null && owner == null, "runtime and presentation destroyed");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == 0, "no orphan owners");
        Check(loader.GetComponentsInChildren<WorldObject>(true).Length == 0, "no orphan runtime objects");
        Check(loader.GetComponentsInChildren<SpriteFrameAnimator>(true).Length == 0, "no orphan animators");
        Check(Textures() == textures - frames, "owned textures released");
        Check(loader.ReloadSector(), "reload succeeds");
        yield return null;
        var restored = Find(loader, id);
        Check(restored.WorldObject.IsOpen == expectedOpen && restored.WorldObject.ArtId == stableAid, "stable state restored");
        Check(restored.WorldObject.transform.position == position && restored.WorldObject.transform.localScale == scale
            && restored.transform.localPosition == visual, "reload placement");
        Check(loader.Session.States.Count == states && ReferenceEquals(loader.Session.States[id], stable), "same state record");
        Check(loader.RenderedObjectCount == owners, "reload owner count");
        Check(IssueSignatures(loader).SequenceEqual(issues), "reload preserves render issue classification");
        Check(loader.GetComponentsInChildren<WorldObject>(true).Length == owners, "no duplicate WorldObjects");
        Check(loader.GetComponentsInChildren<SpriteFrameAnimator>(true).Length == animators, "no duplicate animators");
        Check(loader.SpriteOwners.Sum(o => o.FrameCount) == frames && Textures() == textures, "frame/texture count stable");
    }

    [MenuItem("OpenArcanum/World Session/Run Complete EditMode Suite")]
    private static void RunTests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/WorldSession-EditMode.xml");
            Debug.Log($"World session COMPLETE EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
        }
    }
}
