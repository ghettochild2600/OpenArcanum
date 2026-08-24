using System.Collections.Generic;
using System.IO;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
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
