using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.Art;
using Arcanum.Runtime.World;
using UnityEngine;
using OpenArcanum.Rendering;

namespace Arcanum.World
{
    /// <summary>
    /// Encapsulated terrain/tile generator: turns a sector's terrain grid (<see cref="SectorTerrain"/>) into
    /// rendered Unity geometry, either as a single batched mesh (one draw call per sector) or, as a fallback,
    /// one <see cref="SpriteRenderer"/> per tile.
    /// <para>
    /// Extracted from the game's main world controller so the exact in-game terrain generation can be reused by
    /// standalone test scenes and shipped as a self-contained slice. Depends only on <c>Arcanum.Formats</c>
    /// (pure readers) plus the engine-art primitives in this assembly — no gameplay code.
    /// </para>
    /// </summary>
    public sealed class TileMapRenderer
    {
        private const int TerrainAtlasSize = 2048;

        private readonly DatVirtualFileSystem _vfs;
        private readonly TileArtPathResolver _tileResolver;
        private readonly FacadeArtResolver _facades;
        private readonly float _pixelsPerUnit;

        private readonly Dictionary<string, Sprite> _spriteCache =
            new Dictionary<string, Sprite>();

        private readonly Dictionary<string, ArtFile> _artCache =
            new Dictionary<string, ArtFile>();

        private readonly Dictionary<(string, int), Sprite> _facadeSpriteCache =
            new Dictionary<(string, int), Sprite>();

        private readonly HashSet<string> _blendMissSamples =
            new HashSet<string>();

        private int _blendMisses;

        /// <param name="vfs">Mounted archive(s) holding the tile + facade art.</param>
        /// <param name="tileResolver">Tile art-id → path resolver (from <c>art/tile/tilename.mes</c>).</param>
        /// <param name="facades">Optional facade resolver (from <c>art/facade/facadename.mes</c>); may be null.</param>
        /// <param name="pixelsPerUnit">Sprite PPU (the engine art is 100 px/unit).</param>
        public TileMapRenderer(
            DatVirtualFileSystem vfs,
            TileArtPathResolver tileResolver,
            FacadeArtResolver facades,
            float pixelsPerUnit)
        {
            _vfs = vfs;
            _tileResolver = tileResolver;
            _facades = facades;
            _pixelsPerUnit = pixelsPerUnit;
        }

        /// <summary>
        /// Places one terrain tile in the per-tile fallback path.
        /// Lets the host own depth/Z ordering.
        /// </summary>
        public delegate SpriteRenderer PlaceTileDelegate(
            string name,
            Transform parent,
            Sprite sprite,
            Vector3 localPos,
            int sortingOrder);

        /// <summary>
        /// Blend tiles whose every variant was missing (fell back to the base tile).
        /// Diagnostic only.
        /// </summary>
        public int BlendMisses => _blendMisses;

        /// <summary>
        /// A capped sample of the missing blends
        /// (<c>"name1+name2 e&lt;edge&gt;"</c>), for logging.
        /// </summary>
        public IReadOnlyCollection<string> BlendMissSamples => _blendMissSamples;

        /// <summary>
        /// Renders one sector's terrain under <paramref name="root"/>.
        ///
        /// When <paramref name="batch"/> is true (the default in-game path)
        /// this emits a single packed-atlas mesh.
        ///
        /// Otherwise it places one sprite per tile via <paramref name="placeTile"/>.
        ///
        /// <paramref name="offX"/> and <paramref name="offY"/> are the
        /// sector's global tile origin.
        /// </summary>
        public void RenderSector(
            SectorTerrain terrain,
            int offX,
            int offY,
            Transform root,
            Material baseMaterial,
            int sortingOrder,
            bool batch,
            PlaceTileDelegate placeTile = null)
        {
            // Terrain tiles must NOT use the shared sprite atlas.
            //
            // The batched path packs each tile's own texture into its mesh atlas
            // (Texture2D.PackTextures). A tile sprite whose texture is already a
            // shared atlas page would therefore produce incorrect UVs.
            //
            // Temporarily disable the shared atlas while terrain is generated,
            // then restore it afterward.
            RuntimeSpriteAtlas prevAtlas = ArtTextureFactory.ActiveAtlas;
            ArtTextureFactory.ActiveAtlas = null;

            try
            {
                if (batch)
                {
                    BuildTerrainMesh(
                        terrain,
                        offX,
                        offY,
                        root,
                        baseMaterial,
                        sortingOrder);
                }
                else
                {
                    BuildTerrainTiles(
                        terrain,
                        offX,
                        offY,
                        root,
                        placeTile);
                }
            }
            finally
            {
                ArtTextureFactory.ActiveAtlas = prevAtlas;
            }
        }

        /// <summary>
        /// Fallback renderer:
        /// one GameObject + SpriteRenderer per terrain tile.
        ///
        /// This creates 4096 objects per sector and is therefore much heavier
        /// than the batched terrain renderer.
        /// </summary>
        private void BuildTerrainTiles(
            SectorTerrain terrain,
            int offX,
            int offY,
            Transform root,
            PlaceTileDelegate placeTile)
        {
            if (placeTile == null)
            {
                Debug.LogWarning(
                    "TileMapRenderer: non-batched terrain needs a placeTile delegate; nothing drawn.");

                return;
            }

            Sprite filler = GetTileSprite(MostCommonArtId(terrain));
            Transform parent = NewChild("Tiles", root);

            for (int y = 0; y < SectorTerrain.Size; y++)
            {
                for (int x = 0; x < SectorTerrain.Size; x++)
                {
                    Sprite sprite =
                        GetTileSprite(terrain.At(x, y)) ?? filler;

                    if (!sprite)
                        continue;

                    int gx = offX + x;
                    int gy = offY + y;

                    placeTile(
                        $"Tile_{x}_{y}",
                        parent,
                        sprite,
                        IsoProjection.TileToWorld(
                            gx,
                            gy,
                            _pixelsPerUnit),
                        (gx + gy) * 2);
                }
            }
        }

        /// <summary>
        /// Batched terrain renderer.
        ///
        /// Packs the sector's distinct terrain textures into one atlas and
        /// emits one mesh containing 4096 tile quads.
        ///
        /// This substantially reduces draw calls compared with the fallback
        /// SpriteRenderer-per-tile path.
        /// </summary>
        private void BuildTerrainMesh(
            SectorTerrain terrain,
            int offX,
            int offY,
            Transform root,
            Material baseMaterial,
            int sortingOrder)
        {
            const int size = SectorTerrain.Size;

            Sprite filler = GetTileSprite(
                MostCommonArtId(terrain));

            var distinct = new List<Texture2D>();
            var indexByTex = new Dictionary<Texture2D, int>();

            int[] tileTex = new int[size * size];

            // Mirrored blend tiles reuse canonical artwork with horizontally
            // flipped UV coordinates.
            bool[] tileFlip = new bool[size * size];

            Sprite sample = null;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    uint aid = terrain.At(x, y);

                    Sprite own = GetTileSprite(aid);
                    Sprite sp = own ?? filler;

                    Texture2D tex = sp ? sp.texture : null;

                    if (!tex)
                    {
                        tileTex[y * size + x] = -1;
                        continue;
                    }

                    sample ??= sp;

                    if (!indexByTex.TryGetValue(tex, out int idx))
                    {
                        idx = distinct.Count;

                        indexByTex[tex] = idx;
                        distinct.Add(tex);
                    }

                    tileTex[y * size + x] = idx;

                    tileFlip[y * size + x] =
                        own && TileArtId.IsMirrored(aid);
                }
            }

            if (!sample)
                return;

            // ------------------------------------------------------------
            // OPENARCANUM TERRAIN TEXTURE POLICY
            // ------------------------------------------------------------
            //
            // Terrain is handled differently from normal ART sprites.
            //
            // Enhanced mode may use bilinear filtering, but terrain mipmaps
            // are intentionally disabled because packed atlas mip levels can
            // bleed neighboring tile colors across tile boundaries and create
            // a visible grid.
            //
            // Original:
            //   Point filtering
            //   No mipmaps
            //
            // Enhanced:
            //   Bilinear filtering
            //   No mipmaps
            //
            // If seams remain with mipmaps disabled, the next step will be
            // UV inset / atlas-edge padding rather than reverting Enhanced
            // mode back to point filtering.
            // ------------------------------------------------------------

            bool useMipMaps = false;

            var atlas = new Texture2D(
                TerrainAtlasSize,
                TerrainAtlasSize,
                TextureFormat.RGBA32,
                mipChain: useMipMaps)
            {
                filterMode =
                    OpenArcanumTexturePolicy.TextureFilterMode,

                wrapMode =
                    TextureWrapMode.Clamp,

                name =
                    "TerrainAtlas"
            };

            Rect[] rects = atlas.PackTextures(
                distinct.ToArray(),
                2,
                TerrainAtlasSize,
                makeNoLongerReadable: true);

            // Quad footprint and pivot from a sample tile.
            //
            // All terrain tiles share the original Arcanum 78×40
            // isometric tile geometry.
            float w =
                sample.rect.width / _pixelsPerUnit;

            float h =
                sample.rect.height / _pixelsPerUnit;

            float px =
                sample.pivot.x / sample.rect.width;

            float py =
                sample.pivot.y / sample.rect.height;

            // Sort tiles back-to-front using the original isometric depth.
            var order = new List<int>(size * size);

            for (int i = 0; i < size * size; i++)
            {
                if (tileTex[i] >= 0)
                    order.Add(i);
            }

            order.Sort(
                (a, b) =>
                    ((a % size) + (a / size))
                    .CompareTo(
                        (b % size) + (b / size)));

            var verts =
                new List<Vector3>(order.Count * 4);

            var uvs =
                new List<Vector2>(order.Count * 4);

            var tris =
                new List<int>(order.Count * 6);

            foreach (int i in order)
            {
                int x = i % size;
                int y = i / size;

                Vector3 pos =
                    IsoProjection.TileToWorld(
                        offX + x,
                        offY + y,
                        _pixelsPerUnit);

                Vector3 bl =
                    pos + new Vector3(
                        -px * w,
                        -py * h,
                        0f);

                int b = verts.Count;

                verts.Add(bl);

                verts.Add(
                    bl + new Vector3(
                        w,
                        0f,
                        0f));

                verts.Add(
                    bl + new Vector3(
                        0f,
                        h,
                        0f));

                verts.Add(
                    bl + new Vector3(
                        w,
                        h,
                        0f));

                Rect r = rects[tileTex[i]];

                // ------------------------------------------------------------
                // OPENARCANUM TERRAIN UV INSET
                // ------------------------------------------------------------
                //
                // Bilinear filtering samples neighboring texels around the
                // requested UV coordinate.
                //
                // The terrain atlas contains many independent Arcanum tiles.
                // Sampling exactly on a packed tile's UV boundary can therefore
                // blend with atlas padding or a neighboring tile, producing the
                // visible rectangular tile grid.
                //
                // Move each UV boundary inward by half a texel so the outermost
                // sample lands at the center of the tile's actual edge pixels.
                //
                // Original point-filtered rendering does not require this, but
                // the inset is harmless there as well.
                // ------------------------------------------------------------

                // ------------------------------------------------------------
                // OPENARCANUM TERRAIN UV INSET
                // ------------------------------------------------------------
                //
                // Bilinear sampling can reach outside the packed texture's
                // allocated rectangle and blend with atlas padding or neighboring
                // terrain textures.
                //
                // A half-texel inset removed most seams. A full texel gives us
                // additional protection at high-resolution output and fractional
                // camera scaling.
                //
                // This is still an interim solution. The long-term renderer will
                // use edge-extruded atlas entries so filtering can remain smooth
                // without sacrificing texture-edge coverage.
                // ------------------------------------------------------------

                float texelU = 1.0f / atlas.width;
                float texelV = 1.0f / atlas.height;

                float rMinX = r.xMin + texelU;
                float rMaxX = r.xMax - texelU;
                float rMinY = r.yMin + texelV;
                float rMaxY = r.yMax - texelV;

                // Mirrored tiles reuse the canonical artwork with the U
                // coordinates reversed.
                float u0 =
                    tileFlip[i]
                        ? rMaxX
                        : rMinX;

                float u1 =
                    tileFlip[i]
                        ? rMinX
                        : rMaxX;

                uvs.Add(
                    new Vector2(
                        u0,
                        rMinY));

                uvs.Add(
                    new Vector2(
                        u1,
                        rMinY));

                uvs.Add(
                    new Vector2(
                        u0,
                        rMaxY));

                uvs.Add(
                    new Vector2(
                        u1,
                        rMaxY));

                tris.Add(b);
                tris.Add(b + 2);
                tris.Add(b + 3);

                tris.Add(b);
                tris.Add(b + 3);
                tris.Add(b + 1);
            }

            var mesh =
                new Mesh
                {
                    name = "TerrainMesh"
                };

            if (verts.Count > 65000)
            {
                mesh.indexFormat =
                    UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);

            // White vertex colours.
            //
            // The 2D lit sprite shader multiplies texture colour by
            // vertex colour. A bare mesh otherwise defaults to black.
            var cols =
                new Color32[verts.Count];

            for (int i = 0; i < cols.Length; i++)
            {
                cols[i] =
                    new Color32(
                        255,
                        255,
                        255,
                        255);
            }

            mesh.SetColors(cols);
            mesh.RecalculateBounds();

            var go =
                new GameObject(
                    "TerrainMesh");

            go.transform.SetParent(
                root,
                false);

            go.AddComponent<MeshFilter>()
                .sharedMesh = mesh;

            MeshRenderer mr =
                go.AddComponent<MeshRenderer>();

            Material terrainBase =
                baseMaterial ??
                new Material(
                    Shader.Find("Sprites/Default"));

            mr.sharedMaterial =
                new Material(terrainBase)
                {
                    mainTexture = atlas
                };

            mr.sortingOrder =
                sortingOrder;

            // These stay disabled for now.
            //
            // We will handle lighting and shadows as a separate
            // OpenArcanum rendering milestone rather than changing
            // several graphics systems simultaneously.
            mr.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            mr.receiveShadows =
                false;
        }

        /// <summary>
        /// Resolves a terrain tile art-id to its sprite.
        ///
        /// Facade tiles (large buildings, TIG type 11) decode to their
        /// facade artwork. Everything else routes through the normal
        /// tile blend/variant resolver with a base fallback.
        /// </summary>
        private Sprite GetTileSprite(uint artId)
        {
            if (ArtId.Type(artId) == ArtId.TypeFacade)
            {
                return GetFacadeSprite(artId);
            }

            string path =
                _tileResolver.ResolveExisting(
                    artId,
                    _vfs.Exists,
                    OnTileBlendMissing);

            if (path == null ||
                !_vfs.Exists(path))
            {
                path =
                    _tileResolver.BaseFallback(
                        artId);
            }

            if (path == null ||
                !_vfs.Exists(path))
            {
                return null;
            }

            return GetSprite(path);
        }

        /// <summary>
        /// Records blend combinations for which every expected variant
        /// was unavailable.
        /// </summary>
        private void OnTileBlendMissing(
            string name1,
            string name2,
            int edge)
        {
            _blendMisses++;

            if (_blendMissSamples.Count < 24)
            {
                _blendMissSamples.Add(
                    $"{name1}+{name2} e{edge}");
            }
        }

        /// <summary>
        /// Resolves the 78×40 facade frame for a facade terrain tile.
        /// </summary>
        private Sprite GetFacadeSprite(
            uint artId)
        {
            string path =
                _facades?.Resolve(artId);

            if (path == null ||
                !_vfs.Exists(path))
            {
                return null;
            }

            int frame =
                FacadeArtResolver.Frame(
                    artId);

            if (_facadeSpriteCache.TryGetValue(
                    (path, frame),
                    out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;

            try
            {
                if (!_artCache.TryGetValue(
                        path,
                        out ArtFile art))
                {
                    art =
                        ArtReader.Read(
                            _vfs.ReadAllBytes(
                                path));

                    _artCache[path] =
                        art;
                }

                ArtFrame[] frames =
                    art.Rotations[0].Frames;

                ArtFrame f =
                    frames[
                        Mathf.Clamp(
                            frame,
                            0,
                            frames.Length - 1)];

                sprite =
                    ArtTextureFactory.CreateSprite(
                        f,
                        art.PrimaryPalette,
                        _pixelsPerUnit);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"facade decode failed '{path}' frame {frame}: {ex.Message}");
            }

            _facadeSpriteCache[
                (path, frame)] = sprite;

            return sprite;
        }

        /// <summary>
        /// Loads and caches a standard ART sprite.
        /// </summary>
        private Sprite GetSprite(
            string path)
        {
            if (_spriteCache.TryGetValue(
                    path,
                    out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;

            try
            {
                ArtFile art =
                    ArtReader.Read(
                        _vfs.ReadAllBytes(
                            path));

                ArtFrame frame =
                    art.Rotations[0]
                        .Frames[0];

                sprite =
                    ArtTextureFactory.CreateSprite(
                        frame,
                        art.PrimaryPalette,
                        _pixelsPerUnit);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"decode failed '{path}': {ex.Message}");
            }

            _spriteCache[path] =
                sprite;

            return sprite;
        }

        /// <summary>
        /// Creates a child transform under the supplied parent.
        /// </summary>
        private static Transform NewChild(
            string name,
            Transform parent)
        {
            Transform t =
                new GameObject(name)
                    .transform;

            t.SetParent(
                parent,
                false);

            return t;
        }

        /// <summary>
        /// Returns the most frequently used terrain art ID in the sector.
        /// Used as a fallback if a specific terrain tile cannot be decoded.
        /// </summary>
        private static uint MostCommonArtId(
            SectorTerrain terrain)
        {
            var counts =
                new Dictionary<uint, int>();

            foreach (uint id in terrain.TileArtIds)
            {
                counts[id] =
                    counts.TryGetValue(
                        id,
                        out int c)
                        ? c + 1
                        : 1;
            }

            uint best = 0;
            int bestCount = -1;

            foreach (
                KeyValuePair<uint, int> kvp
                in counts)
            {
                if (kvp.Value > bestCount)
                {
                    bestCount =
                        kvp.Value;

                    best =
                        kvp.Key;
                }
            }

            return best;
        }
    }
}