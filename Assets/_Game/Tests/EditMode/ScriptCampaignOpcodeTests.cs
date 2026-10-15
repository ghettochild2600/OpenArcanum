using System;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Script;
using Arcanum.Runtime;
using Arcanum.Script;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    [TestFixture]
    [Category("ScriptCompatibility")]
    public sealed class ScriptCampaignOpcodeTests
    {
        private sealed class Host : ScriptHostAdapter, IScriptCampaignHost
        {
            public readonly object Triggerer = new();
            public readonly object Attachee = new();
            public readonly object Player = new();
            public int MagicResult = 73;
            public int Reaction = 55;
            public int Faction = 7;
            public int ObjectType = 16;
            public int AddedReputation;
            public int RemovedReputation;
            public int TransferredItem;
            public int CreatedItem;
            public int DestroyCalls;
            public int DestroyedNamedItem;
            public int CurrentFatigue = 37;
            public int MaximumFatigue = 62;
            public int AdvancedSeconds;
            public int AdvanceCalls;
            public int FatePointCalls;
            public int ScheduledScript;
            public int ScheduledLine;
            public int ScheduledSeconds;
            public bool ScheduledAt;
            public int RemovedScript;
            public int AddedEffect;
            public int AddedEffectCause;
            public int RemovedEffect;
            public int FatigueCalls;
            public bool Reputation;
            public bool Identified = true;
            public int MagicCalls;
            public int Day = 3, Hour = 17, Minute = 42, Hours = 89;

            public override object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext context)
                => (Sfo)sfoType switch
                {
                    Sfo.Triggerer => new[] { context.Triggerer ?? Triggerer },
                    Sfo.Attachee => new[] { context.Attachee ?? Attachee },
                    Sfo.Player => new[] { Player },
                    _ => Array.Empty<object>(),
                };

            public int GetObjectType(object obj) => ObjectType;
            public int GetMagicTechAdjustment(int value, object item, object source, object target)
            { MagicCalls++; return MagicResult; }
            public int GetMagicTechAdjustmentEx(int value, object item, object source)
            { MagicCalls++; return MagicResult; }
            public int GetReaction(object npc, object pc) => Reaction;
            public void SetReaction(object npc, object pc, int value) => Reaction = value;
            public void AdjustReaction(object npc, object pc, int delta) => Reaction += delta;
            public bool HasReputation(object obj, int reputation) => Reputation;
            public bool IsIdentified(object obj) => Identified;
            public void AddReputation(object obj, int reputation) { Reputation = true; AddedReputation = reputation; }
            public void RemoveReputation(object obj, int reputation) { Reputation = false; RemovedReputation = reputation; }
            public int GetFaction(object obj) => Faction;
            public void GetLocation(object obj, out int x, out int y) { x = 12; y = 34; }
            public int DistanceBetween(object left, object right) => 6;
            public int GetArea(object obj) => 9;
            public void GetFatiguePoints(object obj, out int current, out int maximum)
            { FatigueCalls++; current = CurrentFatigue; maximum = MaximumFatigue; }
            public void TransferItem(int name, object from, object to) => TransferredItem = name;
            public void CreateItem(int prototype, object parent) => CreatedItem = prototype;
            public void Destroy(object obj) => DestroyCalls++;
            public void DestroyItemNamed(int name, object owner) => DestroyedNamedItem = name;
            public int GetDaySinceStartup() => Day;
            public int GetCurrentHour() => Hour;
            public int GetCurrentMinute() => Minute;
            public int GetHoursSinceStartup() => Hours;
            public void AdvanceTimeSeconds(int seconds) { AdvanceCalls++; AdvancedSeconds += seconds; }
            public void ScheduleScriptCall(int scriptNumber, int startLine, object triggerer, object attachee,
                int seconds, bool atTimeOfDay)
            {
                ScheduledScript = scriptNumber;
                ScheduledLine = startLine;
                ScheduledSeconds = seconds;
                ScheduledAt = atTimeOfDay;
            }
            public void RemoveScriptCall(int scriptNumber, object attachee) => RemovedScript = scriptNumber;
            public void AddEffect(object target, int effectId, int cause)
            { AddedEffect = effectId; AddedEffectCause = cause; }
            public void RemoveEffect(object target, int effectId) => RemovedEffect = effectId;
            public void GrantOneFatePoint(object obj) => FatePointCalls++;
        }

        [Test]
        public void CampaignQueriesWriteSourceDestinations()
        {
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Triggerer, Attachee = host.Attachee };
            ScriptFile file = File(
                Entry(Act(Sat.GetObjectType, Obj(Sfo.Attachee), Val(Svt.LcVar, 0))),
                Entry(Act(Sat.GetReaction, Obj(Sfo.Attachee), Obj(Sfo.Triggerer), Val(Svt.LcVar, 1))),
                Entry(Act(Sat.GetFaction, Obj(Sfo.Attachee), Val(Svt.LcVar, 2))),
                Entry(Act(Sat.GetDaySinceStartup, Val(Svt.LcVar, 3))),
                Entry(Act(Sat.GetCurrentHour, Val(Svt.LcVar, 4))),
                Entry(Act(Sat.GetCurrentMinute, Val(Svt.LcVar, 5))),
                Entry(Act(Sat.GetHoursSinceStartup, Val(Svt.LcVar, 6))));

            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(file, context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(context.LocalVars.Take(7), Is.EqualTo(new[] { 16, 55, 7, 3, 17, 42, 89 }));
        }

        [Test]
        public void SocialMutationsAndReputationConditionUseCampaignAuthority()
        {
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Triggerer, Attachee = host.Attachee };
            ScriptFile file = File(
                Entry(Act(Sat.SetReaction, Obj(Sfo.Attachee), Obj(Sfo.Triggerer), Val(Svt.Number, 60))),
                Entry(Act(Sat.AdjustReaction, Obj(Sfo.Attachee), Obj(Sfo.Triggerer), Val(Svt.Number, -4))),
                Entry(Act(Sat.AddReputation, Obj(Sfo.Triggerer), Val(Svt.Number, 1007))),
                Cond(Sct.ObjHasReputation,
                    Act(Sat.AssignNum, Val(Svt.LcVar, 0), Val(Svt.Number, 1)), Nop(),
                    Obj(Sfo.Triggerer), Val(Svt.Number, 1007)),
                Entry(Act(Sat.RemoveReputation, Obj(Sfo.Triggerer), Val(Svt.Number, 1007))));

            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(file, context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(host.Reaction, Is.EqualTo(56));
            Assert.That(host.AddedReputation, Is.EqualTo(1007));
            Assert.That(host.RemovedReputation, Is.EqualTo(1007));
            Assert.That(context.LocalVars[0], Is.EqualTo(1));
        }

        [Test]
        public void EffectActionsResolveTheEntireSourceSetAndPreserveCause()
        {
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Triggerer, Attachee = host.Attachee };
            ScriptFile file = File(
                Entry(Act(Sat.AddEffect, Obj(Sfo.Triggerer), Val(Svt.Number, 282), Val(Svt.Number, 5))),
                Entry(Act(Sat.RemoveEffect, Obj(Sfo.Triggerer), Val(Svt.Number, 282))));

            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(file, context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(host.AddedEffect, Is.EqualTo(282));
            Assert.That(host.AddedEffectCause, Is.EqualTo(5));
            Assert.That(host.RemovedEffect, Is.EqualTo(282));
        }

        [Test]
        public void RealRetailIdentifiedConditionUsesSourceFocusOperand()
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            ScriptFile retail = ScriptDatabase.Load(vfs).Get(28020);
            ScriptCondition source = retail.Entries.First(entry => entry.Type == (int)Sct.Identified);
            source.Action = Act(Sat.AssignNum, Val(Svt.LcVar, 0), Val(Svt.Number, 1));
            source.Els = Nop();
            var host = new Host();
            var context = new ScriptContext
            {
                ScriptNum = 28020,
                Triggerer = host.Triggerer,
                Attachee = host.Attachee,
            };

            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(File(source), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(context.LocalVars[0], Is.EqualTo(1));
        }

        [Test]
        public void ChangeThisScriptPersistsReplacementWithoutChangingRunningDialogSource()
        {
            var host = new Host();
            var context = new ScriptContext { ScriptNum = 1084 };

            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(
                File(Entry(Act(Sat.ChangeThisScriptToScript, Val(Svt.Number, 1179)))), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(context.ScriptNum, Is.EqualTo(1084));
            Assert.That(context.ReplacementScriptNum, Is.EqualTo(1179));
            Assert.That(context.RemoveScript, Is.False);
        }

        [Test]
        public void SpatialAndInventoryOperationsUseCampaignAuthority()
        {
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Triggerer, Attachee = host.Attachee };
            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(File(
                Entry(Act(Sat.GetLocation, Obj(Sfo.Attachee), Val(Svt.LcVar, 0), Val(Svt.LcVar, 1))),
                Entry(Act(Sat.DistanceBetween, Obj(Sfo.Triggerer), Obj(Sfo.Attachee), Val(Svt.LcVar, 2))),
                Entry(Act(Sat.GetArea, Obj(Sfo.Attachee), Val(Svt.LcVar, 3))),
                Entry(Act(Sat.TransferItem, Val(Svt.Number, 4001), Obj(Sfo.Attachee), Obj(Sfo.Triggerer))),
                Entry(Act(Sat.CreateItem, Val(Svt.Number, 9001), Obj(Sfo.Triggerer))),
                Entry(Act(Sat.Destroy, Obj(Sfo.Attachee))),
                Entry(Act(Sat.DestroyItemNamed, Val(Svt.Number, 4002), Obj(Sfo.Triggerer)))), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(context.LocalVars.Take(4), Is.EqualTo(new[] { 12, 34, 6, 9 }));
            Assert.That(host.TransferredItem, Is.EqualTo(4001));
            Assert.That(host.CreatedItem, Is.EqualTo(9001));
            Assert.That(host.DestroyCalls, Is.EqualTo(1));
            Assert.That(host.DestroyedNamedItem, Is.EqualTo(4002));
        }

        [Test]
        public void FatigueFadeAndFateOperationsUseAuthoritativeCampaignState()
        {
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Player };
            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(File(
                Entry(Act(Sat.GetFatiguePoints, Obj(Sfo.Triggerer), Val(Svt.LcVar, 0), Val(Svt.LcVar, 1))),
                Entry(Act(Sat.Fade, Val(Svt.Number, 45), Val(Svt.Number, 0), Val(Svt.Number, 0))),
                Entry(Act(Sat.GrantOneFatePoint, Obj(Sfo.Triggerer)))), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(context.LocalVars.Take(2), Is.EqualTo(new[] { 37, 62 }));
            Assert.That(host.AdvanceCalls, Is.EqualTo(1));
            Assert.That(host.AdvancedSeconds, Is.EqualTo(45));
            Assert.That(host.FatePointCalls, Is.EqualTo(1));
        }

        [Test]
        public void TimerOperationsPreserveSourceOperandLayoutAndCancellationIdentity()
        {
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Triggerer, Attachee = host.Attachee };
            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(File(
                Entry(Act(Sat.CallScriptIn, Val(Svt.Number, 1179), Val(Svt.Number, 8),
                    Obj(Sfo.Triggerer), Obj(Sfo.Attachee), Val(Svt.Number, 30))),
                Entry(Act(Sat.RemoveScriptCall, Val(Svt.Number, 1179), Obj(Sfo.Attachee)))), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That((host.ScheduledScript, host.ScheduledLine, host.ScheduledSeconds, host.ScheduledAt),
                Is.EqualTo((1179, 8, 30, false)));
            Assert.That(host.RemovedScript, Is.EqualTo(1179));
        }

        [TestCase(28000, Sat.GetMagictechAdjustment)]
        [TestCase(2664, Sat.GetMagictechAdjustmentEx)]
        public void RealRetailMagicTechInstructionExecutesWithSourceOperandLayout(int scriptNumber, Sat opcode)
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            ScriptFile retail = ScriptDatabase.Load(vfs).Get(scriptNumber);
            ScriptAction source = retail.Entries.SelectMany(entry => new[] { entry.Action, entry.Els })
                .First(action => action != null && action.Type == (int)opcode);
            var host = new Host();
            var context = new ScriptContext { Triggerer = host.Triggerer, Attachee = host.Attachee };
            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(File(Entry(source)), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(host.MagicCalls, Is.EqualTo(1));
            int destination = opcode == Sat.GetMagictechAdjustment ? 4 : 3;
            Assert.That(ReadDestination(source.OpType[destination], source.OpValue[destination], context),
                Is.EqualTo(host.MagicResult));
        }

        [TestCase(1059, Sat.PlaySound)]
        [TestCase(1092, Sat.PlayScriptEyeCandy)]
        [TestCase(1074, Sat.PlaySpellEyeCandy)]
        [TestCase(2210, Sat.StopScriptEyeCandy)]
        public void RealRetailPresentationInstructionPreservesStrictControlFlow(int scriptNumber, Sat opcode)
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            ScriptFile retail = ScriptDatabase.Load(vfs).Get(scriptNumber);
            ScriptAction source = retail.Entries.SelectMany(entry => new[] { entry.Action, entry.Els })
                .First(action => action != null && action.Type == (int)opcode);

            var context = new ScriptContext { ScriptNum = scriptNumber };
            ScriptExecutionResult result = new ScriptVm(new Host()).ExecuteStrict(
                File(Entry(source), Entry(Act(Sat.AssignNum, Val(Svt.LcVar, 0), Val(Svt.Number, 1)))),
                context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(context.LocalVars[0], Is.EqualTo(1));
        }

        [TestCase(1291, Sat.GetFatiguePoints)]
        [TestCase(1087, Sat.Fade)]
        [TestCase(1037, Sat.GrantOneFatePoint)]
        public void RealRetailCampaignStateInstructionUsesSourceOperandLayout(int scriptNumber, Sat opcode)
        {
            using DatVirtualFileSystem vfs = MountSourceData();
            ScriptFile retail = ScriptDatabase.Load(vfs).Get(scriptNumber);
            ScriptAction source = retail.Entries.SelectMany(entry => new[] { entry.Action, entry.Els })
                .First(action => action != null && action.Type == (int)opcode);
            var host = new Host();
            var context = new ScriptContext
            {
                ScriptNum = scriptNumber,
                Triggerer = host.Triggerer,
                Attachee = host.Attachee,
            };

            ScriptExecutionResult result = new ScriptVm(host).ExecuteStrict(File(Entry(source)), context);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            if (opcode == Sat.GetFatiguePoints) Assert.That(host.FatigueCalls, Is.EqualTo(1));
            if (opcode == Sat.Fade) Assert.That(host.AdvanceCalls, Is.EqualTo(1));
            if (opcode == Sat.GrantOneFatePoint) Assert.That(host.FatePointCalls, Is.EqualTo(1));
        }

        private static int ReadDestination(byte type, int value, ScriptContext context)
            => (Svt)type switch
            {
                Svt.LcVar => context.LocalVars[value],
                _ => throw new AssertionException($"Retail proof expected an LC_VAR destination, got {(Svt)type}."),
            };

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

        private static ScriptCondition Cond(Sct type, ScriptAction action, ScriptAction els,
            params (Svt t, int v)[] operands)
        {
            var condition = new ScriptCondition { Type = (int)type, Action = action, Els = els };
            for (int index = 0; index < operands.Length; index++)
            {
                condition.OpType[index] = (byte)operands[index].t;
                condition.OpValue[index] = operands[index].v;
            }
            return condition;
        }

        private static ScriptCondition Entry(ScriptAction action) => Cond(Sct.True, action, Nop());

        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var file = new ScriptFile();
            file.Entries.AddRange(entries);
            return file;
        }
    }
}
