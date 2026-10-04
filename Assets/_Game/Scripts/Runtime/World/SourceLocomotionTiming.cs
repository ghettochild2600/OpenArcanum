using Arcanum.Formats.Art;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Source WALK timing and ART-offset projection from anim.c <c>sub_4305D0</c>/<c>sub_437990</c>.</summary>
    public static class SourceLocomotionTiming
    {
        public static int AdjustedWalkFramesPerSecond(int sourceFps, int speed, uint artId)
        {
            int fps = sourceFps > 0 ? sourceFps : 10;
            int bodyAdjustment = ArtId.Type(artId) == ArtId.TypeCritter
                                 && ((artId >> 24) & 7u) is 1u or 2u ? 4 : 0;
            int normal = 17 + bodyAdjustment;
            int low = 6 + bodyAdjustment;
            int high = 30 + bodyAdjustment;
            speed = Mathf.Max(0, speed);
            if (speed < 8) return low + speed * (normal - low) / 8;
            if (speed > 8) return speed < 30
                ? normal + speed * (high - normal) / 30
                : high;
            return normal;
        }

        /// <summary>Projects one effective engine screen-space frame delta onto its requested tile direction.</summary>
        public static float TileProgress(int offsetX, int offsetY, int facing)
        {
            // Differential inverse of location_xy: sx=40*(dy-dx), sy=20*(dy+dx).
            float dx = (offsetY / IsoProjection.HalfHeight - offsetX / IsoProjection.HalfWidth) * .5f;
            float dy = (offsetY / IsoProjection.HalfHeight + offsetX / IsoProjection.HalfWidth) * .5f;
            Vector2 direction = IsoProjection.DirDelta[facing & 7];
            float denominator = Vector2.Dot(direction, direction);
            return denominator > 0f ? Mathf.Max(0f, Vector2.Dot(new Vector2(dx, dy), direction) / denominator) : 0f;
        }
    }
}
