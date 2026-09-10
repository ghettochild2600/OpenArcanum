using System;
using System.Collections.Generic;
using System.Reflection;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M2AInteraction")]
    public sealed class M2AInteractionKernelTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private SectorNavigationMap _map;
        private PersistentPlayerState _playerState;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M2AInteractionKernelTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _navigation = _root.AddComponent<PlayerNavigationController>();
            _interaction = _root.AddComponent<PlayerInteractionController>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            _map = Map();
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), _map);
            _playerState = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject player = Runtime("Player", ObjectType.Pc);
            _session.BindPlayer(Sector, _playerState, player);
            Assert.That(_navigation.TryBind(player), Is.True);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void CommandReturnsExplicitActorTargetAndRangeFailures()
        {
            WorldObject portal = Portal(1, 10, 10);
            var wrongActor = ArcanumObjectId.CreateGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            Assert.That(_session.ExecuteInteraction(new WorldInteractionCommand(
                wrongActor, portal.Identity, WorldInteractionCommandType.Use)).Code,
                Is.EqualTo(WorldInteractionResultCode.ActorNotFound));
            Assert.That(_session.ExecuteInteraction(new WorldInteractionCommand(
                _playerState.Identity, AuthoredId(99), WorldInteractionCommandType.Use)).Code,
                Is.EqualTo(WorldInteractionResultCode.TargetNotFound));
            Assert.That(_session.ExecuteInteraction(new WorldInteractionCommand(
                _playerState.Identity, portal.Identity, WorldInteractionCommandType.Use)).Code,
                Is.EqualTo(WorldInteractionResultCode.OutOfRange));
        }

        [Test]
        public void SemanticObjectIdResolvesAuthoritativeStateAndRuntime()
        {
            WorldObject portal = Portal(1, 3, 1);
            byte[] padded = IdBytes(1);
            padded[3] = 99;
            padded[20] = 77;
            ArcanumObjectId equivalent = ArcanumObjectId.FromBytes(padded);
            Assert.That(_session.TryGetObjectState(equivalent, out PersistentObjectState state), Is.True);
            Assert.That(state.Identity, Is.EqualTo(portal.Identity));
            Assert.That(_session.TryGetLoadedObject(equivalent, out WorldObject loaded), Is.True);
            Assert.That(loaded, Is.SameAs(portal));
        }

        [Test]
        public void PortalUseRangeIsSourceChebyshevTwo()
        {
            Assert.That(InteractionRangeRules.Distance(new Vector2(1, 1), new Vector2(3, 3)), Is.EqualTo(2));
            Assert.That(InteractionRangeRules.IsWithin(new Vector2(1, 1), new Vector2(3, 3), 2), Is.True);
            Assert.That(InteractionRangeRules.IsWithin(new Vector2(1, 1), new Vector2(4, 3), 2), Is.False);
        }

        [Test]
        public void DeterministicOverlappingSelectionUsesFrontmostThenObjectId()
        {
            Texture2D texture = SolidTexture();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 1);
            WorldObject back = Selectable("Back", AuthoredId(2), sprite, 4);
            WorldObject front = Selectable("Front", AuthoredId(3), sprite, 5);
            Assert.That(WorldObjectTargetSelector.TrySelectPortal(new[] { back, front }, Vector2.zero, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(front.Identity));
            front.View.sortingOrder = 4;
            Assert.That(WorldObjectTargetSelector.TrySelectPortal(new[] { back, front }, Vector2.zero, out selected), Is.True);
            Assert.That(selected, Is.EqualTo(back.Identity));
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void TransparentPixelAndNonPortalAreNotTargets()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 1);
            WorldObject portal = Selectable("Transparent", AuthoredId(1), sprite, 5);
            WorldObject scenery = Selectable("Scenery", AuthoredId(2), sprite, 6);
            scenery.Type = ObjectType.Scenery;
            Assert.That(WorldObjectTargetSelector.TrySelectPortal(new[] { portal, scenery }, Vector2.zero, out _), Is.False);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void ApproachPlannerChoosesShortestStableRangePosition()
        {
            var route = new List<Vector2Int>();
            Assert.That(new InteractionApproachPlanner().TryPlan(_map, new Vector2Int(1, 1),
                new Vector2Int(8, 1), 2, out Vector2Int destination, route), Is.True);
            Assert.That(destination, Is.EqualTo(new Vector2Int(6, 0)));
            Assert.That(route.Count, Is.EqualTo(5));
        }

        [Test]
        public void ApproachPlannerReportsUnreachableRange()
        {
            var blocked = new List<Vector2Int>();
            for (int y = 0; y <= 3; y++)
            for (int x = 6; x <= 10; x++) blocked.Add(new Vector2Int(x, y));
            var route = new List<Vector2Int>();
            Assert.That(new InteractionApproachPlanner().TryPlan(Map(blocked.ToArray()), new Vector2Int(1, 1),
                new Vector2Int(8, 1), 2, out _, route), Is.False);
            Assert.That(route, Is.Empty);
        }

        [Test]
        public void ApproachIntentWalksThenExecutesUse()
        {
            WorldObject portal = Portal(1, 8, 1);
            WorldInteractionResult accepted = _interaction.TryUse(portal.Identity);
            Assert.That(accepted.Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_interaction.Phase, Is.EqualTo(PlayerInteractionPhase.ApproachingTarget));
            _navigation.AdvanceNavigation(100);
            _interaction.AdvanceInteraction();
            Assert.That(_interaction.LastResult.Value.Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(_interaction.Phase, Is.EqualTo(PlayerInteractionPhase.Completed));
            Assert.That(_session.Portals.Phase(portal.Identity), Is.EqualTo(PortalPhase.Opening));
        }

        [Test]
        public void ManualMovementCancelsPendingInteraction()
        {
            WorldObject portal = Portal(1, 8, 1);
            Assert.That(_interaction.TryUse(portal.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_navigation.TrySetDestination(new Vector2Int(2, 4)), Is.True);
            Assert.That(_interaction.Phase, Is.EqualTo(PlayerInteractionPhase.Cancelled));
            Assert.That(_interaction.LastResult.Value.Code, Is.EqualTo(WorldInteractionResultCode.Cancelled));
            _navigation.AdvanceNavigation(100);
            _interaction.AdvanceInteraction();
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
        }

        [Test]
        public void NewTargetReplacesPriorIntentWithoutExecutingIt()
        {
            WorldObject first = Portal(1, 8, 1);
            WorldObject second = Portal(2, 8, 4);
            Assert.That(_interaction.TryUse(first.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_interaction.TryUse(second.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_interaction.PendingCommand.Value.Target, Is.EqualTo(second.Identity));
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
            Assert.That(_session.States[first.Identity].PortalOpen, Is.False);
        }

        [Test]
        public void TargetUnloadCancelsApproachAndCannotExecuteLater()
        {
            WorldObject portal = Portal(1, 8, 1);
            Assert.That(_interaction.TryUse(portal.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            _session.UnloadSector(Sector);
            _interaction.AdvanceInteraction();
            Assert.That(_interaction.LastResult.Value.Code, Is.EqualTo(WorldInteractionResultCode.TargetNotFound));
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
        }

        [Test]
        public void SuccessfulUseTogglesOnlyThroughScheduler()
        {
            WorldObject portal = Portal(1, 3, 1);
            WorldInteractionResult opening = _interaction.TryUse(portal.Identity);
            Assert.That(opening.Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(opening.RequestedPortalOpen, Is.True);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
            Assert.That(portal.IsOpen, Is.True);
            _session.Portals.Tick(.25);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.True);
            WorldInteractionResult closing = _interaction.TryUse(portal.Identity);
            Assert.That(closing.RequestedPortalOpen, Is.False);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.True);
            _session.Portals.Tick(.25);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
            Assert.That(portal.IsOpen, Is.False);
        }

        [Test]
        public void UnboundScriptSourceAndLockedPortalsAreExplicitlyRefused()
        {
            WorldObject scripted = Portal(1, 3, 1, useScript: 42);
            Assert.That(_interaction.TryUse(scripted.Identity).Code, Is.EqualTo(WorldInteractionResultCode.ScriptUnavailable));
            WorldObject locked = Portal(2, 2, 3, locked: true);
            Assert.That(_interaction.TryUse(locked.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Blocked));
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
        }

        [Test]
        public void ClosedPortalBlocksAndOpeningMakesEdgeTraversable()
        {
            WorldObject portal = Portal(1, 2, 2, rotation: 5);
            Assert.That(_map.CanTraverse(new Vector2Int(2, 2), 5), Is.False);
            _session.SetMovementState(_playerState.Identity, new Vector2(2, 1), _playerState.ArtId, false);
            Assert.That(_interaction.TryUse(portal.Identity).IsSuccess, Is.True);
            Assert.That(_map.CanTraverse(new Vector2Int(2, 2), 5), Is.True);
            _session.Portals.Tick(.25);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.True);
        }

        [Test]
        public void OpenStatePersistsAcrossUnbindAndReload()
        {
            WorldObject portal = Portal(1, 3, 1);
            Assert.That(_interaction.TryUse(portal.Identity).IsSuccess, Is.True);
            _session.Portals.Tick(.25);
            PersistentObjectState state = _session.States[portal.Identity];
            _session.UnloadSector(Sector);
            Object.DestroyImmediate(portal.gameObject);
            _session.BeginSector(Sector);
            WorldObject restored = Runtime("RestoredPortal", ObjectType.Portal);
            _session.Bind(Sector, state, restored);
            restored.PortalOpenable = true;
            _session.BindPortal(state, restored, 7, 8);
            Assert.That(restored.IsOpen, Is.True);
            Assert.That(PortalTransitionScheduler.Frame(restored.ArtId), Is.EqualTo(3));
            Assert.That(_session.Portals.Phase(restored.Identity), Is.EqualTo(PortalPhase.Open));
        }

        [Test]
        public void PresentationRefreshDoesNotMutatePendingIntentOrPortalState()
        {
            WorldObject portal = Portal(1, 8, 1);
            Assert.That(_interaction.TryUse(portal.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            WorldInteractionCommand command = _interaction.PendingCommand.Value;
            uint stableArt = _session.States[portal.Identity].ArtId;
            portal.ReRender = _ => null;
            portal.SetArt(portal.ArtId);
            Assert.That(_interaction.PendingCommand.Value.Target, Is.EqualTo(command.Target));
            Assert.That(_interaction.Phase, Is.EqualTo(PlayerInteractionPhase.ApproachingTarget));
            Assert.That(_session.States[portal.Identity].ArtId, Is.EqualTo(stableArt));
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
        }

        private WorldObject Portal(int number, int x, int y, int rotation = 5, int useScript = 0, bool locked = false)
        {
            uint art = 0x30000000u | ((uint)rotation << 11);
            ObjectInstance source = Source(number, x, y, ObjectType.Portal, art);
            PersistentObjectState state = _session.GetOrCreate(source, Sector, art, false, locked);
            WorldObject runtime = Runtime("Portal" + number, ObjectType.Portal);
            _session.Bind(Sector, state, runtime);
            runtime.PortalOpenable = true;
            state.UseScriptNum = useScript;
            runtime.UseScriptNum = state.UseScriptNum;
            _session.BindPortal(state, runtime, 7, 8);
            _map.Register(runtime, 0);
            return runtime;
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private WorldObject Selectable(string name, ArcanumObjectId identity, Sprite sprite, int sortingOrder)
        {
            WorldObject runtime = Runtime(name, ObjectType.Portal);
            runtime.Identity = identity;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(runtime.transform);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            runtime.View = renderer;
            return runtime;
        }

        private static ObjectInstance Source(int number, int x, int y, ObjectType type, uint art)
            => new(type, 2000 + number, Location(x, y), art, 0, 0, oid: IdBytes(number));

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static byte[] IdBytes(int number)
        {
            var bytes = new byte[24];
            bytes[0] = 1;
            Array.Copy(BitConverter.GetBytes(number), 0, bytes, 8, 4);
            return bytes;
        }

        private static ArcanumObjectId AuthoredId(int number) => ArcanumObjectId.FromBytes(IdBytes(number));

        private static Texture2D SolidTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return texture;
        }

        private static SectorNavigationMap Map(params Vector2Int[] blocked)
        {
            var mask = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blocked) mask[tile.y * SectorCoordinate.Size + tile.x] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), mask, names);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }
    }
}
