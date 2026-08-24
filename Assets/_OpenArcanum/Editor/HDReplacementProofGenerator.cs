using System.IO;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Runtime;
using Arcanum.Runtime.Art;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class HDReplacementProofGenerator
{
    private const string SourcePath = "art/critter/hmf/hmfuwxaa.art";
    private const int Rotation = 0;
    private const int FrameIndex = 0;

    [MenuItem("OpenArcanum/HD Proof/Generate 4x Character Replacement")]
    private static void Generate()
    {
        string archivePath = GameDataLocator.Find("arcanum1.dat");
        if (string.IsNullOrEmpty(archivePath))
            throw new FileNotFoundException("Could not locate arcanum1.dat through GameDataLocator.");

        using var vfs = new DatVirtualFileSystem();
        vfs.MountFile(archivePath);

        if (!vfs.Exists(SourcePath))
            throw new FileNotFoundException($"Could not locate '{SourcePath}' in arcanum1.dat.");

        ArtFile art = ArtReader.Read(vfs.ReadAllBytes(SourcePath));
        ArtFrame frame = art.Rotations[Rotation].Frames[FrameIndex];
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
            SourcePath,
            Rotation,
            FrameIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(replacementPath));
        File.WriteAllBytes(replacementPath, replacement.EncodeToPNG());

        Vector2 pivot = new Vector2(
            frame.HotX / (float)frame.Width,
            (frame.Height - frame.HotY) / (float)frame.Height);

        Debug.Log(
            $"OpenArcanum HD proof generated: source='{SourcePath}', rotation={Rotation}, frame={FrameIndex}, " +
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
}
