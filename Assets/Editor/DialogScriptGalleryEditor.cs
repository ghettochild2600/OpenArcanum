using Arcanum.Runtime.Demo;
using UnityEditor;
using UnityEngine;

namespace Arcanum.Editor
{
    /// <summary>
    /// Inspector for <see cref="DialogScriptGallery"/> — adds a button that opens the searchable
    /// <see cref="DialogBrowserWindow"/> for picking which NPC dialog to run in the bench.
    /// </summary>
    [CustomEditor(typeof(DialogScriptGallery))]
    public sealed class DialogScriptGalleryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Browse dialogs…"))
                DialogBrowserWindow.Open((DialogScriptGallery)target);

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Enter Play mode, then pick a dialog from the browser to run it in the bench. " +
                    "The browser lists dialogs in edit mode too, but Talk needs Play mode.", MessageType.Info);
        }
    }
}
