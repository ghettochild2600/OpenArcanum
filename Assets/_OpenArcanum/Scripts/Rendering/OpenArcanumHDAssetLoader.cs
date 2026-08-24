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
        private static readonly Dictionary<string, Texture2D> TextureCache =
            new Dictionary<string, Texture2D>(
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

            if (TextureCache.TryGetValue(
                    replacementPath,
                    out Texture2D cachedTexture))
            {
                texture = cachedTexture;
                return texture != null;
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
                    UnityEngine.Object.Destroy(
                        loadedTexture);

                    Debug.LogWarning(
                        $"OpenArcanum: Failed to decode HD texture '{replacementPath}'.");

                    return false;
                }

                loadedTexture.filterMode =
                    FilterMode.Bilinear;

                loadedTexture.wrapMode =
                    TextureWrapMode.Clamp;

                TextureCache[replacementPath] =
                    loadedTexture;

                texture =
                    loadedTexture;

                Debug.Log(
                    $"OpenArcanum: Loaded HD replacement '{replacementPath}'.");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"OpenArcanum: Failed loading HD replacement '{replacementPath}': {ex.Message}");

                return false;
            }
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