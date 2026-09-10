using System;
using System.Reflection;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using Arcanum.Script;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M2BUseScript")]
    public sealed class M2BUseScriptDispatchTests
    {
        private const string Sector = "maps/test/1.sec";
        private const int PanariiOfficeDoorScript = 1162;
        private const int PanariiOfficeDoorFlag = 2087;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _player;
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M2BUseScriptDispatchTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _navigation = _root.AddComponent<PlayerNavigationController>();
            _interaction = _root.AddComponent<PlayerInteractionController>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            _map = Map();
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), _map);
            _player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject player = Runtime("Player", ObjectType.Pc);
            _session.BindPlayer(Sector, _player, player);
            Assert.That(_navigation.TryBind(player), Is.True);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticPanariiGateSuppressesDefaultWhenFlagIsClear()
        {
            WorldObject portal = Portal(1, PanariiOfficeDoorScript);
            _session.BindUseScriptSource(n => n == PanariiOfficeDoorScript ? PanariiGate() : null);

            WorldInteractionResult result = Use(portal);

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(result.ScriptNum, Is.EqualTo(PanariiOfficeDoorScript));
            Assert.That(result.ScriptStatus, Is.EqualTo(ScriptExecutionStatus.Executed));
            Assert.That(result.ScriptRunDefault, Is.False);
            Assert.That(result.RequestedPortalOpen, Is.Null);
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
        }

        [Test]
        public void AuthenticPanariiGateRunsBuiltInSchedulerWhenFlagIsSet()
        {
            WorldObject portal = Portal(1, PanariiOfficeDoorScript);
            _session.BindUseScriptSource(n => n == PanariiOfficeDoorScript ? PanariiGate() : null);
            _session.ScriptGlobals.SetFlag(PanariiOfficeDoorFlag, 1);

            WorldInteractionResult result = Use(portal);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ScriptRunDefault, Is.True);
            Assert.That(result.RequestedPortalOpen, Is.True);
            Assert.That(_session.Portals.ActiveCount, Is.EqualTo(1));
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False,
                "the scheduler, not the VM or presentation, commits the transition");
        }

        [Test]
        public void MissingScriptFailsClosedWithoutDefaultOrTransition()
        {
            WorldObject portal = Portal(1, 7777);
            _session.BindUseScriptSource(_ => null);

            WorldInteractionResult result = Use(portal);

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.ScriptMissing));
            Assert.That(result.ScriptStatus, Is.EqualTo(ScriptExecutionStatus.MissingScript));
            Assert.That(result.RequestedPortalOpen, Is.Null);
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
        }

        [Test]
        public void UnsupportedOpcodeFailsBeforeAnyPortalDefault()
        {
            WorldObject portal = Portal(1, 42);
            ScriptFile unsupported = File(Cond(Sct.True, Act(Sat.ToggleOpenClosed), Act(Sat.DoNothing)));
            _session.BindUseScriptSource(n => n == 42 ? unsupported : null);

            WorldInteractionResult result = Use(portal);

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.ScriptUnsupported));
            Assert.That(result.ScriptStatus, Is.EqualTo(ScriptExecutionStatus.UnsupportedOpcode));
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
        }

        [Test]
        public void RunawayVmFlowIsExplicitFailureAndFailsClosed()
        {
            WorldObject portal = Portal(1, 43);
            ScriptFile loop = File(Cond(Sct.True, Act(Sat.Goto, (Svt.Number, 0)), Act(Sat.DoNothing)));
            _session.BindUseScriptSource(n => n == 43 ? loop : null);

            WorldInteractionResult result = Use(portal);

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.ScriptFailed));
            Assert.That(result.ScriptStatus, Is.EqualTo(ScriptExecutionStatus.Runaway));
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
        }

        [Test]
        public void CancelledApproachNeverDispatchesAttachedUseScript()
        {
            WorldObject portal = Portal(1, PanariiOfficeDoorScript, 8, 1);
            _map.Register(portal, 0);
            int resolutions = 0;
            _session.BindUseScriptSource(n =>
            {
                resolutions++;
                return n == PanariiOfficeDoorScript ? PanariiGate() : null;
            });

            Assert.That(_interaction.TryUse(portal.Identity).Code,
                Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(resolutions, Is.Zero, "SAP_USE is resolved only at authoritative execution time");
            Assert.That(_interaction.CancelPending().Code, Is.EqualTo(WorldInteractionResultCode.Cancelled));
            _navigation.AdvanceNavigation(100);
            _interaction.AdvanceInteraction();

            Assert.That(resolutions, Is.Zero);
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
            Assert.That(_session.States[portal.Identity].PortalOpen, Is.False);
        }

        [Test]
        public void ScriptStateAndStablePortalIdentitySurviveUnloadReload()
        {
            WorldObject first = Portal(1, PanariiOfficeDoorScript);
            ArcanumObjectId identity = first.Identity;
            PersistentObjectState state = _session.States[identity];
            _session.BindUseScriptSource(n => n == PanariiOfficeDoorScript ? PanariiGate() : null);
            _session.ScriptGlobals.SetFlag(PanariiOfficeDoorFlag, 1);

            _session.UnloadSector(Sector);
            Object.DestroyImmediate(first.gameObject);
            _session.BeginSector(Sector);
            WorldObject restored = Runtime("RestoredPortal", ObjectType.Portal);
            _session.Bind(Sector, state, restored);
            restored.PortalOpenable = true;
            _session.BindPortal(state, restored, 7, 8);

            WorldInteractionResult result = Use(restored);
            Assert.That(restored.Identity, Is.EqualTo(identity));
            Assert.That(_session.ScriptGlobals.GetFlag(PanariiOfficeDoorFlag), Is.EqualTo(1));
            Assert.That(result.ScriptRunDefault, Is.True);
            Assert.That(result.RequestedPortalOpen, Is.True);
        }

        [Test]
        public void StrictVmDistinguishesMissingUnsupportedAndEmptyScripts()
        {
            var vm = new ScriptVm(new StrictHost());
            Assert.That(vm.ExecuteStrict(null, new ScriptContext()).Status,
                Is.EqualTo(ScriptExecutionStatus.MissingScript));
            Assert.That(vm.ExecuteStrict(new ScriptFile(), new ScriptContext()).Status,
                Is.EqualTo(ScriptExecutionStatus.EmptyScript));
            ScriptFile unsupported = File(Cond((Sct)999, Act(Sat.DoNothing), Act(Sat.DoNothing)));
            Assert.That(vm.ExecuteStrict(unsupported, new ScriptContext()).Status,
                Is.EqualTo(ScriptExecutionStatus.UnsupportedOpcode));
        }

        private WorldInteractionResult Use(WorldObject portal) => _session.ExecuteInteraction(
            new WorldInteractionCommand(_player.Identity, portal.Identity, WorldInteractionCommandType.Use));

        private WorldObject Portal(int number, int scriptNum, int x = 3, int y = 1)
        {
            uint art = 0x30002800u;
            var source = new ObjectInstance(ObjectType.Portal, 2028, Location(x, y), art, 0, 0,
                oid: IdBytes(number));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, art, false, false);
            state.UseScriptNum = scriptNum;
            WorldObject runtime = Runtime("Portal", ObjectType.Portal);
            _session.Bind(Sector, state, runtime);
            runtime.UseScriptNum = scriptNum;
            runtime.PortalOpenable = true;
            _session.BindPortal(state, runtime, 7, 8);
            return runtime;
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        // Shipped scr/01162door_to_the_panarii_offices_use.scr.
        private static ScriptFile PanariiGate() => File(Cond(Sct.Eq,
            Act(Sat.ReturnAndRunDefault), Act(Sat.ReturnAndSkipDefault),
            (Svt.GlFlag, PanariiOfficeDoorFlag), (Svt.Number, 1)));

        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var file = new ScriptFile();
            file.Entries.AddRange(entries);
            return file;
        }

        private static ScriptCondition Cond(Sct type, ScriptAction action, ScriptAction els,
            params (Svt type, int value)[] operands)
        {
            var condition = new ScriptCondition { Type = (int)type, Action = action, Els = els };
            for (int i = 0; i < operands.Length; i++)
            {
                condition.OpType[i] = (byte)operands[i].type;
                condition.OpValue[i] = operands[i].value;
            }
            return condition;
        }

        private static ScriptAction Act(Sat type, params (Svt type, int value)[] operands)
        {
            var action = new ScriptAction { Type = (int)type };
            for (int i = 0; i < operands.Length; i++)
            {
                action.OpType[i] = (byte)operands[i].type;
                action.OpValue[i] = operands[i].value;
            }
            return action;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static SectorNavigationMap Map()
        {
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]),
                new bool[SectorTerrain.TileCount], names);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }

        private static byte[] IdBytes(int number)
        {
            var bytes = new byte[24];
            bytes[0] = 2;
            BitConverter.GetBytes(number).CopyTo(bytes, 4);
            return bytes;
        }

        private sealed class StrictHost : ScriptHostAdapter { }
    }
}
