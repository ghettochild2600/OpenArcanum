using UnityEngine;

namespace OpenArcanum.Rendering
{
    public static class OpenArcanumTexturePolicy
    {
        public static FilterMode TextureFilterMode =>
            OpenArcanumGraphicsSettings.Enhanced
                ? FilterMode.Bilinear
                : FilterMode.Point;

        public static bool UseMipMaps =>
            OpenArcanumGraphicsSettings.Enhanced;
    }
}
