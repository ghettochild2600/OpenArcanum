using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M7BAreaEntrance")]
    public sealed class M7BAreaEntranceTests
    {
        private const string Overland = AreaEntranceResolver.BatesSourceSector;
        private const string Local = "maps/bates mansion lev 1/67108865.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _nav;
        private PlayerInteractionController _interaction;
        private Owner _objects, _terrain;
        private AreaEntranceResolver _entrances;
        private MapTransitionResolver _transitions;
        private ScriptFile _script;
        private MapList _maps;
        private Dictionary<string, byte[]> _files;
        private PersistentObjectState Entrance => _session.States[AreaEntranceResolver.BatesEntranceIdentity];

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M7BAreaEntranceTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _nav = _root.AddComponent<PlayerNavigationController>();
            _interaction = _root.AddComponent<PlayerInteractionController>();
            _maps = Maps();
            _script = GoldenScript();
            _files = new Dictionary<string, byte[]> {
                ["maps/arcanum1-024-fixed/map.prp"] = Properties(131072),
                ["maps/bates mansion lev 1/map.prp"] = Properties(192),
                [Overland] = Array.Empty<byte>(), [Local] = Array.Empty<byte>(),
                ["maps/bates mansion lev 1/map.jmp"] = ReturnTable(),
            };
            BindSources();
            _objects = new Owner(this, true);
            _terrain = new Owner(this, false);
            _session.RegisterTerrainOwner(_terrain);
            _session.RegisterObjectOwner(_objects);
            Assert.That(_session.SelectSector(Overland), Is.True);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticMetadataDecodesMapIdsWorldMapAreaAndDistinctSpaces()
        {
            Assert.That(_maps.TryGet(1, out var overland), Is.True);
            Assert.That(overland.Type, Is.EqualTo(MapType.StartMap));
            Assert.That(_maps.TryGet(12, out var local), Is.True);
            Assert.That(local.Area, Is.EqualTo(21));
            Assert.That(local.WorldMap, Is.EqualTo(overland.WorldMap));
            Assert.That(local.X, Is.EqualTo(104)); Assert.That(local.Y, Is.EqualTo(92));
            Assert.That(Areas().TryGet(21, out var area), Is.True);
            Assert.That(area.TileX, Is.EqualTo(62243)); Assert.That(area.TileY, Is.EqualTo(65664));
            Assert.That(area.TileX, Is.Not.EqualTo(AreaEntranceResolver.BatesSourceTile.x),
                "Tarant world-map origin is NOT the Bates physical entrance or return");
            Assert.That(AreaEntranceResolver.BatesEntranceIdentity.Key,
                Is.EqualTo("P_0000F216_00010080_00000000_00000001"));
        }

        [Test]
        public void AuthenticScriptDecodesMesKeyNotRuntimeMapIdAndIgnoresUnusedGarbage()
        {
            Assert.That(_script.Entries[0].Action.OpValue[1], Is.EqualTo(5011));
            var result = _entrances.Resolve(Entrance);
            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(result.AreaId, Is.EqualTo(21));
            Assert.That(result.Source.MapId, Is.EqualTo(1));
            Assert.That(result.Source.Tile, Is.EqualTo(new Vector2Int(61974, 65664)));
            Assert.That(result.Source.Space, Is.EqualTo(TravelLocationSpace.OverlandTile));
            Assert.That(result.Destination.MapId, Is.EqualTo(12));
            Assert.That(result.Destination.Tile, Is.EqualTo(new Vector2Int(104, 92)));
            Assert.That(result.Destination.Space, Is.EqualTo(TravelLocationSpace.LocalMapTile));
            Assert.That(result.Transition.Destination.LocalTile, Is.EqualTo(new Vector2Int(40, 28)));
        }

        [Test]
        public void PhysicalUseRelocatesSamePcThroughSharedLifecycleAndPreservesDomainRoots()
        {
            var pc = _session.PlayerState;
            var character = _session.Characters.Get(pc.Identity);
            _session.Campaign.SetFlag(77, 1);
            _session.Campaign.SetVar(10, 321);
            _session.Vitality.ApplyHitPointDamage(pc.Identity, 3);
            _session.SetMovementState(pc.Identity, pc.TilePosition,
                CritterArtResolver.WithAnimRotation(pc.ArtId, 1, 6), true);
            var result = Use();
            Assert.That(result.IsSuccess, Is.True, result.AreaEntrance?.Detail);
            Assert.That(result.ScriptRunDefault, Is.False);
            Assert.That(_session.PlayerState, Is.SameAs(pc));
            Assert.That(_session.Characters.Get(pc.Identity), Is.SameAs(character));
            Assert.That(_session.Campaign.GetFlag(77), Is.EqualTo(1));
            Assert.That(_session.Campaign.GetVar(10), Is.EqualTo(321));
            Assert.That(_session.Vitality.Get(pc.Identity).HitPointDamage, Is.EqualTo(3));
            Assert.That(_session.SelectedSector, Is.EqualTo(Local));
            Assert.That(pc.MapPosition, Is.EqualTo(new Vector2(104, 92)));
            Assert.That(CritterArtResolver.RotationOf(pc.ArtId), Is.EqualTo(6));
            Assert.That((pc.ArtId >> 6) & 31, Is.Zero);
            Assert.That(_terrain.PresentedSector, Is.EqualTo(Local));
            Assert.That(_objects.PresentedSector, Is.EqualTo(Local));
            Assert.That(_session.LoadedSectorCount, Is.EqualTo(1));
            Assert.That(_nav.Player.Identity, Is.EqualTo(pc.Identity));
            Assert.That(_nav.Player.Session, Is.SameAs(_session));
        }

        [Test]
        public void SourceReturnUsesJumpNotEntryPositionAndCanReenterSameState()
        {
            var entrance = Entrance;
            var player = _session.PlayerState;
            Assert.That(Use().IsSuccess, Is.True);
            var localObject = LocalState();
            localObject.Off = true;
            var destination = _entrances.ResolveReturn(new Vector2Int(109, 92));
            Assert.That(destination.Succeeded, Is.True, destination.Detail);
            Assert.That(destination.Destination.GlobalTile, Is.EqualTo(new Vector2Int(61976, 65664)));
            Assert.That(destination.Destination.GlobalTile, Is.Not.EqualTo(AreaEntranceResolver.BatesSourceTile));
            Assert.That(_session.SetMovementState(player.Identity, new Vector2(45, 28), player.ArtId, false), Is.True);
            Assert.That(_session.RequestCurrentJumpPoint(player.Identity).Succeeded, Is.True);
            Assert.That(player.MapPosition, Is.EqualTo(new Vector2(61976, 65664)));
            Assert.That(_session.States[entrance.Identity], Is.SameAs(entrance));
            Assert.That(Use().IsSuccess, Is.True);
            Assert.That(LocalState(), Is.SameAs(localObject));
            Assert.That(LocalState().Off, Is.True);
            Assert.That(_session.PlayerState, Is.SameAs(player));
        }

        [Test]
        public void InteractionDispatchCompletesAdmittedSceneryUseOnce()
        {
            Assert.That(_session.TryGetLoadedObject(Entrance.Identity, out WorldObject entranceRuntime), Is.True);
            Assert.That(entranceRuntime.Session, Is.SameAs(_session));
            var result = _interaction.TryUse(Entrance.Identity);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_interaction.PendingCommand, Is.Null);
            Assert.That(_interaction.Phase, Is.EqualTo(PlayerInteractionPhase.Completed));
            Assert.That(_session.SelectedSector, Is.EqualTo(Local));
        }

        [Test]
        public void EntryNormalizesOldRouteAndExecutingIntentWithoutReplay()
        {
            Assert.That(_nav.TrySetDestination(new Vector2Int(25, 0)), Is.True);
            Assert.That(Use().IsSuccess, Is.True);
            Assert.That(_session.PlayerState.Destination, Is.Null);
            Assert.That(_nav.IsMoving, Is.False);
            Assert.That(_nav.Destination, Is.Null);
            Assert.That(_nav.GlobalDestination, Is.Null);
            Assert.That(_interaction.PendingCommand, Is.Null);
            Assert.That(_session.RequestAreaEntrance(_session.PlayerState.Identity, Entrance.Identity).Failure,
                Is.EqualTo(AreaEntranceFailure.TargetUnavailable));
            Assert.That(_session.SelectedSector, Is.EqualTo(Local));
        }

        [Test]
        public void V1SaveRestoreAtLocalEntryAndOverlandReturnKeepsIdentityAndSourcesBound()
        {
            Assert.That(Use().IsSuccess, Is.True);
            string local = _session.SaveGames.SerializeCurrentSession();
            Assert.That(local, Does.Contain("\"version\": 1"));
            Assert.That(_session.SetMovementState(_session.PlayerState.Identity, new Vector2(45, 28),
                _session.PlayerState.ArtId, false), Is.True);
            Assert.That(_session.RequestCurrentJumpPoint(_session.PlayerState.Identity).Succeeded, Is.True);
            string overland = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(local).Succeeded, Is.True);
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(new Vector2(104, 92)));
            Assert.That(_session.SelectedSector, Is.EqualTo(Local));
            Assert.That(_session.SaveGames.LoadJson(overland).Succeeded, Is.True);
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(new Vector2(61976, 65664)));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(Use().IsSuccess, Is.True);
        }

        [Test]
        public void UnknownEntranceFailsExplicitly()
            => Assert.That(_entrances.Resolve(null).Failure, Is.EqualTo(AreaEntranceFailure.UnknownEntrance));

        [Test]
        public void DynamicEquipmentAndStackSurviveFullEntranceReturnReentryLoop()
        {
            var pc = _session.PlayerState;
            var bootsProto = new ObjectProtoInfo(7000, ObjectType.Armor, 0x40000000u, invAid: 4u << 14);
            var goldProto = new ObjectProtoInfo(9056, ObjectType.Gold, 0x40000000u) { GoldQuantity = 37 };
            _session.BindPrototypeSource(number => number == 7000 ? bootsProto : number == 9056 ? goldProto : null);
            var boots = _session.CreateItem(7000, ObjectPlacement.ContainedBy(pc.Identity));
            var gold = _session.CreateItem(9056, ObjectPlacement.ContainedBy(pc.Identity));
            Assert.That(boots.Succeeded && gold.Succeeded, Is.True);
            Assert.That(_session.EquipItem(pc.Identity, boots.State.Identity, WornLocation.Boots).Succeeded, Is.True);
            Assert.That(Use().IsSuccess, Is.True);
            _session.SetMovementState(pc.Identity, new Vector2(45, 28), pc.ArtId, false);
            Assert.That(_session.RequestCurrentJumpPoint(pc.Identity).Succeeded, Is.True);
            Assert.That(Use().IsSuccess, Is.True);
            Assert.That(_session.States[boots.State.Identity], Is.SameAs(boots.State));
            Assert.That(_session.States[gold.State.Identity], Is.SameAs(gold.State));
            Assert.That(boots.State.Identity.Type, Is.EqualTo(ArcanumObjectIdType.SessionDynamic));
            Assert.That(boots.State.Placement, Is.EqualTo(ObjectPlacement.EquippedBy(pc.Identity, WornLocation.Boots)));
            Assert.That(gold.State.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(pc.Identity)));
            Assert.That(gold.State.StackQuantity, Is.EqualTo(37));
        }

        [TestCase("missing", AreaEntranceFailure.MissingEntry)]
        [TestCase("conditional", AreaEntranceFailure.UnsupportedEntranceType)]
        [TestCase("extra-action", AreaEntranceFailure.UnsupportedEntranceType)]
        [TestCase("wrong-focus", AreaEntranceFailure.MalformedEntry)]
        [TestCase("variable-coordinate", AreaEntranceFailure.MalformedEntry)]
        [TestCase("invalid-map", AreaEntranceFailure.InvalidDestinationMap)]
        [TestCase("negative-coordinate", AreaEntranceFailure.DestinationPreflightFailed)]
        [TestCase("out-of-bounds", AreaEntranceFailure.DestinationPreflightFailed)]
        [TestCase("missing-sector", AreaEntranceFailure.DestinationPreflightFailed)]
        [TestCase("missing-properties", AreaEntranceFailure.DestinationPreflightFailed)]
        [TestCase("malformed-properties", AreaEntranceFailure.DestinationPreflightFailed)]
        [TestCase("unknown-area", AreaEntranceFailure.UnknownArea)]
        [TestCase("wrong-worldmap", AreaEntranceFailure.InvalidDestinationMap)]
        public void InvalidEntrancePreflightPreservesExactSessionAndBothOwners(string scenario, AreaEntranceFailure failure)
        {
            switch (scenario)
            {
                case "missing": _script = null; break;
                case "conditional": _script.Entries[0].Type = (int)Sct.GlobalFlag; break;
                case "extra-action": _script.Entries.Add(new ScriptCondition()); break;
                case "wrong-focus": _script.Entries[0].Action.OpType[0] = (byte)Sfo.Attachee; break;
                case "variable-coordinate": _script.Entries[0].Action.OpType[2] = (byte)Svt.GlVar; break;
                case "invalid-map": _script.Entries[0].Action.OpValue[1] = 4999; break;
                case "negative-coordinate": _script.Entries[0].Action.OpValue[2] = -1; break;
                case "out-of-bounds": _script.Entries[0].Action.OpValue[2] = 192; break;
                case "missing-sector": _files.Remove(Local); break;
                case "missing-properties": _files.Remove("maps/bates mansion lev 1/map.prp"); break;
                case "malformed-properties": _files["maps/bates mansion lev 1/map.prp"] = new byte[1]; break;
            }
            BindSources(scenario == "unknown-area" ? new AreaList(new List<Area>()) : Areas(),
                scenario == "wrong-worldmap" ? Maps(1) : _maps);
            string before = _session.SaveGames.SerializeCurrentSession();
            var pc = _session.PlayerState;
            int clears = _objects.Clears + _terrain.Clears;
            var result = _session.RequestAreaEntrance(pc.Identity, Entrance.Identity);
            Assert.That(result.Failure, Is.EqualTo(failure), result.Detail);
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
            Assert.That(_session.PlayerState, Is.SameAs(pc));
            Assert.That(_objects.Clears + _terrain.Clears, Is.EqualTo(clears));
            Assert.That(_objects.PresentedSector, Is.EqualTo(Overland));
            Assert.That(_terrain.PresentedSector, Is.EqualTo(Overland));
        }

        [Test]
        public void OutOfRangeCannotTeleport()
        {
            _session.SetMovementState(_session.PlayerState.Identity, new Vector2(30, 0),
                _session.PlayerState.ArtId, false);
            string before = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.RequestAreaEntrance(_session.PlayerState.Identity, Entrance.Identity).Failure,
                Is.EqualTo(AreaEntranceFailure.OutOfRange));
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
        }

        [Test]
        public void NonPcActorCannotTravel()
            => Assert.That(_session.RequestAreaEntrance(ArcanumObjectId.CreateAuthored(9), Entrance.Identity).Failure,
                Is.EqualTo(AreaEntranceFailure.InvalidActor));

        [Test]
        public void ReentrantRequestIsBusyAndOuterActivationOccursOnce()
        {
            AreaEntranceResult nested = default;
            int selected = 0;
            _session.SectorUnloading += _ => nested = _session.RequestAreaEntrance(_session.PlayerState.Identity, Entrance.Identity);
            _session.SectorSelected += _ => selected++;
            Assert.That(Use().IsSuccess, Is.True);
            Assert.That(nested.Failure, Is.EqualTo(AreaEntranceFailure.Busy));
            Assert.That(selected, Is.EqualTo(1));
            Assert.That(_session.IsMapTransitionActive, Is.False);
        }

        [Test]
        public void PresentationRejectionRollsBackSamePcAndSourceOwners()
        {
            var pc = _session.PlayerState;
            Vector2 position = pc.MapPosition;
            _objects.Reject = Local;
            Assert.That(_session.RequestAreaEntrance(pc.Identity, Entrance.Identity).Failure,
                Is.EqualTo(AreaEntranceFailure.PresentationFailed));
            Assert.That(_session.PlayerState, Is.SameAs(pc));
            Assert.That(pc.MapPosition, Is.EqualTo(position));
            Assert.That(_session.SelectedSector, Is.EqualTo(Overland));
            Assert.That(_terrain.PresentedSector, Is.EqualTo(Overland));
            Assert.That(_objects.PresentedSector, Is.EqualTo(Overland));
        }

        [Test]
        public void NoDiscoveryGateOrTarantOriginIsInventedForPhysicalEntrance()
        {
            Assert.That(_session.Campaign.GetFlag(77), Is.Zero);
            Assert.That(Use().IsSuccess, Is.True);
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(new Vector2(104, 92)));
        }

        private WorldInteractionResult Use() => _session.ExecuteInteraction(new WorldInteractionCommand(
            _session.PlayerState.Identity, AreaEntranceResolver.BatesEntranceIdentity, WorldInteractionCommandType.Use));

        private void BindSources(AreaList areas = null, MapList maps = null)
        {
            _transitions = new MapTransitionResolver(maps ?? _maps, _files.ContainsKey, path => _files[path]);
            _entrances = new AreaEntranceResolver(maps ?? _maps, areas ?? Areas(), _transitions, _ => _script);
            _session.BindMapTransitionSource(_transitions);
            _session.BindAreaEntranceSource(_entrances);
        }

        private PersistentObjectState LocalState()
        {
            var source = new ObjectInstance(ObjectType.Scenery, 4000, Loc(103, 93), 0x40000000, 0, 0);
            return _session.GetOrCreate(source, ArcanumObjectId.CreateAuthored(99), Local, 0x40000000, false, false);
        }

        private static MapList Maps(int localWorldMap = 0)
        {
            var text = new StringBuilder("{5000}{Arcanum1-024-fixed,92958,82592,Type: START_MAP,WorldMap: 0}\n");
            for (int key = 5001; key < 5011; key++) text.AppendLine($"{{{key}}}{{Unused-{key},0,0}}");
            text.AppendLine($"{{5011}}{{Bates Mansion Lev 1,104,92,WorldMap: {localWorldMap},Area: 21}}");
            return MapList.Read(MesReader.Read(text.ToString()));
        }

        private static AreaList Areas() => AreaList.FromMes(MesReader.Read("{21}{62243,65664,0,0/Tarant/Industrial city.}"));
        private static long Loc(int x, int y) => (uint)x | ((long)(uint)y << 32);
        private static byte[] Properties(long size)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(0); writer.Write(0); writer.Write(size); writer.Write(size); return stream.ToArray();
        }
        private static byte[] ReturnTable()
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(1); writer.Write(0u); writer.Write(0); writer.Write(Loc(109, 92));
            writer.Write(1); writer.Write(0); writer.Write(Loc(61976, 65664)); return stream.ToArray();
        }
        private static ScriptFile GoldenScript()
        {
            // Reconstructed two-instruction retail semantics, not a copied proprietary asset.
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(0); writer.Write(0); writer.Write(new byte[40]);
            writer.Write(0); writer.Write(2); writer.Write(2); writer.Write(0);
            for (int line = 0; line < 2; line++)
            {
                writer.Write((int)Sct.True); writer.Write(new byte[40]);
                writer.Write(line == 0 ? (int)Sat.Teleport : (int)Sat.ReturnAndSkipDefault);
                writer.Write(new byte[] { 0, 3, 3, 3, 0, 240, 253, 127 });
                foreach (int value in new[] { -1022296064, 5011, 104, 92, 351, 11074016, 359, -1074326201 }) writer.Write(value);
                writer.Write((int)Sat.DoNothing); writer.Write(new byte[40]);
            }
            return ScriptReader.Read(stream.ToArray());
        }

        private static void SetProperty(object target, string name, object value)
            => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly M7BAreaEntranceTests _test;
            private readonly bool _objects;
            private GameObject _views;
            public string Reject;
            public int Clears;
            public string ConfiguredSector => Overland;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public Owner(M7BAreaEntranceTests test, bool objects) { _test = test; _objects = objects; }
            public bool PresentSector(string path)
            {
                if (path == Reject) return false;
                PresentedSector = path;
                if (!_objects) return true;
                var session = _test._session;
                session.BeginSector(path);
                _views = new GameObject("TestViews"); _views.transform.SetParent(_test._root.transform);
                var map = new SectorNavigationMap(new SectorTerrain(new uint[4096]), new bool[4096],
                    TileNameTable.FromMes(MesReader.Read("{300}{grs}")));
                SetProperty(_test._loader, nameof(WorldObjectSectorLoader.NavigationMap), map);
                if (path == Overland)
                {
                    var source = new ObjectInstance(ObjectType.Scenery, 4036, Loc(61974, 65664), 0x40000000, 0, 0);
                    source.UseScriptNum = 1267;
                    var state = session.GetOrCreate(source, AreaEntranceResolver.BatesEntranceIdentity, path, 0x40000000, false, false);
                    var runtime = Runtime(ObjectType.Scenery);
                    session.Bind(path, state, runtime);
                }
                var player = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    path, path == Overland ? new Vector2(24, 0) : new Vector2(40, 28), 0x28100000);
                var pc = Runtime(ObjectType.Pc);
                session.BindPlayer(path, player, pc);
                _test._nav.TryBind(pc);
                return true;
            }
            private WorldObject Runtime(ObjectType type)
            {
                var go = new GameObject(type.ToString()); go.transform.SetParent(_views.transform);
                var runtime = go.AddComponent<WorldObject>(); runtime.Type = type; return runtime;
            }
            public void ClearPresentedSector()
            {
                Clears++;
                if (_objects && PresentedSector != null)
                {
                    _test._nav.Unbind();
                    _test._session.UnloadSector(PresentedSector);
                    Object.DestroyImmediate(_views);
                }
                PresentedSector = null;
            }
        }
    }
}
