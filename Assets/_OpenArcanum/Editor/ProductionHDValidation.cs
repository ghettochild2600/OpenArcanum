using System.Collections;
using System.Collections.Generic;
using System.IO;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Art;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class ProductionHDValidation
{
    private const string TerrainScene = "Assets/_Game/Scenes/TestTerrain.unity";

    [MenuItem("OpenArcanum/HD Production/Install and Open Real Sector Scene")]
    private static void InstallAndOpenRealSectorScene()
    {
        var scene = EditorSceneManager.OpenScene(TerrainScene);
        var terrainDemo = Object.FindFirstObjectByType<Arcanum.World.Demo.TileMapDemo>();
        if (terrainDemo == null)
            throw new MissingReferenceException("TestTerrain has no TileMapDemo host.");

        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader != null && loader.gameObject == terrainDemo.gameObject)
        {
            Object.DestroyImmediate(loader);
            loader = null;
        }

        GameObject ownerRoot = GameObject.Find("WorldObjectSectorLoader");
        if (ownerRoot == null) ownerRoot = new GameObject("WorldObjectSectorLoader");
        if (loader == null) loader = ownerRoot.GetComponent<WorldObjectSectorLoader>();
        if (loader == null) loader = ownerRoot.AddComponent<WorldObjectSectorLoader>();

        EditorUtility.SetDirty(ownerRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = loader;
        Debug.Log("OpenArcanum HD production validation: installed WorldObjectSectorLoader in TestTerrain.", loader);
    }

    [MenuItem("OpenArcanum/HD Production/Runtime Mode/Original")]
    private static void RuntimeOriginal() => SetMode(GraphicsMode.Original);

    [MenuItem("OpenArcanum/HD Production/Runtime Mode/Enhanced")]
    private static void RuntimeEnhanced() => SetMode(GraphicsMode.Enhanced);

    private static void SetMode(GraphicsMode mode)
    {
        if (!EditorApplication.isPlaying)
            throw new UnityException("Enter Play mode in TestTerrain before changing the runtime graphics mode.");

        OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
        int count = 0;
        foreach (WorldObjectSectorLoader loader in Object.FindObjectsByType<WorldObjectSectorLoader>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            loader.RebuildVisuals();
            count++;
        }

        Debug.Log($"OpenArcanum HD production validation: runtime mode={mode}; rebuilt {count} sector owner(s).");
    }

    [MenuItem("OpenArcanum/HD Production/Generate Real Map Proof Replacements")]
    private static void GenerateRealMapProofReplacements()
    {
        List<WorldObjectSpriteOwner> owners = SelectCases();
        if (owners.Count == 0)
            throw new MissingReferenceException("No real sector sprite owners are running. Open TestTerrain and enter Play mode.");

        using var vfs = MountArtArchives();
        int generated = 0;
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (WorldObjectSpriteOwner owner in owners)
        {
            // Generate the complete selected animation, not only the frame visible when
            // the menu is invoked, so the production animator stays HD throughout its loop.
            int frameCount = owner.FrameCount;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                string key = $"{owner.OriginalAssetPath}|{owner.SourceRotation}|{frameIndex}";
                if (!seen.Add(key)) continue;
                Generate(vfs, owner.OriginalAssetPath, owner.SourceRotation, frameIndex, owner.WorldObject.Type.ToString());
                generated++;
            }
        }

        OpenArcanumHDAssetLoader.ClearCache();
        Debug.Log($"OpenArcanum HD production validation: generated {generated} local real-map replacement(s). Rebuild in Enhanced mode to load them.");
    }

    [MenuItem("OpenArcanum/HD Production/Log Real Map Metrics")]
    private static void LogRealMapMetrics()
    {
        List<WorldObjectSpriteOwner> owners = SelectCases();
        if (owners.Count == 0)
            throw new MissingReferenceException("No real sector sprite owners are running. Open TestTerrain and enter Play mode.");

        foreach (WorldObjectSpriteOwner owner in owners)
        {
            Sprite sprite = owner.CurrentSprite;
            WorldObject worldObject = owner.WorldObject;
            SpriteRenderer renderer = worldObject.View;
            Vector2 pivot = new Vector2(
                sprite.pivot.x / sprite.rect.width,
                sprite.pivot.y / sprite.rect.height);

            Debug.Log(
                $"OpenArcanum HD production metrics: mode={OpenArcanumGraphicsSettings.Mode}, " +
                $"type={worldObject.Type}, proto={worldObject.PrototypeNumber}, artId=0x{worldObject.ArtId:X8}, " +
                $"source='{owner.OriginalAssetPath}', requestedRotation={owner.RequestedRotation}, " +
                $"sourceRotation={owner.SourceRotation}, frame={owner.CurrentFrameIndex}, mirrorX={owner.MirrorX}, " +
                $"frames={owner.FrameCount}, fps={owner.FramesPerSecond}, sprite='{sprite.name}', " +
                $"texture={sprite.texture.width}x{sprite.texture.height}, rect={sprite.rect.width}x{sprite.rect.height}, " +
                $"ppu={sprite.pixelsPerUnit}, spriteWorldSize={sprite.bounds.size}, renderedWorldSize={renderer.bounds.size}, " +
                $"pivot={pivot}, tile={worldObject.Tile}, gameplayPosition={worldObject.transform.position}, " +
                $"visualPosition={renderer.transform.position}, scale={renderer.transform.lossyScale}.",
                owner);
        }
    }

    [MenuItem("OpenArcanum/HD Production/Log Unresolved Record Classification")]
    private static void LogUnresolvedRecordClassification()
    {
        WorldObjectSectorLoader[] loaders = Object.FindObjectsByType<WorldObjectSectorLoader>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (loaders.Length == 0)
            throw new MissingReferenceException("No real sector loader is running. Open TestTerrain and enter Play mode.");

        using DatVirtualFileSystem vfs = MountArtArchives();
        PortalArtResolver portals = PortalArtResolver.FromMes(
            MesReader.Read(vfs.ReadAllBytes("art/portal/portal.mes")));
        foreach (WorldObjectSectorLoader loader in loaders)
        {
            if (loader.RenderedObjectCount == 0) loader.Session.SelectSector(loader.CurrentSector);
            WorldObjectSpriteOwner[] owners = Object.FindObjectsByType<WorldObjectSpriteOwner>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var counts = new Dictionary<WorldObjectSectorLoader.RenderIssueCategory, int>();
            foreach (WorldObjectSectorLoader.RenderIssue issue in loader.RenderIssues)
            {
                if (!counts.ContainsKey(issue.Category)) counts[issue.Category] = 0;
                counts[issue.Category]++;
                Debug.Log($"OpenArcanum unresolved record: {issue}", loader);

                foreach (WorldObjectSpriteOwner owner in owners)
                {
                    WorldObject candidate = owner.WorldObject;
                    if (issue.ObjectType != ObjectType.Portal
                        || candidate.Type != ObjectType.Wall
                        || candidate.Tile != issue.Tile)
                        continue;

                    uint? derivedArtId = portals.DeriveFromWall(candidate.ArtId, owner.OriginalAssetPath);
                    string derivedPath = derivedArtId.HasValue ? portals.Resolve(derivedArtId.Value) : null;
                    Debug.Log(
                        $"OpenArcanum unresolved portal context: tile={issue.Tile}, " +
                        $"wallArtId=0x{candidate.ArtId:X8}, wallPath='{owner.OriginalAssetPath}', " +
                        $"derivedArtId={(derivedArtId.HasValue ? $"0x{derivedArtId.Value:X8}" : "<none>")}, " +
                        $"derivedPath='{derivedPath ?? "<none>"}'.",
                        owner);
                }
            }

            var summary = new List<string>();
            foreach (var pair in counts) summary.Add($"{pair.Key}={pair.Value}");
            summary.Sort(System.StringComparer.Ordinal);
            Debug.Log(
                $"OpenArcanum unresolved classification: sector='{loader.CurrentSector}', " +
                $"records={loader.LastRecordCount}, rendered={loader.RenderedObjectCount}, " +
                $"inventory={loader.LastInventoryCount}, suppressed={loader.LastSuppressedCount}, " +
                $"issues={loader.RenderIssues.Count} [{string.Join(", ", summary)}].",
                loader);
        }
    }

    [MenuItem("OpenArcanum/HD Production/Run Sector Lifecycle Validation")]
    private static void RunSectorLifecycleValidation()
    {
        if (!EditorApplication.isPlaying)
            throw new UnityException("Enter Play mode in TestTerrain before running lifecycle validation.");

        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new MissingReferenceException("No real sector loader is running.");
        loader.StartCoroutine(ValidateSectorLifecycle(loader));
    }

    private static IEnumerator ValidateSectorLifecycle(WorldObjectSectorLoader loader)
    {
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
        OpenArcanumHDAssetLoader.ClearCache();

        bool loaded = loader.Session.SelectSector(loader.CurrentSector);
        yield return null;
        LifecycleSnapshot baseline = LifecycleSnapshot.Capture(loader);
        int productionPlayers = 0;
        foreach (WorldObjectSpriteOwner candidate in loader.SpriteOwners)
            if (candidate.WorldObject != null
                && candidate.WorldObject.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity)
                productionPlayers++;
        bool baselineValid = loaded
            && baseline.OwnerCount == 613
            && productionPlayers == 1
            && baseline.RootCount == 1
            && baseline.IssueCount == 0
            && baseline.DerivedPortalCount == 32;

        bool portalStateValid = ValidatePortalPresentation(loader, out string portalResult);
        loader.RebuildVisuals();
        yield return null;
        LifecycleSnapshot rebuilt = LifecycleSnapshot.Capture(loader);

        int removed = loader.RenderedObjectCount;
        string sector = loader.Session.SelectedSector;
        loader.Session.ClearSelectedSector();
        int activeRootsImmediatelyAfterUnload = CountRoots(loader, activeOnly: true);
        yield return null;
        LifecycleSnapshot unloaded = LifecycleSnapshot.Capture(loader);

        bool reloaded = loader.Session.SelectSector(sector);
        yield return null;
        LifecycleSnapshot reloadedSnapshot = LifecycleSnapshot.Capture(loader);

        bool rebuildValid = rebuilt.MatchesOwnership(baseline);
        bool unloadValid = removed == baseline.OwnerCount
            && activeRootsImmediatelyAfterUnload == 0
            && unloaded.OwnerCount == 0
            && unloaded.RootCount == 0
            && unloaded.ArtFrameTextureCount == baseline.ArtFrameTextureCount - baseline.OwnedFrameCount;
        bool reloadValid = reloaded && reloadedSnapshot.MatchesOwnership(baseline);
        bool passed = baselineValid && portalStateValid && rebuildValid && unloadValid && reloadValid;

        string message =
            $"OpenArcanum sector lifecycle validation: {(passed ? "PASS" : "FAIL")}; " +
            $"baseline=[{baseline}], rebuilt=[{rebuilt}], unloaded=[{unloaded}], " +
            $"reloaded=[{reloadedSnapshot}], removed={removed}, " +
            $"activeRootsImmediatelyAfterUnload={activeRootsImmediatelyAfterUnload}, portal=[{portalResult}].";
        if (passed) Debug.Log(message, loader);
        else Debug.LogError(message, loader);
    }

    private static bool ValidatePortalPresentation(WorldObjectSectorLoader loader, out string result)
    {
        foreach (WorldObjectSpriteOwner owner in loader.SpriteOwners)
        {
            WorldObject portal = owner.WorldObject;
            if (portal == null || portal.Type != ObjectType.Portal || owner.FrameCount < 2) continue;

            uint closedArtId = portal.ArtId & ~(0x1Fu << 14);
            bool opened = portal.TrySetPortalVisualFrame(1);
            bool openState = opened
                && portal.IsOpen
                && ((portal.ArtId >> 14) & 0x1F) == 1
                && owner.CurrentFrameIndex == 1;
            uint openArtId = portal.ArtId;
            bool rejectedInvalid = !portal.TrySetPortalVisualFrame(owner.FrameCount);
            bool invalidPreserved = portal.ArtId == openArtId;
            bool closed = portal.TrySetPortalVisualFrame(0);
            bool closedState = closed
                && !portal.IsOpen
                && portal.ArtId == closedArtId
                && owner.CurrentFrameIndex == 0;

            string portalKind = (portal.ArtId & (1u << 10)) != 0u ? "window" : "door";
            result = $"type={portalKind}, " +
                     $"frames={owner.FrameCount}, fps={owner.FramesPerSecond}, opened={openState}, " +
                     $"invalidRejected={rejectedInvalid && invalidPreserved}, closed={closedState}";
            return openState && rejectedInvalid && invalidPreserved && closedState;
        }

        result = "no multi-frame portal found";
        return false;
    }

    private static int CountRoots(WorldObjectSectorLoader loader, bool activeOnly)
    {
        int count = 0;
        for (int index = 0; index < loader.transform.childCount; index++)
        {
            Transform child = loader.transform.GetChild(index);
            if (child.name == "WorldObjects" && (!activeOnly || child.gameObject.activeSelf)) count++;
        }
        return count;
    }

    private readonly struct LifecycleSnapshot
    {
        public int OwnerCount { get; }
        public int RootCount { get; }
        public int AnimatorCount { get; }
        public int OwnedFrameCount { get; }
        public int ArtFrameTextureCount { get; }
        public int IssueCount { get; }
        public int DerivedPortalCount { get; }

        private LifecycleSnapshot(
            int ownerCount,
            int rootCount,
            int animatorCount,
            int ownedFrameCount,
            int artFrameTextureCount,
            int issueCount,
            int derivedPortalCount)
        {
            OwnerCount = ownerCount;
            RootCount = rootCount;
            AnimatorCount = animatorCount;
            OwnedFrameCount = ownedFrameCount;
            ArtFrameTextureCount = artFrameTextureCount;
            IssueCount = issueCount;
            DerivedPortalCount = derivedPortalCount;
        }

        public static LifecycleSnapshot Capture(WorldObjectSectorLoader loader)
        {
            int frameCount = 0;
            int animatorCount = 0;
            foreach (WorldObjectSpriteOwner owner in loader.SpriteOwners)
            {
                if (owner == null) continue;
                frameCount += owner.FrameCount;
                if (owner.GetComponent<SpriteFrameAnimator>() != null) animatorCount++;
            }

            int artTextures = 0;
            foreach (Texture2D texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (texture != null && texture.name == "ArtFrame") artTextures++;

            return new LifecycleSnapshot(
                loader.RenderedObjectCount,
                CountRoots(loader, activeOnly: false),
                animatorCount,
                frameCount,
                artTextures,
                loader.RenderIssues.Count,
                loader.LastDerivedPortalCount);
        }

        public bool MatchesOwnership(LifecycleSnapshot other)
            => OwnerCount == other.OwnerCount
               && RootCount == other.RootCount
               && AnimatorCount == other.AnimatorCount
               && OwnedFrameCount == other.OwnedFrameCount
               && ArtFrameTextureCount == other.ArtFrameTextureCount
               && IssueCount == other.IssueCount
               && DerivedPortalCount == other.DerivedPortalCount;

        public override string ToString()
            => $"owners={OwnerCount}, roots={RootCount}, animators={AnimatorCount}, " +
               $"frames={OwnedFrameCount}, artTextures={ArtFrameTextureCount}, " +
               $"issues={IssueCount}, derivedPortals={DerivedPortalCount}";
    }

    private static List<WorldObjectSpriteOwner> SelectCases()
    {
        var all = Object.FindObjectsByType<WorldObjectSpriteOwner>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        var selected = new List<WorldObjectSpriteOwner>();

        AddFirst(selected, all, o => IsStaticObject(o.WorldObject.Type) && o.FrameCount == 1);
        AddFirst(selected, all, o => IsStaticObject(o.WorldObject.Type) && o.FrameCount > 1);
        AddFirst(selected, all, o => o.WorldObject.Type == ObjectType.Pc || o.WorldObject.Type == ObjectType.Npc);

        // Sparse sectors may not contain every preferred family. Still report a real placed object.
        if (selected.Count == 0 && all.Length > 0) selected.Add(all[0]);
        return selected;
    }

    private static void AddFirst(
        List<WorldObjectSpriteOwner> selected,
        WorldObjectSpriteOwner[] candidates,
        System.Predicate<WorldObjectSpriteOwner> predicate)
    {
        foreach (WorldObjectSpriteOwner owner in candidates)
        {
            if (!predicate(owner) || selected.Contains(owner)) continue;
            selected.Add(owner);
            return;
        }
    }

    private static bool IsStaticObject(ObjectType type)
        => type == ObjectType.Wall || type == ObjectType.Portal || type == ObjectType.Container || type == ObjectType.Scenery;

    private static DatVirtualFileSystem MountArtArchives()
    {
        var vfs = new DatVirtualFileSystem();
        int mounted = 0;
        foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
        {
            string path = GameDataLocator.Find(archive);
            if (string.IsNullOrEmpty(path)) continue;
            vfs.MountFile(path);
            mounted++;
        }
        if (mounted == 0)
        {
            vfs.Dispose();
            throw new FileNotFoundException("No arcanum*.dat archives were found.");
        }
        return vfs;
    }

    private static void Generate(
        DatVirtualFileSystem vfs,
        string sourcePath,
        int rotation,
        int frameIndex,
        string label)
    {
        ArtFile art = ArtReader.Read(vfs.ReadAllBytes(sourcePath));
        ArtFrame frame = art.Rotations[rotation].Frames[frameIndex];
        Texture2D source = ArtTextureFactory.CreateTexture(frame, art.PrimaryPalette);
        int scale = OpenArcanumHDAssetLoader.ReplacementScale;
        int width = frame.Width * scale;
        int height = frame.Height * scale;
        var replacement = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
        Color32[] sourcePixels = source.GetPixels32();
        var pixels = new Color32[width * height];

        for (int y = 0; y < frame.Height; y++)
        for (int x = 0; x < frame.Width; x++)
        {
            Color32 color = sourcePixels[y * frame.Width + x];
            if (color.a != 0)
            {
                color.r = (byte)Mathf.Min(255, color.r + 70);
                color.g = (byte)Mathf.Min(255, color.g + 15);
                color.b = (byte)(color.b * 0.45f);
            }

            for (int dy = 0; dy < scale; dy++)
            for (int dx = 0; dx < scale; dx++)
                pixels[(y * scale + dy) * width + x * scale + dx] = color;
        }

        replacement.SetPixels32(pixels);
        replacement.Apply(updateMipmaps: false);
        string output = OpenArcanumHDAssetLoader.GetReplacementPath(sourcePath, rotation, frameIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllBytes(output, replacement.EncodeToPNG());
        Debug.Log($"OpenArcanum HD real-map proof: type={label}, source='{sourcePath}', rotation={rotation}, frame={frameIndex}, original={frame.Width}x{frame.Height}, replacement={width}x{height}, path='{output}'.");
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(replacement);
    }
}
