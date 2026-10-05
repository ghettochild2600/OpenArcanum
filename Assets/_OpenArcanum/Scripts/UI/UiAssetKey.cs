using System;
using UnityEngine;

namespace OpenArcanum.UI
{
    /// <summary>
    /// Stable retail interface identity. Filenames are derived metadata; the numeric source identity and exact
    /// palette/rotation/frame tuple are authoritative.
    /// </summary>
    [Serializable]
    public readonly struct UiAssetKey : IEquatable<UiAssetKey>
    {
        public int SourceId { get; }
        public int Palette { get; }
        public int Rotation { get; }
        public int Frame { get; }

        public UiAssetKey(int sourceId, int palette = 0, int rotation = 0, int frame = 0)
        {
            SourceId = sourceId;
            Palette = palette;
            Rotation = rotation;
            Frame = frame;
        }

        public bool Equals(UiAssetKey other)
            => SourceId == other.SourceId
               && Palette == other.Palette
               && Rotation == other.Rotation
               && Frame == other.Frame;

        public override bool Equals(object obj) => obj is UiAssetKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceId;
                hash = (hash * 397) ^ Palette;
                hash = (hash * 397) ^ Rotation;
                hash = (hash * 397) ^ Frame;
                return hash;
            }
        }

        public override string ToString()
            => $"ui:{SourceId:D4}:p{Palette:D2}:r{Rotation:D2}:f{Frame:D3}";

        public static bool operator ==(UiAssetKey left, UiAssetKey right) => left.Equals(right);
        public static bool operator !=(UiAssetKey left, UiAssetKey right) => !left.Equals(right);
    }

    /// <summary>Serializable inspector representation which converts to the immutable runtime key.</summary>
    [Serializable]
    public struct UiAssetReference
    {
        [SerializeField] private int sourceId;
        [SerializeField] private int palette;
        [SerializeField] private int rotation;
        [SerializeField] private int frame;

        public UiAssetKey Key => new UiAssetKey(sourceId, palette, rotation, frame);

        public UiAssetReference(UiAssetKey key)
        {
            sourceId = key.SourceId;
            palette = key.Palette;
            rotation = key.Rotation;
            frame = key.Frame;
        }
    }

    public enum UiAssetSkin
    {
        Original,
        Enhanced,
    }

    public enum SourceUiScalePolicy
    {
        Fixed,
        HorizontalSlice,
        NineSlice,
        TileAssembly,
    }

    public readonly struct SourceUiScaleMetadata
    {
        public SourceUiScalePolicy Policy { get; }
        public Vector4 Border { get; }
        public Vector2 MinimumLogicalSize { get; }
        public bool IsRepeatingTile { get; }

        public SourceUiScaleMetadata(
            SourceUiScalePolicy policy,
            Vector4 border,
            Vector2 minimumLogicalSize,
            bool isRepeatingTile = false)
        {
            Policy = policy;
            Border = border;
            MinimumLogicalSize = minimumLogicalSize;
            IsRepeatingTile = isRepeatingTile;
        }
    }

    /// <summary>Only source-proven scalable assets appear here. Every other identity is fixed-size.</summary>
    public static class SourceUiScaleMetadataCatalog
    {
        private static readonly SourceUiScaleMetadata Fixed = new SourceUiScaleMetadata(
            SourceUiScalePolicy.Fixed, Vector4.zero, Vector2.zero);

        public static SourceUiScaleMetadata Get(int sourceId)
        {
            switch (sourceId)
            {
                case 354:
                    return new SourceUiScaleMetadata(
                        SourceUiScalePolicy.NineSlice,
                        new Vector4(8f, 8f, 8f, 8f),
                        new Vector2(16f, 16f));
                case 822:
                    return new SourceUiScaleMetadata(
                        SourceUiScalePolicy.HorizontalSlice,
                        new Vector4(16f, 0f, 16f, 0f),
                        new Vector2(32f, 136f));
                case 238:
                case 240:
                    return new SourceUiScaleMetadata(
                        SourceUiScalePolicy.TileAssembly,
                        Vector4.zero,
                        new Vector2(11f, sourceId == 238 ? 5f : 7f));
                case 787:
                    return new SourceUiScaleMetadata(
                        SourceUiScalePolicy.TileAssembly,
                        Vector4.zero,
                        new Vector2(11f, 1f),
                        isRepeatingTile: true);
                default:
                    return Fixed;
            }
        }
    }

    /// <summary>One resolved presentation frame plus its unchanged source-space geometry.</summary>
    public sealed class UiResolvedAsset
    {
        public UiAssetKey Key { get; }
        public string SourcePath { get; }
        public Texture2D Texture { get; }
        public Sprite Sprite { get; }
        public Vector2Int LogicalSize { get; }
        public Vector2Int Hotspot { get; }
        public Vector2Int Offset { get; }
        public Vector2 Pivot { get; }
        public bool HasTransparency { get; }
        public UiAssetSkin RequestedSkin { get; }
        public UiAssetSkin ResolvedSkin { get; }
        public bool IsFallback { get; }
        public int TextureScale { get; }

        internal UiResolvedAsset(
            UiAssetKey key,
            string sourcePath,
            Texture2D texture,
            Sprite sprite,
            Vector2Int logicalSize,
            Vector2Int hotspot,
            Vector2Int offset,
            Vector2 pivot,
            bool hasTransparency,
            UiAssetSkin requestedSkin,
            UiAssetSkin resolvedSkin,
            bool isFallback,
            int textureScale)
        {
            Key = key;
            SourcePath = sourcePath;
            Texture = texture;
            Sprite = sprite;
            LogicalSize = logicalSize;
            Hotspot = hotspot;
            Offset = offset;
            Pivot = pivot;
            HasTransparency = hasTransparency;
            RequestedSkin = requestedSkin;
            ResolvedSkin = resolvedSkin;
            IsFallback = isFallback;
            TextureScale = textureScale;
        }

        internal UiResolvedAsset AsFallback()
            => new UiResolvedAsset(
                Key, SourcePath, Texture, Sprite, LogicalSize, Hotspot, Offset, Pivot, HasTransparency,
                UiAssetSkin.Enhanced, UiAssetSkin.Original, isFallback: true, textureScale: TextureScale);
    }
}
