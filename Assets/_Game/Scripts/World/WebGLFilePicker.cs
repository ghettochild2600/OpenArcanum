using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Arcanum.World
{
    /// <summary>
    /// Opens the browser's native file picker (WebGL only) so the player can supply their own legitimate
    /// Arcanum <c>.dat</c> files, and writes them into the emscripten virtual filesystem under
    /// <c>/arcanum/&lt;name&gt;</c> — where <see cref="Arcanum.Formats.Database.DatVirtualFileSystem.MountFile"/>
    /// can read them like real files. Reports the written filenames back via the callbacks. Bundles nothing;
    /// the data never leaves the player's browser.
    ///
    /// <para>Add this component to a GameObject and call <see cref="Pick"/> from a UI button. On non-WebGL
    /// platforms it's a no-op (the desktop build reads the install directly), so the same scene runs everywhere.</para>
    /// </summary>
    public sealed class WebGLFilePicker : MonoBehaviour
    {
        /// <summary>The emscripten FS directory the picker writes uploads into (matches the .jslib).</summary>
        public const string DataDir = "/arcanum";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ArcanumPickFiles(string goName, string cbOk, string cbErr);
#endif

        private Action<string[]> _onPicked;
        private Action<string> _onError;

        /// <summary>Whether a native file picker is available (i.e. this is a WebGL browser build).</summary>
        public static bool Supported =>
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Open the file picker. <paramref name="onPicked"/> gets the uploaded file NAMES (their bytes
        /// are now at <c>/arcanum/&lt;name&gt;</c>); <paramref name="onError"/> gets a reason on cancel/failure.
        /// The GameObject must have a unique name (SendMessage targets it by name).</summary>
        public void Pick(Action<string[]> onPicked, Action<string> onError = null)
        {
            _onPicked = onPicked;
            _onError = onError;
#if UNITY_WEBGL && !UNITY_EDITOR
            ArcanumPickFiles(gameObject.name, nameof(OnFilesPicked), nameof(OnPickError));
#else
            onError?.Invoke("File picker is only available in a WebGL browser build.");
#endif
        }

        /// <summary>The full FS path an uploaded file was written to (for <c>MountFile</c>).</summary>
        public static string PathFor(string fileName) => DataDir + "/" + fileName;

        // Called by the .jslib via SendMessage with a "|"-joined list of written filenames.
        private void OnFilesPicked(string joined)
        {
            string[] names = string.IsNullOrEmpty(joined) ? Array.Empty<string>() : joined.Split('|');
            _onPicked?.Invoke(names);
        }

        // Called by the .jslib via SendMessage on cancel/read/write failure.
        private void OnPickError(string reason) => _onError?.Invoke(reason);
    }
}
