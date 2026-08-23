using UnityEngine;

namespace OpenArcanum.Rendering
{
    public enum GraphicsMode
    {
        Original = 0,
        Enhanced = 1
    }

    [CreateAssetMenu(
        fileName = "OpenArcanumGraphicsConfig",
        menuName = "OpenArcanum/Graphics Config")]
    public sealed class OpenArcanumGraphicsConfig : ScriptableObject
    {
        [SerializeField]
        private GraphicsMode graphicsMode = GraphicsMode.Original;

        public GraphicsMode GraphicsMode => graphicsMode;
    }

    public static class OpenArcanumGraphicsSettings
    {
        private const string ResourceName = "OpenArcanumGraphicsConfig";

        private static OpenArcanumGraphicsConfig _config;

        public static GraphicsMode Mode
        {
            get
            {
                if (_config == null)
                    _config = Resources.Load<OpenArcanumGraphicsConfig>(ResourceName);

                return _config != null
                    ? _config.GraphicsMode
                    : GraphicsMode.Original;
            }
        }

        public static bool Enhanced => Mode == GraphicsMode.Enhanced;
    }
}
