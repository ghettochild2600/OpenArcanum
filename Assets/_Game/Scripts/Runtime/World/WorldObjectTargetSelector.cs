using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Presentation-assisted hit testing that returns stable gameplay identity only.</summary>
    public static class WorldObjectTargetSelector
    {
        public static bool TrySelectPortal(IEnumerable<WorldObjectSpriteOwner> owners, Vector2 worldPoint,
            out ArcanumObjectId identity)
        {
            var objects = new List<WorldObject>();
            if (owners == null)
            {
                identity = default;
                return false;
            }
            foreach (WorldObjectSpriteOwner owner in owners)
                if (owner != null && owner.WorldObject != null) objects.Add(owner.WorldObject);
            return TrySelectPortal(objects, worldPoint, out identity);
        }

        public static bool TrySelectPortal(IEnumerable<WorldObject> objects, Vector2 worldPoint,
            out ArcanumObjectId identity)
        {
            identity = default;
            SpriteRenderer bestRenderer = null;
            string bestKey = null;
            if (objects == null) return false;

            foreach (WorldObject candidate in objects)
            {
                SpriteRenderer renderer = candidate?.View;
                if (candidate == null || candidate.Type != ObjectType.Portal || candidate.Off
                    || !candidate.Identity.IsPersistent || renderer == null || renderer.sprite == null
                    || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!ContainsVisiblePixel(renderer, worldPoint)) continue;

                string key = candidate.Identity.Key;
                if (bestRenderer != null && (renderer.sortingOrder < bestRenderer.sortingOrder
                    || renderer.sortingOrder == bestRenderer.sortingOrder
                    && string.CompareOrdinal(key, bestKey) >= 0))
                    continue;
                bestRenderer = renderer;
                bestKey = key;
                identity = candidate.Identity;
            }
            return bestRenderer != null;
        }

        private static bool ContainsVisiblePixel(SpriteRenderer renderer, Vector2 worldPoint)
        {
            Bounds bounds = renderer.bounds;
            if (worldPoint.x < bounds.min.x || worldPoint.x > bounds.max.x
                || worldPoint.y < bounds.min.y || worldPoint.y > bounds.max.y)
                return false;
            Sprite sprite = renderer.sprite;
            Texture2D texture = sprite.texture;
            if (texture == null || !texture.isReadable) return true;
            Vector3 local = renderer.transform.InverseTransformPoint(worldPoint);
            Bounds spriteBounds = sprite.bounds;
            if (spriteBounds.size.x <= 0f || spriteBounds.size.y <= 0f) return false;
            float u = Mathf.InverseLerp(spriteBounds.min.x, spriteBounds.max.x, local.x);
            float v = Mathf.InverseLerp(spriteBounds.min.y, spriteBounds.max.y, local.y);
            Rect rect = sprite.textureRect;
            int x = Mathf.Clamp(Mathf.FloorToInt(rect.x + u * rect.width),
                Mathf.FloorToInt(rect.x), Mathf.CeilToInt(rect.xMax) - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(rect.y + v * rect.height),
                Mathf.FloorToInt(rect.y), Mathf.CeilToInt(rect.yMax) - 1);
            return texture.GetPixel(x, y).a > 1f / 255f;
        }
    }
}
