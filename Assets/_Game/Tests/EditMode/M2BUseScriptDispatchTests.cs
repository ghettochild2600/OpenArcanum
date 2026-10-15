using System;
using System.Reflection;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Art;
using Arcanum.Formats.Script;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using Arcanum.Runtime.UI;
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
        public void ChangeScriptKeepsCurrentInvocationAndRoutesTheNextUseToReplacement()
        {
            WorldObject portal = Portal(41, 42);
            ScriptFile original = File(
                Cond(Sct.True, Act(Sat.ChangeScript,
                    ((Svt)(int)Sfo.Attachee, 0), (Svt.Number, (int)Sap.Use), (Svt.Number, 43)),
                    Act(Sat.DoNothing)),
                Cond(Sct.True, Act(Sat.ReturnAndSkipDefault), Act(Sat.DoNothing)));
            ScriptFile replacement = File(
                Cond(Sct.True, Act(Sat.ReturnAndRunDefault), Act(Sat.DoNothing)));
            _session.BindUseScriptSource(number => number switch { 42 => original, 43 => replacement, _ => null });

            WorldInteractionResult first = Use(portal);
            WorldInteractionResult second = Use(portal);

            Assert.That(first.ScriptNum, Is.EqualTo(42));
            Assert.That(first.ScriptRunDefault, Is.False);
            Assert.That(_session.States[portal.Identity].UseScriptNum, Is.EqualTo(43));
            Assert.That(_session.Campaign.GetScriptNumber(portal.Identity, (int)Sap.Use), Is.EqualTo(43));
            Assert.That(second.ScriptNum, Is.EqualTo(43));
            Assert.That(second.ScriptRunDefault, Is.True);
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

        [Test]
        public void BedRequiresSkipDefaultThenClaimsAndReleasesSourceOccupancy()
        {
            WorldObject bed = Bed(2, 1729);
            uint originalArt = _session.States[bed.Identity].ArtId;
            _session.BindUseScriptSource(n => n == 1729
                ? File(Cond(Sct.True, Act(Sat.ReturnAndRunDefault), Act(Sat.DoNothing))) : null);

            WorldInteractionResult refused = Use(bed);
            Assert.That(refused.Code, Is.EqualTo(WorldInteractionResultCode.Blocked));
            Assert.That(refused.BedFailure, Is.EqualTo(BedUseFailure.PermissionDenied));
            Assert.That(refused.ScriptRunDefault, Is.EqualTo(true));
            Assert.That(_session.States[bed.Identity].SceneryOccupantIdentity.IsNull, Is.True);

            _session.BindUseScriptSource(n => n == 1729 ? PermittedBedScript() : null);
            WorldInteractionResult allowed = Use(bed);
            PersistentObjectState state = _session.States[bed.Identity];
            Assert.That(allowed.Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(allowed.BedFailure, Is.EqualTo(BedUseFailure.None));
            Assert.That(allowed.SleepMode, Is.EqualTo(SleepLocationMode.BedSleep));
            Assert.That(state.SceneryOccupantIdentity, Is.EqualTo(_player.Identity));
            Assert.That(_session.Beds.IsActiveFor(_player.Identity), Is.True);
            Assert.That((state.UseScriptLocalFlags & (1u << 3)) != 0, Is.True,
                "per-bed SAP_USE locals belong to the scenery instance");
            Assert.That(state.ArtId, Is.EqualTo(originalArt + (1u << 14)));

            _session.Beds.Leave(_player.Identity);
            Assert.That(state.SceneryOccupantIdentity.IsNull, Is.True);
            Assert.That(state.ArtId, Is.EqualTo(originalArt));
        }

        [Test]
        public void OccupiedBedFailsAfterItsPermissionScriptRuns()
        {
            WorldObject bed = Bed(3, 1729);
            PersistentObjectState state = _session.States[bed.Identity];
            state.SceneryOccupantIdentity = ArcanumObjectId.CreateSessionDynamic(99);
            _session.BindUseScriptSource(n => n == 1729 ? PermittedBedScript() : null);

            WorldInteractionResult result = Use(bed);

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.Blocked));
            Assert.That(result.BedFailure, Is.EqualTo(BedUseFailure.Occupied));
            Assert.That((state.UseScriptLocalFlags & (1u << 3)) != 0, Is.True,
                "sleep_ui dispatches SAP_USE before critter_enter_bed checks occupancy");
            Assert.That(state.SceneryOccupantIdentity, Is.EqualTo(ArcanumObjectId.CreateSessionDynamic(99)));
        }

        [Test]
        public void BedSleepUsesSharedSourceTimeDoubledRecoveryAndPoisonLifecycle()
        {
            WorldObject bed = Bed(4, 1729);
            _session.BindUseScriptSource(n => n == 1729 ? PermittedBedScript() : null);
            Assert.That(Use(bed).IsSuccess, Is.True);
            _session.Vitality.ApplyHitPointDamage(_player.Identity, 10);
            _session.Vitality.ApplyFatigueDamage(_player.Identity, 10);
            Assert.That(_session.Poison.Apply(_player.Identity, 1).ResultingLevel, Is.EqualTo(1));
            int healRate = _session.DerivedStats.GetDerivedStat(_player.Identity,
                Arcanum.Runtime.Character.CharacterDerivedStat.HealRate);

            SleepResult result = _session.GameplayHud.Sleep(SleepOption.OneHour);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Mode, Is.EqualTo(SleepLocationMode.BedSleep));
            Assert.That(result.HoursAdvanced, Is.EqualTo(1));
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(3_600_000));
            Assert.That(_session.Poison.GetLevel(_player.Identity), Is.Zero,
                "sleep advances poison through SourceTime rather than a sleep special case");
            Assert.That(_session.Vitality.Get(_player.Identity).HitPointDamage,
                Is.EqualTo(Math.Max(0, 18 - 2 * healRate)));
            Assert.That(_session.Vitality.Get(_player.Identity).FatigueDamage,
                Is.EqualTo(Math.Max(0, 10 - 6 * healRate)));
            Assert.That(_session.Beds.IsActiveFor(_player.Identity), Is.False);
            Assert.That(_session.States[bed.Identity].SceneryOccupantIdentity.IsNull, Is.True);
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

        private WorldObject Bed(int number, int scriptNum, int x = 3, int y = 1)
        {
            uint art = ((uint)ArtId.TypeScenery << 28)
                       | ((uint)BedSleepAuthority.SourceBedScenerySubtype << 6) | (1u << 19);
            var source = new ObjectInstance(ObjectType.Scenery, 20000 + number, Location(x, y), art, 0, 0,
                oid: IdBytes(number));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, art, false, false);
            state.UseScriptNum = scriptNum;
            WorldObject runtime = Runtime("Bed", ObjectType.Scenery);
            _session.Bind(Sector, state, runtime);
            runtime.UseScriptNum = scriptNum;
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

        private static ScriptFile PermittedBedScript() => File(
            Cond(Sct.True, Act(Sat.SetLocalFlag, (Svt.Number, 3)), Act(Sat.DoNothing)),
            Cond(Sct.True, Act(Sat.ReturnAndSkipDefault), Act(Sat.DoNothing)));

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
