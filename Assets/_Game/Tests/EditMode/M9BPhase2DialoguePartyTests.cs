using System;
using System.Collections.Generic;
using System.Reflection;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.World;
using Arcanum.Script;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M9BPhase2DialogueParty")]
    public sealed class M9BPhase2DialoguePartyTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/86570436012.sec";
        private const int DialogueNumber = 1324;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _npc;
        private DialogScript _dialogue;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M9BPhase2DialoguePartyTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc));
            _npc = AddNpc("G_A09DCD63_7A15_D411_8F1D_00E02920220C", DialogueNumber);
            _dialogue = PartyDialogue();
            _session.BindDialogueSource(_ => VirgilShapeScript(), _ => _dialogue);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void DialogueJoinFollowerGateAndLeaveUseAuthoritativePartyState()
        {
            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(1));
            Assert.That(_session.Dialogue.AvailableResponses, Has.Count.EqualTo(1));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Completed));
            Assert.That(_session.Party.Members, Is.EqualTo(new[] { new PartyMember(_npc, false) }));

            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            Assert.That(_session.Dialogue.CurrentLine, Is.EqualTo(10));
            Assert.That(_session.Dialogue.AvailableResponses, Has.Count.EqualTo(1));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.Completed));
            Assert.That(_session.Party.Members, Is.Empty);
        }

        [Test]
        public void CapacityFailurePreflightsBeforeLocalFlagOrMembershipMutation()
        {
            Assert.That(_session.Party.Join(AddNpc("G_11111111_1111_1111_1111_111111111111", 1)).Succeeded, Is.True);
            Assert.That(_session.Party.Join(AddNpc("G_22222222_2222_2222_2222_222222222222", 1)).Succeeded, Is.True);
            _dialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [1] = new(1, "Join?", "", 0, "", 0, ""),
                [2] = new(2, "Yes", "", 1, "fo 1", 0, "lf 1 1 jo"),
            });
            _session.BindDialogueSource(_ => StartAt(1), _ => _dialogue);

            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*UnsupportedEffect.*follower join rejected: CapacityReached"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Campaign.GetLocalFlag(_npc, (int)Sap.Dialog, 1), Is.Zero);
            Assert.That(_session.Party.IsMember(_npc), Is.False);
        }

        [Test]
        public void MissingTargetRollsBackSuccessfulJoinTransaction()
        {
            _dialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [1] = new(1, "Join?", "", 0, "", 0, ""),
                [2] = new(2, "Yes", "", 1, "fo 1", 999, "jo"),
            });
            _session.BindDialogueSource(_ => StartAt(1), _ => _dialogue);

            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*ExecutionFailure.*has no NPC line 999"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.ExecutionFailed));
            Assert.That(_session.Party.Members, Is.Empty);
        }

        [Test]
        public void DuplicateJoinAndNonmemberLeaveFailClosed()
        {
            Assert.That(_session.Party.Join(_npc).Succeeded, Is.True);
            _dialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [1] = new(1, "Again?", "", 0, "", 0, ""),
                [2] = new(2, "Join", "", 1, "", 0, "jo"),
            });
            _session.BindDialogueSource(_ => StartAt(1), _ => _dialogue);
            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*follower join rejected: AlreadyFollowing"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Party.Members, Has.Count.EqualTo(1));

            _session.Dialogue.Cancel("reset");
            Assert.That(_session.Party.Remove(_npc).Succeeded, Is.True);
            _dialogue = new DialogScript(new SortedDictionary<int, DialogLine>
            {
                [1] = new(1, "Leave?", "", 0, "", 0, ""),
                [2] = new(2, "Leave", "", 1, "", 0, "lv"),
            });
            _session.BindDialogueSource(_ => StartAt(1), _ => _dialogue);
            Assert.That(_session.Dialogue.Start(_pc.Identity, _npc), Is.EqualTo(DialogueStartStatus.Started));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "OpenArcanum dialogue compatibility:.*follower removal rejected: NotFollowing"));
            Assert.That(_session.Dialogue.SelectResponse(0), Is.EqualTo(DialogueChoiceStatus.UnsupportedEffect));
            Assert.That(_session.Party.Members, Is.Empty);
        }

        private ArcanumObjectId AddNpc(string key, int dialogue)
        {
            ArcanumObjectId identity = Parse(key);
            var source = new ObjectInstance(ObjectType.Npc, 17102, Location(3, 1),
                0x28100000u, 0, 0, oid: GuidBytes(key), dialogNum: dialogue);
            PersistentObjectState state = _session.GetOrCreate(source, Sector,
                source.CurrentArtId.Value, false, false);
            int[] stats = Stats();
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 17102, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 17102,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 17102,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, 0, 0));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, 17102,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            _session.Bind(Sector, state, Runtime("Npc", ObjectType.Npc));
            return identity;
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.ArtId = 0x28100000u;
            runtime.Tile = new Vector2Int(3, 1);
            runtime.TilePosition = runtime.Tile;
            return runtime;
        }

        private static DialogScript PartyDialogue() => new(new SortedDictionary<int, DialogLine>
        {
            [1] = new(1, "Join?", "", 0, "", 0, ""),
            [2] = new(2, "Yes", "", 1, "fo 1", 0, "jo"),
            [10] = new(10, "Leave?", "", 0, "", 0, ""),
            [11] = new(11, "Yes", "", 1, "fo 0", 0, "lv"),
        });

        private static ScriptFile VirgilShapeScript() => File(
            Cond(Sct.ObjFollowingPc, Act(Sat.Goto, (Svt.Number, 3)), Nop(), Obj(Sfo.Attachee)),
            Cond(Sct.ObjJilted, Act(Sat.Goto, (Svt.Number, 5)), Nop(), Obj(Sfo.Attachee)),
            Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 1)), Nop()),
            Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 10)), Nop()),
            Cond(Sct.True, Act(Sat.ReturnAndSkipDefault), Nop()),
            Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, 1)), Nop()));

        private static ScriptFile StartAt(int line)
            => File(Cond(Sct.True, Act(Sat.Dialog, (Svt.Number, line)), Nop()));

        private static ScriptFile File(params ScriptCondition[] entries)
        {
            var file = new ScriptFile();
            file.Entries.AddRange(entries);
            return file;
        }

        private static ScriptCondition Cond(Sct type, ScriptAction action, ScriptAction els,
            params (Svt t, int v)[] ops)
        {
            var condition = new ScriptCondition { Type = (int)type, Action = action, Els = els };
            for (int index = 0; index < ops.Length && index < 8; index++)
            {
                condition.OpType[index] = (byte)ops[index].t;
                condition.OpValue[index] = ops[index].v;
            }
            return condition;
        }

        private static ScriptAction Act(Sat type, params (Svt t, int v)[] ops)
        {
            var action = new ScriptAction { Type = (int)type };
            for (int index = 0; index < ops.Length && index < 8; index++)
            {
                action.OpType[index] = (byte)ops[index].t;
                action.OpValue[index] = ops[index].v;
            }
            return action;
        }

        private static ScriptAction Nop() => new() { Type = (int)Sat.DoNothing };
        private static (Svt t, int v) Obj(Sfo focus) => ((Svt)(int)focus, 0);
        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static int[] Stats()
        {
            var result = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
            result[CharacterProgressionSource.LevelSourceSlot] = 1;
            result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return result;
        }

        private static ArcanumObjectId Parse(string key)
        {
            Assert.That(ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity), Is.True);
            return identity;
        }

        private static byte[] GuidBytes(string key)
        {
            string compact = key.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property.SetValue(target, value);
        }
    }
}
