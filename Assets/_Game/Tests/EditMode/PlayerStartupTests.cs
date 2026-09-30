using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcanum.Formats.Tests
{
    [Category("PlayerStartup")]
    public sealed class PlayerStartupTests
    {
        private const string ProductionScene = "Assets/_Game/Scenes/OpenArcanum.unity";
        private const string TestScene = "Assets/_Game/Scenes/TestTerrain.unity";
        private const string StartSector = "maps/arcanum1-024-fixed/86570436012.sec";

        [Test]
        public void ProductionSceneIsFirstAndTestTerrainRemainsAvailable()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes, Has.Length.EqualTo(2));
            Assert.That((scenes[0].path, scenes[0].enabled), Is.EqualTo((ProductionScene, true)));
            Assert.That((scenes[1].path, scenes[1].enabled), Is.EqualTo((TestScene, true)));
        }

        [Test]
        public void ProductionSceneHasPlayerFacingCompositionWithoutDebugAutostart()
        {
            Scene scene = EditorSceneManager.OpenScene(ProductionScene, OpenSceneMode.Additive);
            try
            {
                GameObject runtime = FindRoot(scene, "OpenArcanum Runtime");
                GameObject terrain = FindRoot(scene, "World Terrain");
                GameObject camera = FindRoot(scene, "Main Camera");

                Assert.That(runtime.GetComponent<WorldObjectSectorLoader>(), Is.Not.Null);
                Assert.That(runtime.GetComponent<WorldMapSessionCoordinator>(), Is.Not.Null);
                Assert.That(runtime.GetComponent<ProductionPlayerLifecycle>(), Is.Not.Null);
                Assert.That(runtime.GetComponent<PlayerNavigationController>(), Is.Not.Null);
                Assert.That(runtime.GetComponent<PlayerClickMoveInput>(), Is.Not.Null);
                Assert.That(terrain.GetComponent<TileMapDemo>(), Is.Not.Null);
                Assert.That(camera.GetComponent<Camera>(), Is.Not.Null);
                Assert.That(camera.GetComponent<AudioListener>(), Is.Not.Null);

                var loader = new SerializedObject(runtime.GetComponent<WorldObjectSectorLoader>());
                var session = new SerializedObject(runtime.GetComponent<WorldMapSessionCoordinator>());
                var terrainPresenter = new SerializedObject(terrain.GetComponent<TileMapDemo>());
                Assert.That(loader.FindProperty("sectorPath").stringValue, Is.EqualTo(StartSector));
                Assert.That(loader.FindProperty("loadOnStart").boolValue, Is.False);
                Assert.That(session.FindProperty("initialSector").stringValue, Is.EqualTo(StartSector));
                Assert.That(session.FindProperty("selectOnStart").boolValue, Is.False);
                Assert.That(terrainPresenter.FindProperty("SectorPath").stringValue, Is.EqualTo(StartSector));
                Assert.That(terrainPresenter.FindProperty("LoadConfiguredSectorOnStart").boolValue, Is.False);
                Assert.That(terrainPresenter.FindProperty("ShowSectorBrowser").boolValue, Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NoPlayerSessionStartsAtExistingProductionMainMenu()
        {
            var root = new GameObject(nameof(NoPlayerSessionStartsAtExistingProductionMainMenu));
            try
            {
                var session = root.AddComponent<WorldMapSessionCoordinator>();
                var controller = new GameUiController(session);
                Assert.That(controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
                Assert.That(controller.HasPlayer, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            Assert.Fail($"Production scene root '{name}' was not found.");
            return null;
        }
    }
}
