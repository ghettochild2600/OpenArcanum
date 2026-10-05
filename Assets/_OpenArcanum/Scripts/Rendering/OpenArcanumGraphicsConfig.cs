using System;
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

        private static GraphicsMode? _runtimeMode;

        /// <summary>
        /// Presentation owners may rebind visual resources after a runtime mode switch. Gameplay systems must not
        /// subscribe to or derive authority from this event.
        /// </summary>
        public static event Action<GraphicsMode> ModeChanged;

        public static GraphicsMode Mode
        {
            get
            {
                if (_runtimeMode.HasValue)
                    return _runtimeMode.Value;

                if (_config == null)
                    _config = Resources.Load<OpenArcanumGraphicsConfig>(ResourceName);

                return _config != null
                    ? _config.GraphicsMode
                    : GraphicsMode.Original;
            }
        }

        public static bool Enhanced => Mode == GraphicsMode.Enhanced;

        /// <summary>
        /// Overrides the configured mode for the current runtime session. Presentation owners
        /// still decide when to rebuild their sprites; this only changes subsequent asset creation.
        /// </summary>
        public static void SetRuntimeMode(GraphicsMode mode)
        {
            if (_runtimeMode == mode) return;
            _runtimeMode = mode;
            OpenArcanumHDAssetLoader.ClearCache();
            ModeChanged?.Invoke(mode);
        }

        public static void ClearRuntimeMode()
        {
            if (!_runtimeMode.HasValue) return;
            _runtimeMode = null;
            OpenArcanumHDAssetLoader.ClearCache();
            ModeChanged?.Invoke(Mode);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeMode()
        {
            _runtimeMode = null;
            _config = null;
            ModeChanged = null;
        }
    }
}
