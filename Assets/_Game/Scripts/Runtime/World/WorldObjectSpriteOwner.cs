using System;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Runtime.Art;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// Owns the visual sprites for one placed <see cref="WorldObject"/>. It retains the
    /// original ART identity so the presentation can be rebuilt without changing the
    /// object's gameplay transform or animation rate.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldObjectSpriteOwner : MonoBehaviour
    {
        private DatVirtualFileSystem _vfs;
        private Func<uint, string> _resolvePath;
        private WorldObject _worldObject;
        private SpriteRenderer _renderer;
        private SpriteFrameAnimator _animator;
        private Sprite[] _ownedSprites;
        private float _pixelsPerUnit;
        private bool _animate;

        public string OriginalAssetPath { get; private set; }
        public int RequestedRotation { get; private set; }
        public int SourceRotation { get; private set; }
        public int InitialFrameIndex { get; private set; }
        public bool MirrorX { get; private set; }
        public int FramesPerSecond { get; private set; }
        public int FrameCount => _ownedSprites?.Length ?? 0;
        public int CurrentFrameIndex => _animator != null ? _animator.CurrentFrame : InitialFrameIndex;
        public Sprite CurrentSprite => _renderer != null ? _renderer.sprite : null;
        public WorldObject WorldObject => _worldObject;
        public string LastBuildError { get; private set; }

        public void Initialize(
            DatVirtualFileSystem vfs,
            Func<uint, string> resolvePath,
            WorldObject worldObject,
            int objectOffsetX,
            int objectOffsetY,
            float pixelsPerUnit,
            bool animate)
        {
            _vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            _resolvePath = resolvePath ?? throw new ArgumentNullException(nameof(resolvePath));
            _worldObject = worldObject ?? throw new ArgumentNullException(nameof(worldObject));
            _pixelsPerUnit = pixelsPerUnit;
            _animate = animate;
            _renderer = GetComponent<SpriteRenderer>();

            // The visual child carries the engine's screen-space base (+40,+20) and authored
            // object offsets. The WorldObject root remains exactly on its gameplay tile.
            transform.localPosition = IsoProjection.ScreenOffset(
                40 + objectOffsetX,
                20 + objectOffsetY,
                pixelsPerUnit);

            _worldObject.View = _renderer;
            _worldObject.ReRender = ReRender;
            _worldObject.SetVisualFrame = TryShowVisualFrame;
            Rebuild();
        }

        /// <summary>Recreates this owner's frame array from retained ART identity.</summary>
        public bool Rebuild()
        {
            if (_worldObject == null || _vfs == null) return false;
            LastBuildError = null;

            OriginalAssetPath = _resolvePath(_worldObject.ArtId);
            if (string.IsNullOrEmpty(OriginalAssetPath) || !_vfs.Exists(OriginalAssetPath))
            {
                LastBuildError = $"ART source '{OriginalAssetPath ?? "<none>"}' was not found.";
                return false;
            }

            Sprite[] rebuilt = null;
            bool adopted = false;
            try
            {
                ArtFile art = ArtReader.Read(_vfs.ReadAllBytes(OriginalAssetPath));
                RequestedRotation = RotationOf(_worldObject.ArtId);
                SourceRotation = RequestedRotation;
                MirrorX = false;

                int artType = ArtId.Type(_worldObject.ArtId);
                if (UsesFacingMirror(artType) && RequestedRotation > 0 && RequestedRotation < 4)
                {
                    SourceRotation = 8 - RequestedRotation;
                    MirrorX = true;
                }

                bool idFlip = UsesArtIdFlip(artType) && (_worldObject.ArtId & 1u) != 0;
                if (idFlip) MirrorX = !MirrorX;

                if (art.Rotations.Count == 1) SourceRotation = 0;
                if (SourceRotation < 0 || SourceRotation >= art.Rotations.Count) return false;

                ArtFrame[] sourceFrames = art.Rotations[SourceRotation].Frames;
                if (sourceFrames == null || sourceFrames.Length == 0 || art.PrimaryPalette == null) return false;

                InitialFrameIndex = Mathf.Clamp(FrameOf(_worldObject.ArtId), 0, sourceFrames.Length - 1);
                FramesPerSecond = art.Fps;
                rebuilt = new Sprite[sourceFrames.Length];
                Vector2Int[] cumulativeOffsets = CumulativeFrameOffsets(sourceFrames, MirrorX);
                for (int frameIndex = 0; frameIndex < sourceFrames.Length; frameIndex++)
                {
                    ArtFrame frame = sourceFrames[frameIndex];
                    Vector2 pivot = ExactPivot(
                        frame,
                        artType,
                        SourceRotation,
                        UsesFacingMirror(artType) && RequestedRotation > 0 && RequestedRotation < 4,
                        idFlip,
                        cumulativeOffsets[frameIndex].x,
                        cumulativeOffsets[frameIndex].y);

                    rebuilt[frameIndex] = ArtTextureFactory.CreateSprite(
                        frame,
                        art.PrimaryPalette,
                        OriginalAssetPath,
                        SourceRotation,
                        frameIndex,
                        _pixelsPerUnit,
                        pivotOverride: pivot,
                        mirrorX: MirrorX);
                }

                Sprite[] previous = _ownedSprites;
                _ownedSprites = rebuilt;
                adopted = true;
                if (_worldObject.IsDead)
                {
                    int corpseFrame = rebuilt.Length - 1;
                    _animator ??= GetComponent<SpriteFrameAnimator>() ?? gameObject.AddComponent<SpriteFrameAnimator>();
                    _animator.ShowStatic(rebuilt[corpseFrame], corpseFrame);
                }
                else if (_animate && rebuilt.Length > 1)
                {
                    _animator ??= GetComponent<SpriteFrameAnimator>() ?? gameObject.AddComponent<SpriteFrameAnimator>();
                    if (previous == null) _animator.Init(rebuilt, art.Fps, InitialFrameIndex);
                    else _animator.RebuildLoop(rebuilt, art.Fps);
                }
                else
                {
                    if (_animator != null) _animator.ShowStatic(rebuilt[InitialFrameIndex], InitialFrameIndex);
                    else _renderer.sprite = rebuilt[InitialFrameIndex];
                }

                DestroySprites(previous);
                return true;
            }
            catch (Exception ex)
            {
                if (!adopted) DestroySprites(rebuilt);
                LastBuildError = ex.Message;
                Debug.LogWarning(
                    $"WorldObjectSpriteOwner: could not build '{OriginalAssetPath}' for " +
                    $"0x{_worldObject.ArtId:X8}: {ex.Message}", this);
                return false;
            }
        }

        private bool TryShowVisualFrame(int frameIndex)
        {
            if (_worldObject == null
                || ArtId.Type(_worldObject.ArtId) != ArtId.TypePortal
                || _ownedSprites == null
                || frameIndex < 0
                || frameIndex >= _ownedSprites.Length)
                return false;

            InitialFrameIndex = frameIndex;
            if (_animator != null) _animator.ShowStatic(_ownedSprites[frameIndex], frameIndex);
            else if (_renderer != null) _renderer.sprite = _ownedSprites[frameIndex];
            return true;
        }

        private Sprite ReRender(WorldObject worldObject)
        {
            Rebuild();
            return CurrentSprite;
        }

        private static int RotationOf(uint artId)
        {
            switch (ArtId.Type(artId))
            {
                case ArtId.TypeTile:
                case ArtId.TypeInterface:
                case ArtId.TypeMisc:
                case ArtId.TypeRoof:
                case ArtId.TypeItem:
                case ArtId.TypeFacade:
                    return 0;
                case ArtId.TypeLight:
                case ArtId.TypeEyeCandy:
                    return (int)((artId >> 9) & 7);
                default:
                    return (int)((artId >> 11) & 7);
            }
        }

        private static int FrameOf(uint artId)
        {
            switch (ArtId.Type(artId))
            {
                case ArtId.TypeTile:
                case ArtId.TypeWall:
                case ArtId.TypeItem:
                    return 0;
                case ArtId.TypeInterface:
                case ArtId.TypeMisc:
                    return (int)((artId >> 8) & 0xFF);
                case ArtId.TypeFacade:
                    return (int)((artId >> 1) & 0x3FF);
                case ArtId.TypeLight:
                case ArtId.TypeEyeCandy:
                    return (int)((artId >> 12) & 0x7F);
                default:
                    return (int)((artId >> 14) & 0x1F);
            }
        }

        private static bool UsesFacingMirror(int artType)
            => artType == ArtId.TypeCritter || artType == ArtId.TypeMonster || artType == ArtId.TypeUniqueNpc;

        private static bool UsesArtIdFlip(int artType)
            => artType == ArtId.TypeWall || artType == ArtId.TypePortal || artType == ArtId.TypeRoof;

        internal static Vector2Int[] CumulativeFrameOffsets(ArtFrame[] frames, bool mirrorX)
        {
            if (frames == null || frames.Length == 0) return Array.Empty<Vector2Int>();
            var result = new Vector2Int[frames.Length];
            for (int frameIndex = 1; frameIndex < frames.Length; frameIndex++)
            {
                ArtFrame frame = frames[frameIndex];
                result[frameIndex] = result[frameIndex - 1]
                    + new Vector2Int(mirrorX ? -frame.OffsetX : frame.OffsetX, frame.OffsetY);
            }
            return result;
        }

        // Exact tig_art_frame_data hotspot transforms. Supplying this as a pivot keeps
        // mirror semantics independent of SpriteRenderer.flipX and preserves the anchor.
        internal static Vector2 ExactPivot(
            ArtFrame frame,
            int artType,
            int sourceRotation,
            bool facingMirror,
            bool idFlip,
            int cumulativeOffsetX = 0,
            int cumulativeOffsetY = 0)
        {
            int hotX = frame.HotX;
            int hotY = frame.HotY;

            if ((artType == ArtId.TypeWall || artType == ArtId.TypePortal)
                && (sourceRotation < 2 || sourceRotation > 5))
            {
                hotX -= 40;
                hotY += 20;
            }

            if (facingMirror) hotX = frame.Width - hotX - 1;

            if (idFlip)
            {
                hotX = artType == ArtId.TypeRoof
                    ? 0
                    : frame.Width - hotX - 2;
            }

            // Arcanum accumulates each newly displayed frame's authored offset into the
            // object's presentation offset. Baking that cumulative delta into the pivot
            // preserves the original registration without moving the gameplay transform.
            hotX -= cumulativeOffsetX;
            hotY -= cumulativeOffsetY;

            return new Vector2(
                frame.Width > 0 ? hotX / (float)frame.Width : 0.5f,
                frame.Height > 0 ? (frame.Height - hotY) / (float)frame.Height : 0.5f);
        }

        private static void DestroySprites(Sprite[] sprites)
        {
            if (sprites == null) return;
            foreach (Sprite sprite in sprites)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                bool ownsTexture = texture != null && texture.name == "ArtFrame";
                DestroyOwned(sprite);
                if (ownsTexture) DestroyOwned(texture);
            }
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }

        private void OnDestroy()
        {
            if (_worldObject != null && _worldObject.ReRender != null)
                _worldObject.ReRender = null;
            if (_worldObject != null && _worldObject.SetVisualFrame != null)
                _worldObject.SetVisualFrame = null;
            DestroySprites(_ownedSprites);
            _ownedSprites = null;
        }
    }
}
