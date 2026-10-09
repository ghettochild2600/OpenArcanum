using System;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UiF1DialoguePresentation")]
    public sealed class UiF1DialoguePresentationTests
    {
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(UiF1DialoguePresentationTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector("maps/test/1.sec"), Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            PersistentPlayerState player = _session.GetOrCreatePlayer(pc, "maps/test/1.sec", Vector2.one,
                0x28100000u);
            _session.BindPlayer("maps/test/1.sec", player, Runtime("Player", ObjectType.Pc));
            _controller = new GameUiController(_session);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ResponseStripUsesSourceFiveRowBoundaryAtEightHundredBySixHundred()
        {
            Assert.That(RetailDialogueLayout.ResponseScreenRect(800, 600),
                Is.EqualTo(new Rect(200f, 341f, 400f, 100f)));
            Assert.That(RetailDialogueLayout.ResponseRowRect(0),
                Is.EqualTo(new Rect(6f, 0f, 388f, 20f)));
            Assert.That(RetailDialogueLayout.ResponseRowRect(4),
                Is.EqualTo(new Rect(6f, 80f, 388f, 20f)));
            Assert.That(RetailDialogueLayout.VisibleResponseCount(9), Is.EqualTo(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetailDialogueLayout.ResponseRowRect(5));
            Assert.That(RetailDialogueLayout.NpcBubbleFontSourceId, Is.EqualTo(229));
            Assert.That((RetailDialogueLayout.NpcBubbleMaximumWidth,
                    RetailDialogueLayout.NpcBubbleMaximumHeight),
                Is.EqualTo((200f, 200f)));
        }

        [TestCase(1024, 768)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void ResponseStripStaysNativeSizeCenteredAboveNativeHud(int width, int height)
        {
            Rect rect = RetailDialogueLayout.ResponseScreenRect(width, height);
            Assert.That((rect.width, rect.height), Is.EqualTo((400f, 100f)));
            Assert.That(rect.x, Is.EqualTo((width - 400f) * .5f));
            Assert.That(rect.yMax, Is.EqualTo(height - 159f));
        }

        [Test]
        public void DialogueOwnsInterfaceStateButLeavesSourceWorldMovementUnblocked()
        {
            Assert.That(_controller.Open(GameUiScreen.Dialogue), Is.True);
            Assert.That((_controller.Screen, _controller.IsModalOpen, _controller.BlocksWorldInput),
                Is.EqualTo((GameUiScreen.Dialogue, true, false)));
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True,
                "without an active dialogue transaction this setup may leave the presentation-only route");
            Assert.That(_controller.BlocksWorldInput, Is.True);
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => "maps/test/1.sec";
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string path = PresentedSector;
                PresentedSector = null;
                if (path != null) _session.UnloadSector(path);
            }
        }
    }
}
