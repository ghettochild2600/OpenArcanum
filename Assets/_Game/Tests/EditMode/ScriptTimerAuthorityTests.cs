using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.Script;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [TestFixture]
    [Category("ScriptCompatibility")]
    public sealed class ScriptTimerAuthorityTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private ArcanumObjectId _actor;
        private readonly Dictionary<int, ScriptFile> _scripts = new();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(ScriptTimerAuthorityTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _actor = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.GetOrCreatePlayer(_actor, Sector, Vector2.one, 0x28100000u);
            _session.BindUseScriptSource(number => _scripts.GetValueOrDefault(number));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _scripts.Clear();
        }

        [Test]
        public void DelayedCallsUseEightTimesGameDelayAndSourceEqualDeadlineOrder()
        {
            _scripts[1] = AssignGlobal(50, 1);
            _scripts[2] = AssignGlobal(50, 2);
            _scripts[3] = AssignGlobal(50, 3);
            var order = new List<int>();
            _session.ScriptTimers.CallExecuted += (call, _) => order.Add(call.ScriptNumber);

            _session.ScriptTimers.Schedule(1, 0, _actor, _actor, 2);
            _session.ScriptTimers.Schedule(2, 0, _actor, _actor, 2);
            _session.ScriptTimers.Schedule(3, 0, _actor, _actor, 1);
            _session.SourceTime.Advance(7_999);
            Assert.That(order, Is.Empty);
            _session.SourceTime.Advance(1);
            Assert.That(order, Is.EqualTo(new[] { 3 }));
            _session.SourceTime.Advance(8_000);

            Assert.That(order, Is.EqualTo(new[] { 3, 2, 1 }),
                "retail inserts a newer equal-deadline event before the older event");
            Assert.That(_session.Campaign.GetVar(50), Is.EqualTo(1));
            Assert.That(_session.ScriptTimers.Calls, Is.Empty);
        }

        [Test]
        public void SaveStateAndMapProjectionDoNotOwnScheduledCalls()
        {
            _scripts[4] = AssignGlobal(51, 9);
            _session.ScriptTimers.Schedule(4, 0, _actor, _actor, 2);
            ScriptTimerSaveData saved = _session.ScriptTimers.ExportSaveData();

            _session.ClearSelectedSector();
            Assert.That(_session.SelectSector(Sector), Is.True);
            _session.ScriptTimers.RestoreSaveData(saved);
            _session.SourceTime.Advance(16_000);

            Assert.That(_session.Campaign.GetVar(51), Is.EqualTo(9));
            Assert.That(_session.ScriptTimers.Calls, Is.Empty);
        }

        [Test]
        public void DestroyedTargetCancelsExecutionAndLargeAdvanceRunsEveryOtherDueCall()
        {
            _scripts[5] = AssignGlobal(52, 5);
            _scripts[6] = AssignGlobal(53, 6);
            ArcanumObjectId target = AddObject(77).Identity;
            _session.ScriptTimers.Schedule(5, 0, _actor, target, 1);
            for (int index = 0; index < 600; index++)
                _session.ScriptTimers.Schedule(6, 0, _actor, _actor, 3);
            Assert.That(_session.DestroyScriptObject(target), Is.True);

            _session.SourceTime.Advance(100_000);

            Assert.That(_session.Campaign.GetVar(52), Is.Zero);
            Assert.That(_session.Campaign.GetVar(53), Is.EqualTo(6));
            Assert.That(_session.ScriptTimers.Calls, Is.Empty);
        }

        [Test]
        public void CallAtUsesElapsedCampaignDayAndRetailMinimumDelay()
        {
            _scripts[9] = AssignGlobal(55, 9);
            _session.ScriptTimers.Schedule(9, 0, _actor, _actor, 0, atTimeOfDay: true);
            Assert.That(_session.ScriptTimers.Calls.Single().DueCampaignMilliseconds,
                Is.EqualTo(_session.SourceTime.CampaignMilliseconds + 1));

            _session.SourceTime.Advance(1);
            Assert.That(_session.Campaign.GetVar(55), Is.EqualTo(9));

            _session.ScriptTimers.Schedule(9, 0, _actor, _actor, 0);
            Assert.That(_session.ScriptTimers.Calls.Single().DueCampaignMilliseconds,
                Is.EqualTo(_session.SourceTime.CampaignMilliseconds + SourceTimeService.RealTimeScale));
        }

        [Test]
        public void CallbackScheduledDuringCallbackWaitsForNextTimeEventPass()
        {
            _scripts[7] = File(Entry(Act(Sat.CallScriptIn,
                Val(Svt.Number, 8), Val(Svt.Number, 0), Obj(Sfo.Triggerer), Obj(Sfo.Attachee),
                Val(Svt.Number, 0))));
            _scripts[8] = AssignGlobal(54, 8);
            _session.ScriptTimers.Schedule(7, 0, _actor, _actor, 0);

            _session.SourceTime.Advance(SourceTimeService.RealTimeScale);
            Assert.That(_session.Campaign.GetVar(54), Is.Zero);
            Assert.That(_session.ScriptTimers.Calls.Select(value => value.ScriptNumber), Is.EqualTo(new[] { 8 }));

            _session.SourceTime.Advance(SourceTimeService.RealTimeScale);
            Assert.That(_session.Campaign.GetVar(54), Is.EqualTo(8));
        }

        [TestCase(1000)]
        [TestCase(1598)]
        [TestCase(2970)]
        public void RealRetailCallScriptInInstructionSchedulesThroughProductionVm(int retailScriptNumber)
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            ScriptFile retail = ScriptDatabase.Load(vfs).Get(retailScriptNumber);
            ScriptAction source = retail.Entries.SelectMany(entry => new[] { entry.Action, entry.Els })
                .First(action => action != null && action.Type == (int)Sat.CallScriptIn);
            _scripts[retailScriptNumber] = File(Entry(source));
            _session.ScriptTimers.Schedule(retailScriptNumber, 0, _actor, _actor, 0);

            _session.SourceTime.Advance(SourceTimeService.RealTimeScale);

            Assert.That(_session.ScriptTimers.Calls, Has.Count.EqualTo(1));
            ScheduledScriptCall scheduled = _session.ScriptTimers.Calls[0];
            Assert.That(scheduled.ScriptNumber, Is.EqualTo(source.OpValue[0]));
            Assert.That(scheduled.StartLine, Is.EqualTo(source.OpValue[1]));
            long sourceDelay = source.OpValue[4] == 0 ? 1L : source.OpValue[4] * 1_000L;
            Assert.That(scheduled.DueCampaignMilliseconds - _session.SourceTime.CampaignMilliseconds,
                Is.EqualTo(sourceDelay * SourceTimeService.RealTimeScale));
            Assert.That((scheduled.Triggerer, scheduled.Attachee), Is.EqualTo((_actor, _actor)));
        }

        private PersistentObjectState AddObject(int value)
        {
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            var source = new ObjectInstance(ObjectType.Scenery, 2000 + value, 0L,
                0x20000000u, 0, 0, oid: bytes);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
        }

        private static ScriptFile AssignGlobal(int index, int value)
            => File(Entry(Act(Sat.AssignNum, Val(Svt.GlVar, index), Val(Svt.Number, value))));

        private static DatVirtualFileSystem MountSourceData()
        {
            var vfs = new DatVirtualFileSystem();
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) throw new FileNotFoundException("modules/Arcanum.dat");
            vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) vfs.MountFile(path);
            }
            return vfs;
        }

        private static (Svt t, int v) Val(Svt type, int value) => (type, value);
        private static (Svt t, int v) Obj(Sfo type, int value = 0) => ((Svt)(int)type, value);
        private static ScriptAction Nop() => new() { Type = (int)Sat.DoNothing };

        private static ScriptAction Act(Sat type, params (Svt t, int v)[] operands)
        {
            var action = new ScriptAction { Type = (int)type };
            for (int index = 0; index < operands.Length; index++)
            {
                action.OpType[index] = (byte)operands[index].t;
                action.OpValue[index] = operands[index].v;
            }
            return action;
        }

        private static ScriptCondition Entry(ScriptAction action)
            => new() { Type = (int)Sct.True, Action = action, Els = Nop() };

        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var file = new ScriptFile();
            file.Entries.AddRange(entries);
            return file;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
