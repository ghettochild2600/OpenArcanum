using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Text;
using Arcanum.Runtime;
using Arcanum.Runtime.Art;
using OpenArcanum.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenArcanum.UI
{
    /// <summary>
    /// Resolves interface IDs directly from mounted retail data. Generated UIReference PNGs are never consulted.
    /// The resolver owns its VFS, decoded ART cache, and Unity resources and therefore has an explicit lifetime.
    /// </summary>
    public sealed class RetailUiAssetResolver : IDisposable
    {
        public const string InterfaceMesPath = "art/interface/interface.mes";

        private readonly DatVirtualFileSystem _vfs;
        private readonly MesFile _interfaceTable;
        private readonly Action<string> _diagnostic;
        private readonly Dictionary<int, DecodedSource> _sources = new Dictionary<int, DecodedSource>();
        private readonly Dictionary<UiAssetKey, UiResolvedAsset> _frames =
            new Dictionary<UiAssetKey, UiResolvedAsset>();
        private readonly Dictionary<string, MesFile> _messageTables =
            new Dictionary<string, MesFile>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;

        public int CachedFrameCount => _frames.Count;
        public int DecodedSourceCount => _sources.Count;

        public RetailUiAssetResolver(DatVirtualFileSystem vfs, Action<string> diagnostic = null)
        {
            _vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            _diagnostic = diagnostic ?? (message => Debug.LogWarning(message));
            if (!_vfs.Exists(InterfaceMesPath))
                throw new FileNotFoundException($"Retail UI table '{InterfaceMesPath}' is unavailable.");
            _interfaceTable = MesReader.Read(_vfs.ReadAllBytes(InterfaceMesPath));
        }

        public static RetailUiAssetResolver CreateProduction(Action<string> diagnostic = null)
        {
            var vfs = new DatVirtualFileSystem();
            try
            {
                string looseModule = GameDataLocator.FindDirectory("modules/Arcanum");
                if (!string.IsNullOrEmpty(looseModule)) vfs.MountDirectory(looseModule);

                foreach (string archive in new[]
                         {
                             "modules/Arcanum.dat", "arcanum4.dat", "arcanum3.dat", "arcanum2.dat", "arcanum1.dat",
                         })
                {
                    string path = GameDataLocator.Find(archive);
                    if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
                }

                return new RetailUiAssetResolver(vfs, diagnostic);
            }
            catch
            {
                vfs.Dispose();
                throw;
            }
        }

        public bool TryResolve(UiAssetKey key, out UiResolvedAsset asset)
        {
            ThrowIfDisposed();
            if (_frames.TryGetValue(key, out asset) && asset != null && asset.Sprite != null) return true;

            asset = null;
            if (key.SourceId < 0 || key.Palette < 0 || key.Rotation < 0 || key.Frame < 0)
                return Fail(key, "negative source, palette, rotation, or frame index");

            if (!TryGetSource(key.SourceId, out DecodedSource source)) return false;
            ArtFile art = source.Art;
            if (key.Palette >= art.Palettes.Count)
                return Fail(key, $"palette {key.Palette} is outside 0..{art.Palettes.Count - 1}");
            if (key.Rotation >= art.Rotations.Count)
                return Fail(key, $"rotation {key.Rotation} is outside 0..{art.Rotations.Count - 1}");

            ArtFrame[] frames = art.Rotations[key.Rotation].Frames;
            if (key.Frame >= frames.Length)
                return Fail(key, $"frame {key.Frame} is outside 0..{frames.Length - 1}");

            ArtFrame frame = frames[key.Frame];
            ArtPalette palette = art.Palettes[key.Palette];
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                texture = ArtTextureFactory.CreateTexture(frame, palette);
                texture.name = $"UI_Original_{key}";
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;

                Vector2 pivot = Pivot(frame);
                Vector4 border = SourceUiScaleMetadataCatalog.Get(key.SourceId).Border;
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    pivot,
                    pixelsPerUnit: 1f,
                    extrude: 0,
                    meshType: SpriteMeshType.FullRect,
                    border: border);
                sprite.name = $"UI_Original_{key}";

                asset = new UiResolvedAsset(
                    key,
                    source.Path,
                    texture,
                    sprite,
                    new Vector2Int(frame.Width, frame.Height),
                    new Vector2Int(frame.HotX, frame.HotY),
                    new Vector2Int(frame.OffsetX, frame.OffsetY),
                    pivot,
                    frame.Indices.Any(index => index == 0),
                    UiAssetSkin.Original,
                    UiAssetSkin.Original,
                    isFallback: false,
                    textureScale: 1);
                _frames[key] = asset;
                return true;
            }
            catch (Exception ex)
            {
                Destroy(sprite);
                Destroy(texture);
                return Fail(key, ex.Message);
            }
        }

        /// <summary>
        /// Reads localized retail text through the same mounted source hierarchy as interface artwork. Screen
        /// presenters use this instead of copying message tables or opening a second archive stack.
        /// </summary>
        public bool TryReadMessage(string virtualPath, int key, out string value)
        {
            ThrowIfDisposed();
            value = null;
            if (string.IsNullOrWhiteSpace(virtualPath)) return false;
            string normalized = DatFileEntry.Normalize(virtualPath.Replace('\\', '/'));
            if (!_messageTables.TryGetValue(normalized, out MesFile table))
            {
                if (!_vfs.Exists(normalized)) return false;
                try
                {
                    table = MesReader.Read(_vfs.ReadAllBytes(normalized));
                    _messageTables.Add(normalized, table);
                }
                catch (Exception exception)
                {
                    string message = $"OpenArcanum UI: retail message table '{normalized}' unavailable: "
                                     + exception.Message;
                    if (_reported.Add(message)) _diagnostic(message);
                    return false;
                }
            }
            return table.TryGet(key, out value);
        }

        private bool TryGetSource(int sourceId, out DecodedSource source)
        {
            if (_sources.TryGetValue(sourceId, out source)) return source != null;
            if (!_interfaceTable.TryGet(sourceId, out string value))
            {
                _sources[sourceId] = null;
                return Fail(new UiAssetKey(sourceId), "source ID is absent from interface.mes");
            }

            string path = ResolveArtPath(value);
            if (!_vfs.Exists(path))
            {
                _sources[sourceId] = null;
                return Fail(new UiAssetKey(sourceId), $"retail target '{path}' is absent from mounted data");
            }

            try
            {
                ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(path));
                if (art.Palettes.Count == 0)
                    throw new InvalidDataException("ART has no embedded palette");
                source = new DecodedSource(path, art);
                _sources[sourceId] = source;
                return true;
            }
            catch (Exception ex)
            {
                _sources[sourceId] = null;
                return Fail(new UiAssetKey(sourceId), $"failed decoding '{path}': {ex.Message}");
            }
        }

        private bool Fail(UiAssetKey key, string reason)
        {
            string message = $"OpenArcanum UI: Original asset {key} unavailable: {reason}.";
            if (_reported.Add(message)) _diagnostic(message);
            return false;
        }

        private static string ResolveArtPath(string tableValue)
        {
            string value = (tableValue ?? string.Empty).Trim().Replace('\\', '/');
            if (!value.EndsWith(".art", StringComparison.OrdinalIgnoreCase)) value += ".art";
            if (!value.StartsWith("art/", StringComparison.OrdinalIgnoreCase)) value = "art/interface/" + value;
            return DatFileEntry.Normalize(value);
        }

        internal static string SafeStem(string sourcePath)
        {
            string value = Path.GetFileNameWithoutExtension(sourcePath)?.ToLowerInvariant() ?? "unknown";
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
                builder.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-');
            return builder.ToString();
        }

        private static Vector2 Pivot(ArtFrame frame)
        {
            int width = Mathf.Max(frame.Width, 1);
            int height = Mathf.Max(frame.Height, 1);
            return new Vector2(frame.HotX / (float)width, (height - frame.HotY) / (float)height);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RetailUiAssetResolver));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (UiResolvedAsset asset in _frames.Values)
            {
                Destroy(asset?.Sprite);
                Destroy(asset?.Texture);
            }
            _frames.Clear();
            _sources.Clear();
            _messageTables.Clear();
            _vfs.Dispose();
        }

        internal static void Destroy(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        private sealed class DecodedSource
        {
            public readonly string Path;
            public readonly ArtFile Art;

            public DecodedSource(string path, ArtFile art)
            {
                Path = path;
                Art = art;
            }
        }
    }

    /// <summary>Loads and validates optional local 4x UI replacements using the audit's source-ID convention.</summary>
    public sealed class EnhancedUiAssetResolver : IDisposable
    {
        public const int ReplacementScale = 4;

        private readonly string _root;
        private readonly Action<string> _diagnostic;
        private readonly Dictionary<UiAssetKey, UiResolvedAsset> _cache =
            new Dictionary<UiAssetKey, UiResolvedAsset>();
        private readonly HashSet<string> _rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public int CachedFrameCount => _cache.Count;
        public string Root => _root;

        public EnhancedUiAssetResolver(string root, Action<string> diagnostic = null)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentNullException(nameof(root));
            _root = Path.GetFullPath(root);
            _diagnostic = diagnostic ?? (message => Debug.LogWarning(message));
        }

        public static EnhancedUiAssetResolver CreateProduction(Action<string> diagnostic = null)
            => new EnhancedUiAssetResolver(
                Path.Combine(OpenArcanumHDAssetLoader.HDAssetsRoot, "ui", "by-source-id"),
                diagnostic);

        public string GetReplacementPath(UiResolvedAsset original)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            UiAssetKey key = original.Key;
            string directory = $"{key.SourceId:D4}-{RetailUiAssetResolver.SafeStem(original.SourcePath)}";
            string file = $"p{key.Palette:D2}-r{key.Rotation:D2}-f{key.Frame:D3}.png";
            return Path.GetFullPath(Path.Combine(_root, directory, file));
        }

        public bool TryResolve(UiResolvedAsset original, out UiResolvedAsset asset)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EnhancedUiAssetResolver));
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (_cache.TryGetValue(original.Key, out asset) && asset?.Sprite != null) return true;

            asset = null;
            string path = GetReplacementPath(original);
            if (_rejected.Contains(path)) return false;
            if (!File.Exists(path)) return Reject(path, $"replacement for {original.Key} is missing");

            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                {
                    name = $"UI_Enhanced_{original.Key}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!texture.LoadImage(File.ReadAllBytes(path), markNonReadable: false))
                    throw new InvalidDataException("PNG decoder rejected the file");

                int expectedWidth = original.LogicalSize.x * ReplacementScale;
                int expectedHeight = original.LogicalSize.y * ReplacementScale;
                if (texture.width != expectedWidth)
                    throw new InvalidDataException($"expected width {expectedWidth}, found {texture.width}");
                if (texture.height != expectedHeight)
                    throw new InvalidDataException($"expected height {expectedHeight}, found {texture.height}");
                if (original.HasTransparency && !texture.GetPixels32().Any(pixel => pixel.a < byte.MaxValue))
                    throw new InvalidDataException("source frame contains transparency but replacement is fully opaque");

                SourceUiScaleMetadata metadata = SourceUiScaleMetadataCatalog.Get(original.Key.SourceId);
                Vector4 border = metadata.Border * ReplacementScale;
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    original.Pivot,
                    pixelsPerUnit: ReplacementScale,
                    extrude: 0,
                    meshType: SpriteMeshType.FullRect,
                    border: border);
                sprite.name = $"UI_Enhanced_{original.Key}";

                asset = new UiResolvedAsset(
                    original.Key,
                    original.SourcePath,
                    texture,
                    sprite,
                    original.LogicalSize,
                    original.Hotspot,
                    original.Offset,
                    original.Pivot,
                    original.HasTransparency,
                    UiAssetSkin.Enhanced,
                    UiAssetSkin.Enhanced,
                    isFallback: false,
                    textureScale: ReplacementScale);
                _cache[original.Key] = asset;
                return true;
            }
            catch (Exception ex)
            {
                RetailUiAssetResolver.Destroy(sprite);
                RetailUiAssetResolver.Destroy(texture);
                return Reject(path, $"replacement for {original.Key} is invalid: {ex.Message}");
            }
        }

        private bool Reject(string path, string reason)
        {
            _rejected.Add(path);
            string message = $"OpenArcanum UI: {reason}; using Original retail frame. Path: '{path}'.";
            if (_reported.Add(message)) _diagnostic(message);
            return false;
        }

        public void ClearCache()
        {
            foreach (UiResolvedAsset asset in _cache.Values)
            {
                RetailUiAssetResolver.Destroy(asset?.Sprite);
                RetailUiAssetResolver.Destroy(asset?.Texture);
            }
            _cache.Clear();
            _rejected.Clear();
            _reported.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearCache();
        }
    }

    /// <summary>Presentation-only mode selector. It never owns controller, input, modal, or gameplay state.</summary>
    public sealed class UiSkinResolver : IDisposable
    {
        private readonly RetailUiAssetResolver _original;
        private readonly EnhancedUiAssetResolver _enhanced;
        private readonly bool _ownsResolvers;
        private readonly Dictionary<SkinKey, UiResolvedAsset> _cache =
            new Dictionary<SkinKey, UiResolvedAsset>();
        private bool _disposed;

        public event Action PresentationInvalidated;
        public int CachedResolutionCount => _cache.Count;
        public RetailUiAssetResolver Original => _original;
        public EnhancedUiAssetResolver Enhanced => _enhanced;

        public UiSkinResolver(
            RetailUiAssetResolver original,
            EnhancedUiAssetResolver enhanced,
            bool ownsResolvers = false)
        {
            _original = original ?? throw new ArgumentNullException(nameof(original));
            _enhanced = enhanced ?? throw new ArgumentNullException(nameof(enhanced));
            _ownsResolvers = ownsResolvers;
            OpenArcanumGraphicsSettings.ModeChanged += OnGraphicsModeChanged;
        }

        public static UiSkinResolver CreateProduction(Action<string> diagnostic = null)
            => new UiSkinResolver(
                RetailUiAssetResolver.CreateProduction(diagnostic),
                EnhancedUiAssetResolver.CreateProduction(diagnostic),
                ownsResolvers: true);

        public bool TryResolve(UiAssetKey key, out UiResolvedAsset asset)
            => TryResolve(
                key,
                OpenArcanumGraphicsSettings.Mode == GraphicsMode.Enhanced
                    ? UiAssetSkin.Enhanced
                    : UiAssetSkin.Original,
                out asset);

        public bool TryResolve(UiAssetKey key, UiAssetSkin requestedSkin, out UiResolvedAsset asset)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UiSkinResolver));
            var cacheKey = new SkinKey(key, requestedSkin);
            if (_cache.TryGetValue(cacheKey, out asset) && asset?.Sprite != null) return true;
            if (!_original.TryResolve(key, out UiResolvedAsset original))
            {
                asset = null;
                return false;
            }

            if (requestedSkin == UiAssetSkin.Enhanced)
                asset = _enhanced.TryResolve(original, out UiResolvedAsset enhanced)
                    ? enhanced
                    : original.AsFallback();
            else
                asset = original;

            _cache[cacheKey] = asset;
            return true;
        }

        public void InvalidatePresentation()
        {
            _cache.Clear();
            _enhanced.ClearCache();
            PresentationInvalidated?.Invoke();
        }

        private void OnGraphicsModeChanged(GraphicsMode _) => InvalidatePresentation();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OpenArcanumGraphicsSettings.ModeChanged -= OnGraphicsModeChanged;
            _cache.Clear();
            if (_ownsResolvers)
            {
                _enhanced.Dispose();
                _original.Dispose();
            }
        }

        private readonly struct SkinKey : IEquatable<SkinKey>
        {
            private readonly UiAssetKey _key;
            private readonly UiAssetSkin _skin;

            public SkinKey(UiAssetKey key, UiAssetSkin skin)
            {
                _key = key;
                _skin = skin;
            }

            public bool Equals(SkinKey other) => _key.Equals(other._key) && _skin == other._skin;
            public override bool Equals(object obj) => obj is SkinKey other && Equals(other);
            public override int GetHashCode() => (_key.GetHashCode() * 397) ^ (int)_skin;
        }
    }
}
