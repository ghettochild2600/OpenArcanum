using System.IO;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace OpenArcanum.Editor
{
    internal static class UiComparisonCapture
    {
        [MenuItem("OpenArcanum/UI Comparison/Capture ST 8 at 800x600 _F7")]
        private static void CaptureStrengthEight()
            => SetStrengthAndCapture(8);

        [MenuItem("OpenArcanum/UI Comparison/Capture ST 10 at 800x600 _F10")]
        private static void CaptureStrengthTen()
            => SetStrengthAndCapture(10);

        [MenuItem("OpenArcanum/UI Comparison/Capture ST 20 at 800x600 _F12")]
        private static void CaptureStrengthTwenty()
            => SetStrengthAndCapture(20);

        private static void SetStrengthAndCapture(int value)
        {
            ProductionGameUiPresenter presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
            RetailCharacterCreationView view = Object.FindFirstObjectByType<RetailCharacterCreationView>();
            if (presenter?.Controller?.CreationDraft == null || view == null)
            {
                Debug.LogError("UI comparison capture requires the live Character Creation editor.");
                return;
            }

            presenter.Controller.CreationDraft.SetAttribute(CharacterAttribute.Strength, value);
            view.Synchronize();
            Canvas.ForceUpdateCanvases();
            Capture($"openarcanum-st{value}-800x600.png");
        }

        private static void Capture(string fileName)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("UI comparison capture requires Play Mode.");
                return;
            }

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "UiComparison"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"UI comparison capture queued: {path}");
        }
    }
}
