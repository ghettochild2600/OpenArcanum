using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenArcanum.Rendering
{
    /// <summary>
    /// Loads optional high-resolution replacement artwork for OpenArcanum.
    ///
    /// HD assets are stored outside Unity's normal Assets folder:
    ///
    ///     OpenArcanum-Unity/
    ///         HDAssets/
    ///
    /// This keeps replacement artwork separate from:
    /// - Original Arcanum game data
    /// - Unity project assets
    /// - Git/source control
    ///
    /// If an HD replacement does not exist, the caller simply continues
    /// using the original Arcanum artwork.
    /// </summary>
    public static class OpenArcanumHDAssetLoader
    {
        public const int ReplacementScale = 4;

        private static readonly Dictionary<string, Texture2D> TextureCache =
            new Dictionary<string, Texture2D>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> RejectedReplacementPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Root folder containing optional HD replacement artwork.
        /// </summary>
        public static string HDAssetsRoot
        {
            get
            {
                return Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        "HDAssets"));
            }
        }

        /// <summary>
        /// Attempts to load an HD replacement texture.
        ///
        /// Example original Arcanum path:
        ///
        ///     art/tile/grass/grass01.art
        ///
        /// Rotation:
        ///
        ///     0
        ///
        /// Frame:
        ///
        ///     0
        ///
        /// The PNG is accepted only when its width and height are exactly
        /// <see cref="ReplacementScale"/> times the decoded ART frame dimensions.
        ///
        /// Expected replacement:
        ///
        ///     HDAssets/
        ///         art/
        ///             tile/
        ///                 grass/
        ///                     grass01/
        ///                         r0_f0.png
        ///
        /// The directory layout deliberately mirrors Arcanum's internal
        /// resource paths so thousands of future replacements can remain
        /// organized and predictable.
        /// </summary>
        public static bool TryLoadTexture(
            string originalAssetPath,
            int rotation,
            int frame,
            int originalFrameWidth,
            int originalFrameHeight,
            out Texture2D texture)
        {
            return TryLoadTexture(
                originalAssetPath,
                rotation,
                frame,
                originalFrameWidth,
                originalFrameHeight,
                mirrorX: false,
                out texture);
        }

        /// <summary>
        /// Attempts to load an HD replacement and optionally mirrors its pixels using
        /// the same horizontal pixel reversal as the original ART rendering path.
        /// Mirrored variants are cached separately from their source textures.
        /// </summary>
        public static bool TryLoadTexture(
            string originalAssetPath,
            int rotation,
            int frame,
            int originalFrameWidth,
            int originalFrameHeight,
            bool mirrorX,
            out Texture2D texture)
        {
            texture = null;

            // HD replacement artwork is only active in Enhanced mode.
            if (!OpenArcanumGraphicsSettings.Enhanced)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(originalAssetPath))
            {
                return false;
            }

            string replacementPath =
                GetReplacementPath(
                    originalAssetPath,
                    rotation,
                    frame);

            string cacheKey = mirrorX
                ? replacementPath + "|mirrorX"
                : replacementPath;

            if (TextureCache.TryGetValue(
                    cacheKey,
                    out Texture2D cachedTexture))
            {
                texture = cachedTexture;
                return texture != null;
            }

            if (mirrorX
                && TextureCache.TryGetValue(
                    replacementPath,
                    out Texture2D cachedSourceTexture))
            {
                texture = CreateMirroredTexture(
                    cachedSourceTexture,
                    replacementPath);

                TextureCache[cacheKey] = texture;
                return true;
            }

            if (RejectedReplacementPaths.Contains(replacementPath))
            {
                return false;
            }

            if (!File.Exists(replacementPath))
            {
                return false;
            }

            try
            {
                byte[] fileBytes =
                    File.ReadAllBytes(
                        replacementPath);

                var loadedTexture =
                    new Texture2D(
                        2,
                        2,
                        TextureFormat.RGBA32,
                        mipChain: false)
                    {
                        filterMode =
                            FilterMode.Bilinear,

                        wrapMode =
                            TextureWrapMode.Clamp,

                        name =
                            $"HD_{Path.GetFileNameWithoutExtension(replacementPath)}"
                    };

                bool loaded =
                    loadedTexture.LoadImage(
                        fileBytes,
                        markNonReadable: false);

                if (!loaded)
                {
                    RejectedReplacementPaths.Add(
                        replacementPath);

                    UnityEngine.Object.Destroy(
                        loadedTexture);

                    Debug.LogWarning(
                        $"OpenArcanum: Failed to decode HD texture '{replacementPath}'.");

                    return false;
                }

                int expectedWidth =
                    originalFrameWidth * ReplacementScale;

                int expectedHeight =
                    originalFrameHeight * ReplacementScale;

                if (originalFrameWidth <= 0
                    || originalFrameHeight <= 0
                    || loadedTexture.width != expectedWidth
                    || loadedTexture.height != expectedHeight)
                {
                    RejectedReplacementPaths.Add(
                        replacementPath);

                    Debug.LogWarning(
                        $"OpenArcanum: Rejected HD replacement '{replacementPath}': " +
                        $"expected exactly {expectedWidth}x{expectedHeight} pixels " +
                        $"({ReplacementScale}x the original {originalFrameWidth}x{originalFrameHeight} frame), " +
                        $"but found {loadedTexture.width}x{loadedTexture.height}. Falling back to original ART.");

                    UnityEngine.Object.Destroy(
                        loadedTexture);

                    return false;
                }

                loadedTexture.filterMode =
                    FilterMode.Bilinear;

                loadedTexture.wrapMode =
                    TextureWrapMode.Clamp;

                TextureCache[replacementPath] =
                    loadedTexture;

                if (mirrorX)
                {
                    texture = CreateMirroredTexture(
                        loadedTexture,
                        replacementPath);

                    TextureCache[cacheKey] = texture;
                }
                else
                {
                    texture = loadedTexture;
                }

                Debug.Log(
                    $"OpenArcanum: Loaded HD replacement '{replacementPath}'.");

                return true;
            }
            catch (Exception ex)
            {
                RejectedReplacementPaths.Add(
                    replacementPath);

                Debug.LogWarning(
                    $"OpenArcanum: Failed loading HD replacement '{replacementPath}': {ex.Message}");

                return false;
            }
        }

        private static Texture2D CreateMirroredTexture(
            Texture2D source,
            string replacementPath)
        {
            int width = source.width;
            int height = source.height;
            Color32[] sourcePixels = source.GetPixels32();
            var mirroredPixels = new Color32[sourcePixels.Length];

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    mirroredPixels[row + width - 1 - x] = sourcePixels[row + x];
            }

            var mirrored = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                filterMode = source.filterMode,
                wrapMode = source.wrapMode,
                name = $"HD_{Path.GetFileNameWithoutExtension(replacementPath)}_MirrorX"
            };

            mirrored.SetPixels32(mirroredPixels);
            mirrored.Apply(updateMipmaps: false);
            return mirrored;
        }

        /// <summary>
        /// Returns the path where OpenArcanum expects an HD replacement
        /// for the specified original Arcanum ART resource/frame.
        /// </summary>
        public static string GetReplacementPath(
            string originalAssetPath,
            int rotation,
            int frame)
        {
            string normalized =
                NormalizeAssetPath(
                    originalAssetPath);

            string withoutExtension =
                Path.ChangeExtension(
                    normalized,
                    null);

            string relativePath =
                Path.Combine(
                    withoutExtension,
                    $"r{rotation}_f{frame}.png");

            return Path.GetFullPath(
                Path.Combine(
                    HDAssetsRoot,
                    relativePath));
        }

        /// <summary>
        /// Checks whether an HD replacement exists without loading it.
        /// </summary>
        public static bool ReplacementExists(
            string originalAssetPath,
            int rotation,
            int frame)
        {
            string path =
                GetReplacementPath(
                    originalAssetPath,
                    rotation,
                    frame);

            return File.Exists(path);
        }

        // SubsystemRegistration also runs when entering Play mode with domain reload disabled.
        // Starting each runtime session clean prevents an edited or renamed local PNG from
        // leaving a stale accepted/rejected cache entry behind between validation runs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeCache()
        {
            ClearCache();
        }

        /// <summary>
        /// Clears loaded replacement textures.
        ///
        /// Primarily useful while developing and testing replacement art.
        /// </summary>
        public static void ClearCache()
        {
            foreach (Texture2D texture in TextureCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(
                        texture);
                }
            }

            TextureCache.Clear();
            RejectedReplacementPaths.Clear();
        }

        /// <summary>
        /// Converts an Arcanum resource path into a safe relative path.
        /// </summary>
        private static string NormalizeAssetPath(
            string assetPath)
        {
            string normalized =
                assetPath
                    .Replace('\\', '/')
                    .Trim();

            while (normalized.StartsWith("/"))
            {
                normalized =
                    normalized.Substring(1);
            }

            // Prevent a malformed resource name from escaping HDAssets.
            normalized =
                normalized.Replace(
                    "../",
                    string.Empty);

            normalized =
                normalized.Replace(
                    "..\\",
                    string.Empty);

            return normalized.Replace(
                '/',
                Path.DirectorySeparatorChar);
        }
    }
}
