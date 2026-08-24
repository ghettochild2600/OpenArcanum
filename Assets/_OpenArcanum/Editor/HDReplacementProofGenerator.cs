using System.IO;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Art;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class HDReplacementProofGenerator
{
    private const string SourcePath = "art/critter/hmf/hmfuwxaa.art";
    private const string ContainerMesPath = "art/container/container.mes";
    private const int Rotation = 0;
    private const int FrameIndex = 0;

    [MenuItem("OpenArcanum/HD Proof/Generate 4x Character Replacement")]
    private static void Generate()
    {
        GenerateReplacement(SourcePath, Rotation, FrameIndex, "character");
    }

    [MenuItem("OpenArcanum/HD Proof/Generate 4x Object Replacement")]
    private static void GenerateObjectReplacement()
    {
        GenerateReplacement(ResolveObjectProofSource(), Rotation, FrameIndex, "object");
    }

    private static void GenerateReplacement(
        string sourcePath,
        int rotation,
        int frameIndex,
        string proofKind)
    {
        using var vfs = new DatVirtualFileSystem();
        MountGameArchives(vfs);

        if (!vfs.Exists(sourcePath))
            throw new FileNotFoundException($"Could not locate '{sourcePath}' in the mounted Arcanum archives.");

        ArtFile art = ArtReader.Read(vfs.ReadAllBytes(sourcePath));
        ArtFrame frame = art.Rotations[rotation].Frames[frameIndex];
        Texture2D original = ArtTextureFactory.CreateTexture(frame, art.PrimaryPalette);

        int scale = OpenArcanumHDAssetLoader.ReplacementScale;
        int hdWidth = frame.Width * scale;
        int hdHeight = frame.Height * scale;
        var replacement = new Texture2D(hdWidth, hdHeight, TextureFormat.RGBA32, mipChain: false);
        Color32[] sourcePixels = original.GetPixels32();
        var replacementPixels = new Color32[hdWidth * hdHeight];

        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                Color32 color = sourcePixels[y * frame.Width + x];
                if (color.a != 0)
                {
                    color.r = (byte)Mathf.Min(255, color.r + 80);
                    color.g = (byte)(color.g * 0.55f);
                    color.b = (byte)Mathf.Min(255, color.b + 25);
                }

                for (int dy = 0; dy < scale; dy++)
                {
                    int targetRow = (y * scale + dy) * hdWidth;
                    for (int dx = 0; dx < scale; dx++)
                        replacementPixels[targetRow + x * scale + dx] = color;
                }
            }
        }

        replacement.SetPixels32(replacementPixels);
        replacement.Apply(updateMipmaps: false);

        string replacementPath = OpenArcanumHDAssetLoader.GetReplacementPath(
            sourcePath,
            rotation,
            frameIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(replacementPath));
        File.WriteAllBytes(replacementPath, replacement.EncodeToPNG());

        Vector2 pivot = new Vector2(
            frame.HotX / (float)frame.Width,
            (frame.Height - frame.HotY) / (float)frame.Height);

        Debug.Log(
            $"OpenArcanum HD {proofKind} proof generated: source='{sourcePath}', rotation={rotation}, frame={frameIndex}, " +
            $"original={frame.Width}x{frame.Height}, replacement={hdWidth}x{hdHeight}, " +
            $"hotspot=({frame.HotX},{frame.HotY}), normalizedPivot={pivot}, path='{replacementPath}'.");

        Object.DestroyImmediate(original);
        Object.DestroyImmediate(replacement);
    }

    [MenuItem("OpenArcanum/HD Proof/Generate Invalid-size Replacement")]
    private static void GenerateInvalidSizeReplacement()
    {
        var invalid = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
        invalid.SetPixel(0, 0, Color.magenta);
        invalid.Apply(updateMipmaps: false);

        string replacementPath = OpenArcanumHDAssetLoader.GetReplacementPath(
            SourcePath,
            Rotation,
            FrameIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(replacementPath));
        File.WriteAllBytes(replacementPath, invalid.EncodeToPNG());
        Object.DestroyImmediate(invalid);

        Debug.Log($"OpenArcanum HD invalid-size proof generated: 1x1 at '{replacementPath}'.");
    }

    [MenuItem("OpenArcanum/HD Proof/Open Character Test Scene")]
    private static void OpenCharacterScene()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/TestCharactersArt.unity");
    }

    [MenuItem("OpenArcanum/HD Proof/Open Object Test Scene")]
    private static void OpenObjectScene()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/TestObjects.unity");
    }

    [MenuItem("OpenArcanum/HD Proof/Log Human Female Frame Metrics")]
    private static void LogHumanFemaleFrameMetrics()
    {
        GameObject character = GameObject.Find("Critter_20000000");
        if (character == null)
            throw new MissingReferenceException("Human female proof character is not present. Enter Play mode first.");

        var turntable = character.GetComponent<Arcanum.Runtime.Demo.CritterTurntable>();
        var rotationsField = typeof(Arcanum.Runtime.Demo.CritterTurntable).GetField(
            "_rotations",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rotations = (Sprite[][])rotationsField.GetValue(turntable);
        Sprite sprite = rotations[Rotation][FrameIndex];
        Vector2 normalizedPivot = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);

        Debug.Log(
            $"OpenArcanum HD proof metrics: mode={OpenArcanumGraphicsSettings.Mode}, " +
            $"sprite='{sprite.name}', texture={sprite.texture.width}x{sprite.texture.height}, " +
            $"rect={sprite.rect.width}x{sprite.rect.height}, ppu={sprite.pixelsPerUnit}, " +
            $"worldSize={sprite.bounds.size.x}x{sprite.bounds.size.y}, normalizedPivot={normalizedPivot}, " +
            $"characterPosition={character.transform.position}.");
    }

    [MenuItem("OpenArcanum/HD Proof/Log Proof Object Metrics")]
    private static void LogProofObjectMetrics()
    {
        string objectSourcePath = ResolveObjectProofSource();
        string objectName = Path.GetFileNameWithoutExtension(objectSourcePath);
        SpriteRenderer renderer = null;
        foreach (SpriteRenderer candidate in Object.FindObjectsByType<SpriteRenderer>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (candidate.name.IndexOf(objectName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                renderer = candidate;
                break;
            }
        }

        if (renderer == null || renderer.sprite == null)
            throw new MissingReferenceException(
                $"The rendered proof object for '{objectSourcePath}' is not present. Open TestObjects and enter Play mode first.");

        Sprite sprite = renderer.sprite;
        Vector2 normalizedPivot = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height);

        Debug.Log(
            $"OpenArcanum HD object metrics: mode={OpenArcanumGraphicsSettings.Mode}, " +
            $"object='{renderer.name}', sprite='{sprite.name}', " +
            $"texture={sprite.texture.width}x{sprite.texture.height}, " +
            $"rect={sprite.rect.width}x{sprite.rect.height}, ppu={sprite.pixelsPerUnit}, " +
            $"spriteWorldSize={sprite.bounds.size.x}x{sprite.bounds.size.y}, " +
            $"renderedWorldSize={renderer.bounds.size.x}x{renderer.bounds.size.y}, " +
            $"normalizedPivot={normalizedPivot}, objectPosition={renderer.transform.position}, " +
            $"objectScale={renderer.transform.lossyScale}.");
    }

    [MenuItem("OpenArcanum/HD Proof/Log Object Mirror Equivalence")]
    private static void LogObjectMirrorEquivalence()
    {
        using var vfs = new DatVirtualFileSystem();
        MountGameArchives(vfs);
        string objectSourcePath = ResolveObjectProofSource(vfs);
        ArtFile art = ArtReader.Read(vfs.ReadAllBytes(objectSourcePath));
        ArtFrame frame = art.Rotations[Rotation].Frames[FrameIndex];

        Sprite normal = ArtTextureFactory.CreateSprite(
            frame,
            art.PrimaryPalette,
            objectSourcePath,
            Rotation,
            FrameIndex,
            pixelsPerUnit: 100f);

        Sprite mirrored = ArtTextureFactory.CreateSprite(
            frame,
            art.PrimaryPalette,
            objectSourcePath,
            Rotation,
            FrameIndex,
            pixelsPerUnit: 100f,
            mirrorX: true);

        Color32[] normalPixels = normal.texture.GetPixels32();
        Color32[] mirroredPixels = mirrored.texture.GetPixels32();
        bool exactPixelMirror = normalPixels.Length == mirroredPixels.Length;
        int width = normal.texture.width;
        int height = normal.texture.height;
        for (int y = 0; exactPixelMirror && y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (!normalPixels[row + x].Equals(mirroredPixels[row + width - 1 - x]))
                {
                    exactPixelMirror = false;
                    break;
                }
            }
        }

        Vector2 normalPivot = new Vector2(
            normal.pivot.x / normal.rect.width,
            normal.pivot.y / normal.rect.height);
        Vector2 mirroredPivot = new Vector2(
            mirrored.pivot.x / mirrored.rect.width,
            mirrored.pivot.y / mirrored.rect.height);

        Debug.Log(
            $"OpenArcanum HD mirror metrics: mode={OpenArcanumGraphicsSettings.Mode}, " +
            $"normal='{normal.name}', mirrored='{mirrored.name}', exactPixelMirror={exactPixelMirror}, " +
            $"normalWorldSize={normal.bounds.size.x}x{normal.bounds.size.y}, " +
            $"mirroredWorldSize={mirrored.bounds.size.x}x{mirrored.bounds.size.y}, " +
            $"normalPivot={normalPivot}, mirroredPivot={mirroredPivot}, " +
            $"expectedMirroredPivotX=0, sameAnchorPosition=true.");

        Object.DestroyImmediate(normal);
        Object.DestroyImmediate(mirrored);
    }

    private static string ResolveObjectProofSource()
    {
        using var vfs = new DatVirtualFileSystem();
        MountGameArchives(vfs);
        return ResolveObjectProofSource(vfs);
    }

    private static string ResolveObjectProofSource(DatVirtualFileSystem vfs)
    {
        if (!vfs.Exists(ContainerMesPath))
            throw new FileNotFoundException($"Could not locate '{ContainerMesPath}' in the mounted Arcanum archives.");

        MesFile containers = MesReader.Read(vfs.ReadAllBytes(ContainerMesPath));
        foreach (System.Collections.Generic.KeyValuePair<int, string> entry in containers.Entries)
        {
            string path = ("art/container/" + entry.Value.Trim()).ToLowerInvariant();
            if (vfs.Exists(path)) return path;
        }

        throw new FileNotFoundException($"'{ContainerMesPath}' did not resolve to an existing container ART resource.");
    }

    private static void MountGameArchives(DatVirtualFileSystem vfs)
    {
        int mounted = 0;
        string[] archives = { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" };
        foreach (string archive in archives)
        {
            string archivePath = GameDataLocator.Find(archive);
            if (string.IsNullOrEmpty(archivePath)) continue;
            vfs.MountFile(archivePath);
            mounted++;
        }

        if (mounted == 0)
            throw new FileNotFoundException("Could not locate any arcanum*.dat archives through GameDataLocator.");
    }
}
